/*
  Copyright (c) Microsoft Corporation. All rights reserved.
  Licensed under the MIT License. See License.txt in the project root for license information.
*/

using System;
using System.Collections.Generic;
using System.Linq;
using Adxstudio.Xrm.AspNet;
using Adxstudio.Xrm.AspNet.PortalBus;
using Adxstudio.Xrm.Caching;
using Adxstudio.Xrm.Search;
using Adxstudio.Xrm.Cms;
using Adxstudio.Xrm.Core.Flighting;
using Adxstudio.Xrm.Configuration;
using Adxstudio.Xrm.IO;
using Microsoft.Owin;
using Polly;
using Adxstudio.Xrm.Threading;
using Microsoft.Xrm.Client.Services.Messages;
using Microsoft.Xrm.Sdk;

namespace Adxstudio.Xrm.EventHubBasedInvalidation
{
	public class PortalCacheInvalidatorThread
	{
		private static readonly bool timeTrackingTelemetry = WebAppConfigurationProvider.GetTimeTrackingTelemetryString();
		private static readonly Lazy<PortalCacheInvalidatorThread> instance = new Lazy<PortalCacheInvalidatorThread>(() => new PortalCacheInvalidatorThread());
		private readonly Lazy<ResiliencePipeline> retryPolicy = new Lazy<ResiliencePipeline>(GetRetryPolicy); 
		private static object mutexLockObject = new object();

		/// <summary>
		/// Gets the instance of this class
		/// </summary>
		public static PortalCacheInvalidatorThread Instance
		{
			get { return instance.Value; }
		}

		/// <summary>
		/// private constructor
		/// </summary>
		private PortalCacheInvalidatorThread() { }

		/// <summary>
		/// This is the 'Run' function for this thread.
		/// </summary>
		public void Run(CrmDbContext context, Guid websiteId)
		{
			lock (mutexLockObject)
			{
				ADXTrace.Instance.TraceVerbose(TraceCategory.Application, string.Format("Cache Invalidation lock acquired "));

				// we want to happen this in lock state since order of update is important
				this.InvalidateEntityRecordCache(context, websiteId);
				this.InvalidateMetadataCache();

				ADXTrace.Instance.TraceVerbose(TraceCategory.Application, string.Format("Cache Invalidation lock released "));
			}
		}

		/// <summary>
		/// Handling for invalidating adx entity record cache
		/// <param name="context">The context</param>
		/// <param name="websiteId">Current website id</param>
		/// </summary>
		private void InvalidateEntityRecordCache(CrmDbContext context, Guid websiteId)
		{
			Dictionary<string, EntityRecordMessage> processingRecords;

			// First invalidate cache then invalidate search-index.
			if (NotificationUpdateManager.Instance.PrepDirtyEntitiesForProcessing(false))
			{
				// Get the messages currently being processed, before they are marked as complete
				processingRecords = NotificationUpdateManager.Instance.ProcessingEntitiesTable();

				// Invalidates cache
				InvalidateEntityRecordCache(context, websiteId, processingRecords, false);
			}

			if (NotificationUpdateManager.Instance.PrepDirtyEntitiesForProcessing(true))
			{
				// Get the messages currently being processed, before they are marked as complete
				processingRecords = NotificationUpdateManager.Instance.ProcessingEntitiesTable();

				// Invalidates search index
				InvalidateEntityRecordCache(context, websiteId, processingRecords, true);
			}
		}

		private void InvalidateEntityRecordCache(CrmDbContext context, Guid websiteId, Dictionary<string, EntityRecordMessage> processingRecords, bool isSearchIndexInvalidation)
		{
			var dirty = NotificationUpdateManager.Instance.GetEntitiesWithTimeStamps(isSearchIndexInvalidation);

			// short circuit exit
			if (!dirty.Any())
				return;

			// Query CRM for the data
			var crmQueryResults = CrmChangeTrackingManager.Instance.RequestRecordDeltaFromCrm(dirty, context, websiteId);

			if (crmQueryResults == null || crmQueryResults.UpdatedEntityRecords == null || !crmQueryResults.UpdatedEntityRecords.Any())
				return;

			ADXTrace.Instance.TraceWarning(TraceCategory.Application, string.Format("Dirty Entity Count {0} ,Processing Entity Count {1} , Changed Entity Record Count {2} ", dirty.Count, processingRecords.Count, crmQueryResults.UpdatedEntityRecords.Count));

			var timeStampTable = isSearchIndexInvalidation ? NotificationUpdateManager.Instance.TimeStampsForSearchIndex : NotificationUpdateManager.Instance.TimeStampsForCache;

			// Convert
			var convertedMessages = NotificationMessageTransformer.Instance.Convert(crmQueryResults.UpdatedEntityRecords, processingRecords);

			// Post to the Adx cache endpoint
			var entitiesWithSuccessfulInvalidation = this.PostRequest(convertedMessages, false, isSearchIndexInvalidation);

			if (processingRecords.Count > 0)
			{
				// Wrap up NotificationUpdateManager
				NotificationUpdateManager.Instance.CompleteEntityProcessing(crmQueryResults.UpdatedEntitiesWithLastTimestamp, entitiesWithSuccessfulInvalidation, isSearchIndexInvalidation);
			}

			if (entitiesWithSuccessfulInvalidation.Any() && PortalCacheInvalidatorThread.timeTrackingTelemetry)
			{
				// When the values were pushed into the portal cache
				DateTime pushedToCache = DateTime.UtcNow;
				this.LogTimeTelemetry(pushedToCache, processingRecords, crmQueryResults, timeStampTable);
			}
		}

		/// <summary>
		/// Logs telemetry for the message processing for each entity record.
		/// Includes how long total it took from update in CRM to update in Portal
		/// </summary>
		/// <param name="pushedToCache">Time at which the records wer pushed to the cache</param>
		/// <param name="processingRecords">Messages that have notified the portal of changes from Azure</param>
		/// <param name="crmQueryResults">Record changes from CRM</param>
		/// <param name="timeStampTable">Timestamps of records before this current batch updated</param>
		private void LogTimeTelemetry(DateTime pushedToCache, Dictionary<string, EntityRecordMessage> processingRecords, TimeBasedChangedData crmQueryResults, Dictionary<string, string> timeStampTable)
		{
			var filteredProcessingRecords = processingRecords.Where(item => item.Value.MessageType == MessageType.Create || item.Value.MessageType == MessageType.Update);
			var filteredCrmQueryResults = crmQueryResults.UpdatedEntityRecords.OfType<Microsoft.Xrm.Sdk.NewOrUpdatedItem>()
				// Only add telemetry for non-initial load entities (timestamp already has been recorded.
				// This will avoid awful timespan values being captured on initial startup
				.Where(filteredResult => timeStampTable.ContainsKey(filteredResult.NewOrUpdatedEntity.LogicalName)
					// Timestamp table stores string dataTokens from CRM in the form of 123456!04/04/2004 04:04:04.04
					? filteredResult.NewOrUpdatedEntity.GetAttributeValue<DateTime>("modifiedon") > DateTime.Parse(timeStampTable[filteredResult.NewOrUpdatedEntity.LogicalName].Split('!')[1])
					: false);

			IEnumerable<TimeTrackingEntry> entries = filteredProcessingRecords.Join(
				// With list
				filteredCrmQueryResults,
				// Keys
				processingRecord => processingRecord.Key,
				crmQueryResult => crmQueryResult.NewOrUpdatedEntity.LogicalName,
				// How to join
				(processingRecord, crmqueryResult) => new TimeTrackingEntry(
					processingRecord.Key, pushedToCache,
					crmqueryResult.NewOrUpdatedEntity.GetAttributeValue<DateTime>("modifiedon"),
					processingRecord.Value.Received));

			entries.ToList().ForEach(traceEntry =>
			{
				ADXTrace.Instance.TraceInfo(TraceCategory.Application,
					string.Format("Overall the '{0}' change took {1} to propagate\n\tMessage to reach portal through Azure: {2}\n\tSleep and Retreival of change from CRM: {3}",
					traceEntry.EntityLogicalName,
					traceEntry.OverallDelta,
					traceEntry.AzureProcessingDelta,
					traceEntry.InvalidationDelta));
			});
		}

		/// <summary>
		/// Handling for invalidating adx metadata cache
		/// </summary>
		private void InvalidateMetadataCache()
		{
			// short circuit exit
			if (!NotificationUpdateManager.Instance.MetadataDirty)
				return;

			NotificationUpdateManager.Instance.MetadataDirty = false;
			this.PostRequest(new[] { NotificationMessageTransformer.Instance.CreateMetadataMessage() }, true);
		}

		/// <summary>
		/// Posts notifications of entity changes
		/// </summary>
		/// <param name="messages">List of Notification messages</param>
		/// <param name="isMetadataChangeMessage">True if it is a metadata change notification.</param>
		/// <param name="isSearchIndexInvalidation">True for search index invlaidation and false for cache invalidation.</param>
		/// <returns></returns>
		private List<string> PostRequest(IEnumerable<PluginMessage> messages, bool isMetadataChangeMessage, bool isSearchIndexInvalidation = false)
		{
			List<string> entitiesWithSuccessfulInvalidation = new List<string>();
			try
			{
				var batchedMessages = messages
					.Where(mesg => mesg != null)
					.GroupBy(mesg => mesg.Target == null ? string.Empty : mesg.Target.LogicalName);

				foreach (var batchedmessage in batchedMessages)
				{
					List<OrganizationServiceCachePluginMessage> batchedPluginMessage = new List<OrganizationServiceCachePluginMessage>();
					var searchInvalidationDatum = new Dictionary<Guid, SearchIndexBuildRequest.SearchIndexInvalidationData>();

					foreach (var changedItem in batchedmessage)
					{
						if (changedItem != null)
						{
							if (changedItem.Target != null)
							{
								ADXTrace.Instance.TraceInfo(TraceCategory.Application, string.Format("Posting Request for message with Entity: {0} and ChangeType: {1}", changedItem.Target.LogicalName, changedItem.MessageName));
							}

							var restartMessage = new ApplicationRestartPortalBusMessage();
							
							// Conversion to OrganizationServiceCachePluginMessage type
							var message = new OrganizationServiceCachePluginMessage();
							message.MessageName = changedItem.MessageName;
							message.RelatedEntities = changedItem.RelatedEntities;
							message.Relationship = changedItem.Relationship;
							message.Target = changedItem.Target;

							if (restartMessage.Validate(changedItem))
							{
								// The restart messages should be processed only once when the message is received from Cache subscription.
								if (!isSearchIndexInvalidation)
								{
									// restart the web application
									var task = restartMessage.InvokeAsync(new OwinContext()).WithCurrentCulture();
									task.GetAwaiter().GetResult();
									SearchIndexBuildRequest.ProcessMessage(message);
								}
							}
							else
							{
								if (!isSearchIndexInvalidation && FeatureCheckHelper.IsFeatureEnabled(FeatureNames.CmsEnabledSearching) && message.Target != null && message.Target.Id != Guid.Empty)
								{
									// Get relevant info for search index invalidation from content map before cache invalidation
									// MUST OCCUR BEFORE CACHE INVALIDATION
									if (!searchInvalidationDatum.ContainsKey(message.Target.Id))
									{
										searchInvalidationDatum.Add(message.Target.Id, GetSearchIndexInvalidationData(message));
									}
								}
								batchedPluginMessage.Add(message);
							}
						}
						else
						{
							//logging
							ADXTrace.Instance.TraceWarning(TraceCategory.Application, string.Format("ChangedItem Record is Null "));
						}
					}
					if (batchedPluginMessage.Count > 0)
					{
						if (isMetadataChangeMessage)
						{
							// Invalidate both search index as well as cache
							try
							{
								InvalidateSearchIndex(batchedPluginMessage, searchInvalidationDatum);
							}
							catch (Exception e)
							{
								// Even if exception occurs, we still need to invalidate cache, hence cathing exception here and logging error.
								ADXTrace.Instance.TraceError(TraceCategory.Exception, e.ToString());
							}
							InvalidateCache(batchedPluginMessage);
						}
						else if (isSearchIndexInvalidation)
						{
							InvalidateSearchIndex(batchedPluginMessage, searchInvalidationDatum);
						}
						else
						{
							// Invalidate cache
							InvalidateCache(batchedPluginMessage);
						}
					}

					entitiesWithSuccessfulInvalidation.Add(batchedmessage.Key);
				}
				return entitiesWithSuccessfulInvalidation;
			}
			catch (Exception e)
			{
				ADXTrace.Instance.TraceError(TraceCategory.Application, e.ToString());
				return entitiesWithSuccessfulInvalidation;
			}
		}

		private void InvalidateCache(List<OrganizationServiceCachePluginMessage> batchedPluginMessage)
		{
			//Set Properties
			var cacheMessage = new OrganizationServiceCacheBatchedPluginMessage();
			cacheMessage.MessageName = batchedPluginMessage[0].MessageName;
			cacheMessage.RelatedEntities = batchedPluginMessage[0].RelatedEntities;
			cacheMessage.Relationship = batchedPluginMessage[0].Relationship;
			cacheMessage.Target = batchedPluginMessage[0].Target;
			cacheMessage.BatchedPluginMessage = batchedPluginMessage;

			var messageName = batchedPluginMessage[0].Target != null
				? batchedPluginMessage[0].Target.LogicalName
				: batchedPluginMessage[0].MessageName;

			ADXTrace.Instance.TraceInfo(TraceCategory.Application, string.Format("Posting Batch Cache Invalidation Request for {0}  with Count {1} ", messageName, batchedPluginMessage.Count));

			retryPolicy.Value.Execute(() => CacheInvalidation.ProcessMessage(cacheMessage));
		}

		private void InvalidateSearchIndex(List<OrganizationServiceCachePluginMessage> batchedPluginMessage, Dictionary<Guid, SearchIndexBuildRequest.SearchIndexInvalidationData> searchInvalidationDatum)
		{
			foreach (var message in batchedPluginMessage)
			{
				if (message.Target == null || SearchMetadataCache.Instance.SearchEnabledEntities.Contains(message.Target.LogicalName))
				{
					SearchIndexBuildRequest.SearchIndexInvalidationData searchInvalidationData = null;

					if (message.Target != null)
					{
						searchInvalidationDatum.TryGetValue(message.Target.Id, out searchInvalidationData);
					}

					retryPolicy.Value.Execute(() => SearchIndexBuildRequest.ProcessMessage(message, searchInvalidationData, CrmChangeTrackingManager.Instance.OrganizationServiceContext));
				}
			}
		}

		/// <summary>
		/// Receives response
		/// </summary>
		/// <param name="request"></param>

		private void MonitorSystemState()
		{
			// Something to see when the last cache was invalidated / change notification observed
			throw new NotImplementedException();
		}

        private SearchIndexBuildRequest.SearchIndexInvalidationData GetSearchIndexInvalidationData(OrganizationServiceCachePluginMessage message)
        {
            if (message.Target == null || string.IsNullOrEmpty(message.Target.LogicalName) || message.Target.Id == Guid.Empty)
            {
                return null;
            }

            var searchIndexInvalidationData  = new SearchIndexBuildRequest.SearchIndexInvalidationData();

            IContentMapProvider contentMapProvider = AdxstudioCrmConfigurationManager.CreateContentMapProvider();
            contentMapProvider.Using(contentMap =>
            {
                if (message.Target.LogicalName == "adx_webpage")
                {
                    WebPageNode webPageNode;

                    if (contentMap.TryGetValue(new EntityReference(message.Target.LogicalName, message.Target.Id), out webPageNode))
                    {
                        searchIndexInvalidationData.PartialUrl = webPageNode.PartialUrl;

                        if (webPageNode.Parent != null)
                        {
                            searchIndexInvalidationData.ParentPage = new EntityReference("adx_webpage", webPageNode.Parent.Id);
                        }

                        if (webPageNode.Website != null)
                        {
                            searchIndexInvalidationData.Website = new EntityReference("adx_website", webPageNode.Website.Id);
                        }

                        if (webPageNode.PublishingState != null)
                        {
                            searchIndexInvalidationData.PublishingState = new EntityReference("adx_publishingstate", webPageNode.PublishingState.Id);
                        }
					}
                }

				if (message.Target.LogicalName == "adx_webpageaccesscontrolrule_webrole")
				{
					WebPageAccessControlRuleToWebRoleNode webAccessControlToWebRoleNode;

					if (contentMap.TryGetValue(new EntityReference(message.Target.LogicalName, message.Target.Id), out webAccessControlToWebRoleNode))
					{
						WebPageAccessControlRuleNode webAccessControlNode = webAccessControlToWebRoleNode.WebPageAccessControlRule;
						if (webAccessControlNode != null && webAccessControlNode.WebPage != null)
						{
							searchIndexInvalidationData.WebPage = new EntityReference("adx_webpage", webAccessControlNode.WebPage.Id);
						}
					}
				}

				if (message.Target.LogicalName == "adx_webpageaccesscontrolrule")
				{
					WebPageAccessControlRuleNode webAccessControlNode;

					if (contentMap.TryGetValue(new EntityReference(message.Target.LogicalName, message.Target.Id), out webAccessControlNode))
					{
						if (webAccessControlNode.WebPage != null)
						{
							searchIndexInvalidationData.WebPage = new EntityReference("adx_webpage", webAccessControlNode.WebPage.Id);
						}
					}
				}

				if (message.Target.LogicalName == "adx_communityforumaccesspermission")
				{
					ForumAccessPermissionNode forumAccessNode;

					if (contentMap.TryGetValue(new EntityReference(message.Target.LogicalName, message.Target.Id), out forumAccessNode))
					{
						if (forumAccessNode.Forum != null)
						{
							searchIndexInvalidationData.Forum = new EntityReference("adx_communityforum", forumAccessNode.Forum.Id);
						}
					}
				}
			});

            return searchIndexInvalidationData;
        }

		private static ResiliencePipeline GetRetryPolicy()
		{
			var retryStrategy = RetryPolicies.Incremental(5, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
			var retryPolicy = RetryPolicies.Create(new EventHubInvalidationErrorDetectionStrategy().IsTransient, retryStrategy);

			return retryPolicy;
		}
    }
}
