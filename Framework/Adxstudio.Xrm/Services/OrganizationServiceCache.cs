/*
  Copyright (c) Microsoft Corporation. All rights reserved.
  Licensed under the MIT License. See License.txt in the project root for license information.
*/

namespace Adxstudio.Xrm.Services
{
	using System;
	using System.Collections.Generic;
	using System.Collections.Specialized;
	using System.Diagnostics;
	using System.Globalization;
	using System.Linq;
	using System.Runtime.Caching;
	using System.Text;
	using System.Web;

	using Microsoft.Xrm.Client;
	using Microsoft.Xrm.Client.Caching;
	using Microsoft.Xrm.Client.Configuration;
	using Microsoft.Xrm.Client.Services;
	using Microsoft.Xrm.Client.Services.Messages;
	using Microsoft.Xrm.Client.Threading;
	using Microsoft.Xrm.Sdk;
	using Microsoft.Xrm.Sdk.Messages;
	using Microsoft.Xrm.Sdk.Metadata;
	using Microsoft.Xrm.Sdk.Query;
	using Microsoft.Crm.Sdk.Messages;
	using Newtonsoft.Json;

	using Adxstudio.Xrm.Core.Flighting;
	using Adxstudio.Xrm.Threading;
	using Adxstudio.Xrm.Caching;
	using Adxstudio.Xrm.Diagnostics.Trace;
	using Adxstudio.Xrm.EventHubBasedInvalidation;
	using Adxstudio.Xrm.Performance;
	using Adxstudio.Xrm.Services.Query;

	/// <summary>
	/// An extended <see cref="Microsoft.Xrm.Client.Services.OrganizationServiceCache"/> that is capable of invalidating relationship changes.
	/// </summary>
	public class OrganizationServiceCache : IOrganizationServiceCache, IInitializable
	{
		private readonly ICacheItemPolicyFactory _cacheItemPolicyFactory;

		// this lock will be used to block the thread which made the cache item dirty, in a case where it is not the first reader after making it dirty.
		private readonly NamedLock sessionIdLock = new NamedLock();

		/// <summary>
		/// The cache region used when interacting with the <see cref="ObjectCache"/>.
		/// </summary>
		public string CacheRegionName { get; private set; }

		/// <summary>
		/// The caching behavior mode.
		/// </summary>
		public OrganizationServiceCacheMode Mode { get; set; }

		/// <summary>
		/// The cache retrieval mode.
		/// </summary>
		public OrganizationServiceCacheReturnMode ReturnMode { get; set; }

		/// <summary>
		/// The underlying cache.
		/// </summary>
		public virtual ObjectCache Cache { get; private set; }

		/// <summary>
		/// The prefix string used for constructing the <see cref="CacheEntryChangeMonitor"/> objects assigned to the cache items.
		/// </summary>
		public virtual string CacheEntryChangeMonitorPrefix { get; private set; }

		/// <summary>
		/// A key value for uniquely distinguishing the connection.
		/// </summary>
		public string ConnectionId { get; private set; }

		public CacheDependencyCalculator CacheDependencyCalculator { get; }

		/// <summary>
		/// Gets or sets the flag determining whether or not to hash the serialized query.
		/// </summary>
		public bool QueryHashingEnabled { get; private set; }

		static OrganizationServiceCache()
		{
			Initialize();
		}

		public OrganizationServiceCache()
			: this(null)
		{
		}

		public OrganizationServiceCache(ObjectCache cache)
			: this(cache, (OrganizationServiceCacheSettings)null)
		{
		}

		public OrganizationServiceCache(ObjectCache cache, CrmConnection connection)
			: this(cache, connection.GetConnectionId())
		{
		}

		public OrganizationServiceCache(ObjectCache cache, string connectionId)
			: this(cache, new OrganizationServiceCacheSettings(connectionId))
		{
		}

		public OrganizationServiceCache(ObjectCache cache, OrganizationServiceCacheSettings settings)
		{
			var cacheSettings = settings ?? new OrganizationServiceCacheSettings();

			Cache = cache ?? MemoryCache.Default;
			Mode = OrganizationServiceCacheMode.LookupAndInsert;
			ReturnMode = OrganizationServiceCacheReturnMode.Cloned;

			ConnectionId = cacheSettings.ConnectionId;
			CacheRegionName = cacheSettings.CacheRegionName;
			QueryHashingEnabled = cacheSettings.QueryHashingEnabled;
			CacheEntryChangeMonitorPrefix = cacheSettings.CacheEntryChangeMonitorPrefix;
			_cacheItemPolicyFactory = cacheSettings.PolicyFactory;
			this.CacheDependencyCalculator = new CacheDependencyCalculator(cacheSettings.CacheEntryChangeMonitorPrefix);
		}

		/// <summary>
		/// Initializes custom settings.
		/// </summary>
		/// <param name="name"></param>
		/// <param name="config"></param>
		public virtual void Initialize(string name, NameValueCollection config)
		{
		}

		/// <summary>
		/// Executes a request against the <see cref="IOrganizationService"/> or retrieves the response from the cache if found.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="request"></param>
		/// <param name="execute"></param>
		/// <param name="selector"></param>
		/// <param name="selectorCacheKey"></param>
		/// <returns></returns>
		public T Execute<T>(OrganizationRequest request, Func<OrganizationRequest, OrganizationResponse> execute, Func<OrganizationResponse, T> selector, string selectorCacheKey)
		{
			return InnerExecute(request, execute, selector, selectorCacheKey);
		}

		/// <summary>
		/// Removes an entity from the cache.
		/// </summary>
		/// <param name="entity"></param>
		public void Remove(Entity entity)
		{
			InvalidateCacheDependencies(this.CacheDependencyCalculator.GetDependencies(entity, false));
		}

		/// <summary>
		/// Removes an entity from the cache.
		/// </summary>
		/// <param name="entity"></param>
		public void Remove(EntityReference entity)
		{
			InvalidateCacheDependencies(this.CacheDependencyCalculator.GetDependencies(entity, false));
		}

		/// <summary>
		/// Removes an entity from the cache.
		/// </summary>
		/// <param name="entityLogicalName"></param>
		/// <param name="id"></param>
		public void Remove(string entityLogicalName, Guid? id)
		{
			InvalidateCacheDependency(this.CacheDependencyCalculator.GetDependency(entityLogicalName));

			if (id != null && id.Value != Guid.Empty)
			{
				InvalidateCacheDependency(this.CacheDependencyCalculator.GetDependency(entityLogicalName, id.Value));
			}
		}

		/// <summary>
		/// Removes a request from the cache.
		/// </summary>
		/// <param name="request"></param>
		public void Remove(OrganizationRequest request)
		{
			var dependencies = this.CacheDependencyCalculator.GetDependenciesForObject(request).ToList();

			InvalidateCacheDependencies(dependencies);
		}

		/// <summary>
		/// Removes a specific cache item.
		/// </summary>
		/// <param name="cacheKey"></param>
		public void Remove(string cacheKey)
		{
			CacheEventSource.Log.CacheRemove(cacheKey, CacheRegionName);

			Cache.Remove(cacheKey, CacheRegionName);
		}

		public void Remove(OrganizationServiceCachePluginMessage message)
		{
			if (message.Category != null)
			{
				ADXTrace.Instance.TraceInfo(TraceCategory.Application, string.Format("Category={0}", message.Category.Value));

				Remove(message.Category.Value);
			}

			if (message.Target != null)
			{
				var entity = message.Target.ToEntityReference();

				ADXTrace.Instance.TraceInfo(TraceCategory.Application, string.Format("LogicalName={0}, Id={1}", EntityNamePrivacy.GetEntityName(entity.LogicalName), entity.Id));

				Remove(entity);
			}

			if (message.RelatedEntities != null)
			{
				var relatedEntities = message.RelatedEntities.ToEntityReferenceCollection();

				foreach (var entity in relatedEntities)
				{
					ADXTrace.Instance.TraceInfo(TraceCategory.Application, string.Format("LogicalName={0}, Id={1}", EntityNamePrivacy.GetEntityName(entity.LogicalName), entity.Id));

					Remove(entity);
				}
			}

			// For cache removals based on metadata invalidation or Publish/PublishAll messages, we
			// want to also invalidate entity data for all systemform and savedquery records, in
			// order to invalidate CrmEntityFormView/Web Forms forms. These entity types don't get
			// the normal Create/Update messages.
			if (IsPublishMessage(message) || IsMetadataMessage(message))
			{
				Remove("systemform", null);
				Remove("savedquery", null);
				Remove("savedqueryvisualization");
				Remove(CacheDependencyCalculator.DependencyMetadataFormat.FormatWith(CacheEntryChangeMonitorPrefix));
			}
		}

		private static bool IsMetadataMessage(OrganizationServiceCachePluginMessage message)
		{
			return message != null
				&& message.Category.HasValue
				&& message.Category.Value.HasFlag(CacheItemCategory.Metadata);
		}

		private static bool IsPublishMessage(OrganizationServiceCachePluginMessage message)
		{
			return message != null
				&& message.MessageName != null
				&& (string.Equals(message.MessageName, "Publish", StringComparison.InvariantCultureIgnoreCase)
					|| string.Equals(message.MessageName, "PublishAll", StringComparison.InvariantCultureIgnoreCase));
		}

		private void Remove(CacheItemCategory category)
		{
			if (category == CacheItemCategory.All)
			{
				CacheEventSource.Log.CacheRemoveAll();

				Cache.RemoveAll();

				return;
			}

			if (category.HasFlag(CacheItemCategory.Metadata))
			{
				Remove(CacheDependencyCalculator.DependencyMetadataFormat.FormatWith(CacheEntryChangeMonitorPrefix));
			}

			if (category.HasFlag(CacheItemCategory.Content))
			{
				Remove(CacheDependencyCalculator.DependencyContentFormat.FormatWith(CacheEntryChangeMonitorPrefix));
			}
		}

		private TResult InnerExecute<TRequest, TResponse, TResult>(TRequest request, Func<TRequest, TResponse> execute, Func<TResponse, TResult> selector, string selectorCacheKey)
		{
			// perform a cached execute or fallback to a regular execute

			var isCachedRequest = CacheDependencyCalculator.IsCachedRequest(request as OrganizationRequest);

			// For content map, we don't want to cache the queries so checking if SkipCache is set to true.
			var cachedOrganizationRequest = request as CachedOrganizationRequest;

			var isStaleDataAllowed = cachedOrganizationRequest?.IsFlagEnabled(RequestFlag.AllowStaleData) == true;
			var fetch = cachedOrganizationRequest?.ToFetch();
			var skipCache = fetch?.SkipCache == true;

			var response = isCachedRequest && !skipCache ? Get(request, execute, selector, selectorCacheKey, isStaleDataAllowed) : InnerExecute(request, execute, selector);

			// Check if we want to skip the cache invalidation. We want to skip this intentionally for some requests like forum count update.
			var bypassCacheInvalidation = cachedOrganizationRequest?.IsFlagEnabled(RequestFlag.ByPassCacheInvalidation) == true;

			if (!isCachedRequest && !bypassCacheInvalidation)
			{
				var dependencies = this.CacheDependencyCalculator.GetDependenciesForObject(request).ToList();
				InvalidateCacheDependencies(dependencies);
			}

			return response;
		}

		private TResult Get<TRequest, TResponse, TResult>(TRequest query, Func<TRequest, TResponse> execute, Func<TResponse, TResult> selector, string selectorCacheKey, bool allowStaleData = false)
		{
			if (Mode == OrganizationServiceCacheMode.LookupAndInsert) return LookupAndInsert(query, execute, selector, selectorCacheKey, allowStaleData);
			if (Mode == OrganizationServiceCacheMode.InsertOnly) return InsertOnly(query, execute, selector, selectorCacheKey);
			if (Mode == OrganizationServiceCacheMode.Disabled) return Disabled(query, execute, selector, selectorCacheKey);

			return InnerExecute(query, execute, selector);
		}

		private TResult LookupAndInsert<TRequest, TResponse, TResult>(TRequest query, Func<TRequest, TResponse> execute, Func<TResponse, TResult> selector, string selectorCacheKey, bool allowStaleData = false)
		{
			int cacheMissedMetricValue;

			string queryText;

			var stopwatch = Stopwatch.StartNew();
			var cacheKey = GetCacheKey(query, selectorCacheKey, out queryText);
			var response = GetCachedResult(query, execute, selector, cacheKey, out cacheMissedMetricValue);

			var cacheItemDetail = Cache.GetCacheItemDetail(cacheKey, CacheRegionName);

			if (cacheItemDetail != null)
			{
				// If the item is marked dirty, fetch the latest changes from CRM.
				if (cacheItemDetail.CacheItemStatus == CacheItemStatus.Dirty)
				{
					// Set the cache item status to BeingProcessed, before fetching changes from CRM.
					if (cacheItemDetail.TrySetCacheItemStatus(CacheItemStatus.BeingProcessed))
					{
						using (sessionIdLock.Lock(cacheItemDetail.SessionId))
						{
							// Fetch the latest chagnes from CRM and update the cache item.
							cacheMissedMetricValue = 1;
							response = InnerExecute(query, execute, selector);
							Insert(cacheKey, query, response);
						}

						// Set the cache item status to update after it is updated in the cache.
						// Since inserting the latest data in the cache, would perform a cache.Set operation on the actual cache key, it would refresh the cacheItemDetail and cacheItemTelemetry items in the cache.
						// So we can skip the step to update the CacheItemStatus, since it is already updated to current.
					}
					else
					{
						// If thread was not able to flip the CacheItemStatus flag, it will access the stale data. Update the stale access count for the given key.
						ADXTrace.Instance.TraceInfo(TraceCategory.CacheInfra, string.Format("Cache hit with stale Data. Could not flip CacheItemStatus, CacheKey={0}, IsStaleAllowed={1}", cacheKey, cacheItemDetail?.IsStaleDataAllowed == true));
						Cache.IncrementStaleAccessCount(cacheKey, CacheRegionName);
					}
				}
				// The below logic is handle those scenarios where the reader is the same thread which marked the cache item as dirty.
				// In such case, we would not want the reader to return the stale data.
				// Ideally it should be this reader that should update the cache item. But since there is no way to ensure that, we should keep it blocked until the data in the cache is refreshed.
				// To identify that this is the same thread which marked the cache item dirty, we are using session id (which is stored in CacheItemDetail)
				else if (cacheItemDetail.CacheItemStatus == CacheItemStatus.BeingProcessed
					&& !string.IsNullOrEmpty(cacheItemDetail.SessionId)
					&& cacheItemDetail.SessionId.Equals(GetSessionId()))
				{
					using (sessionIdLock.Lock(cacheItemDetail.SessionId))
					{
						// Retrieve the latest response from cache.
						ADXTrace.Instance.TraceInfo(TraceCategory.CacheInfra, string.Format("Cache hit - Session Lock, CacheKey={0}, IsStaleAllowed={1}", cacheKey, cacheItemDetail?.IsStaleDataAllowed == true));
						response = GetCachedResult(query, execute, selector, cacheKey, out cacheMissedMetricValue);
					}
				}
				else if (cacheItemDetail.CacheItemStatus == CacheItemStatus.BeingProcessed)
				{
					// Update the stale access account for the given cache key.
					ADXTrace.Instance.TraceInfo(TraceCategory.CacheInfra, string.Format("Cache hit with stale Data, CacheKey={0}, IsStaleAllowed={1}", cacheKey, cacheItemDetail?.IsStaleDataAllowed == true));
					Cache.IncrementStaleAccessCount(cacheKey, CacheRegionName);
				}

				// Check if we want to allow stale data for this request or not.
				if (allowStaleData)
				{
					// Retrieve the latest cacheItemDetail from the cache, in case it is refresed
					cacheItemDetail = Cache.GetCacheItemDetail(cacheKey, CacheRegionName);

					if (cacheItemDetail != null)
					{
						cacheItemDetail.IsStaleDataAllowed = true;
					}
				}
			}

			stopwatch.Stop();

			if (cacheMissedMetricValue == 0)
			{
				Cache.IncrementAccessCount(cacheKey, CacheRegionName);

				var request = query as OrganizationRequest;

				if (request != null)
				{
					ServicesEventSource.Log.OrganizationRequest(request, stopwatch.ElapsedMilliseconds, true);
				}
			}
			else
			{
				// HttpContext.Current is null in case of call out of request
				var isAuthenticated = HttpContext.Current != null && HttpContext.Current.Request.IsAuthenticated;
				ADXTrace.Instance.TraceInfo(TraceCategory.CacheInfra, string.Format("Cache miss, Query={0}, IsAuthenticated={1}, IsStaleAllowed={2}", queryText, isAuthenticated, cacheItemDetail?.IsStaleDataAllowed == true));
			}

			return this.ReturnMode == OrganizationServiceCacheReturnMode.Cloned ? this.InternalCloneResponse(response) : response;
		}

		/// <summary>
		/// Retrieves result from Cache if available else fetches it from CRM and inserts it into cache before returning.
		/// </summary>
		/// <typeparam name="TRequest"> The TRequest</typeparam>
		/// <typeparam name="TResponse">The TResponse</typeparam>
		/// <typeparam name="TResult">The TResult</typeparam>
		/// <param name="query">The Request</param>
		/// <param name="execute">The execute action</param>
		/// <param name="selector">The Selector action</param>
		/// <param name="cacheKey">The cache key</param>
		/// <param name="cacheMissedMetricValue">Indicates if there was a cache miss or cache hit. If value is 0, it is a cache hit and if value is 1, it's a miss.</param>
		/// <returns>Returns the OrganizationResponse</returns>
		private TResult GetCachedResult<TRequest, TResponse, TResult>(TRequest query, Func<TRequest, TResponse> execute, Func<TResponse, TResult> selector, string cacheKey, out int cacheMissedMetricValue)
		{
			bool cacheMiss = false;

			var response = Cache.Get(cacheKey,
				cache =>
				{
					cacheMiss = true;
					ADXTrace.Instance.TraceInfo(TraceCategory.Application, string.Format("{0}", cacheKey));
					return InnerExecute(query, execute, selector);
				},
				(cache, result) => Insert(cacheKey, query, result),
				CacheRegionName);

			cacheMissedMetricValue = cacheMiss ? 1 : 0;

			return response;
		}

		private TResult InsertOnly<TRequest, TResponse, TResult>(TRequest query, Func<TRequest, TResponse> execute, Func<TResponse, TResult> selector, string selectorCacheKey)
		{
			string queryText;
			var cacheKey = GetCacheKey(query, selectorCacheKey, out queryText);
			var result = default(TResult);

			LockManager.Lock(
				cacheKey,
				() =>
				{
					ADXTrace.Instance.TraceInfo(TraceCategory.Application, string.Format("{0}", cacheKey));

					result = InnerExecute(query, execute, selector);

					Insert(cacheKey, query, result);
				});

			return result;
		}

		private TResult Disabled<TRequest, TResponse, TResult>(TRequest query, Func<TRequest, TResponse> execute, Func<TResponse, TResult> selector, string selectorCacheKey)
		{
			string queryText;
			var cacheKey = GetCacheKey(query, selectorCacheKey, out queryText);

			ADXTrace.Instance.TraceInfo(TraceCategory.Application, string.Format("{0}", cacheKey));

			return InnerExecute(query, execute, selector);
		}

		private static TResult InnerExecute<TRequest, TResponse, TResult>(TRequest query, Func<TRequest, TResponse> execute, Func<TResponse, TResult> selector)
		{
			return selector(execute(query));
		}

		public virtual void Insert(string key, object query, object result)
		{
			var cachePolicy = GetCachePolicy(query, result);
			this.Insert(key, query, result, cachePolicy);
		}

		private void Insert(string key, object query, object result, CacheItemPolicy cachePolicy)
		{
			CacheEventSource.Log.CacheInsert(key, CacheRegionName);

			// select the cache entry monitors and add their keys to the cache

			Cache.Insert(key, result, cachePolicy, CacheRegionName);

			var cached = query as CachedOrganizationRequest;

			if (cached != null && cached.Telemetry != null)
			{
				Cache.AddCacheItemTelemetry(key, cached.Telemetry, CacheRegionName);
			}
		}

		/// <summary>
		/// An extensiblity method for retrieving a custom cache policy.
		/// </summary>
		/// <param name="query"></param>
		/// <param name="result"></param>
		/// <param name="cacheItemPolicy"></param>
		/// <returns></returns>
		protected virtual bool TryGetCachePolicy(object query, object result, out CacheItemPolicy cacheItemPolicy)
		{
			cacheItemPolicy = null;
			return false;
		}

		protected CacheItemPolicy GetCachePolicy(object query, object result)
		{
			var cachePolicy = GetBaseCachePolicy();
			var cachedRequest = query as CachedOrganizationRequest;
			var timedExpiration = cachedRequest?.CacheExpiration;
			var skipDependencies = cachedRequest?.IsFlagEnabled(RequestFlag.SkipDependencyCalculation) == true;

			var dependencies = skipDependencies
				? this.CacheDependencyCalculator.GetDependenciesEmpty()
				: this.CacheDependencyCalculator.GetDependenciesForObject(query).Concat(this.CacheDependencyCalculator.GetDependenciesForObject(result)).Distinct().ToList();

			var monitor = Cache.GetChangeMonitor(dependencies, CacheRegionName);

			if (monitor != null)
			{
				cachePolicy.ChangeMonitors.Add(monitor);
			}

			cachePolicy.RemovedCallback += CacheEventSource.Log.OnRemovedCallback;

			if (timedExpiration != null)
			{
				cachePolicy.AbsoluteExpiration = DateTimeOffset.UtcNow + timedExpiration.Value;
			}

			return cachePolicy;
		}

		protected CacheItemPolicy GetBaseCachePolicy()
		{
			return _cacheItemPolicyFactory != null ? _cacheItemPolicyFactory.Create() : new CacheItemPolicy();
		}

		private string GetCacheKey(object query, string selectorCacheKey, out string queryText)
		{
			if (query is CachedOrganizationRequest) return this.GetCacheKey((query as CachedOrganizationRequest).Request, selectorCacheKey, out queryText);
			if (query is RetrieveSingleRequest) return this.GetCacheKey((query as RetrieveSingleRequest).Request, selectorCacheKey, out queryText);
			if (query is FetchMultipleRequest) return this.GetCacheKey((query as FetchMultipleRequest).Request, selectorCacheKey, out queryText);

			var sb = new StringBuilder(128);

			var text = queryText = this.GetCacheKey(query) ?? Serialize(query);
			var connection = !string.IsNullOrWhiteSpace(ConnectionId)
				? ":ConnectionId={0}".FormatWith(QueryHashingEnabled ? ConnectionId.GetHashCode().ToString(CultureInfo.InvariantCulture) : ConnectionId)
				: null;
			var code = QueryHashingEnabled ? text.GetHashCode().ToString(CultureInfo.InvariantCulture) : text;
			var selector = !string.IsNullOrEmpty(selectorCacheKey)
				? ":Selector={0}".FormatWith(selectorCacheKey)
				: null;

			return sb.AppendFormat("{0}{1}:Query={2}{3}",
				GetType().FullName,
				connection,
				code,
				selector).ToString();
		}

		protected string GetCacheKey(object request)
		{
			if (request == null) return null;

			// use the explicit key from the KeyedRequest

			if (request is KeyedRequest) return (request as KeyedRequest).Key;

			// optimized serializations

			if (request is RetrieveRequest
				&& (request as RetrieveRequest).ColumnSet.AllColumns
				&& (request as RetrieveRequest).RelatedEntitiesQuery == null)
			{
				return _serializedRetrieveRequestFormat.FormatWith(
					(request as RetrieveRequest).Target.LogicalName,
					(request as RetrieveRequest).Target.Id);
			}

			if (request is RetrieveRequest
				&& !(request as RetrieveRequest).ColumnSet.AllColumns
				&& (request as RetrieveRequest).ColumnSet.Columns.Count == 0
				&& (request as RetrieveRequest).RelatedEntitiesQuery != null
				&& (request as RetrieveRequest).RelatedEntitiesQuery.Count == 1
				&& IsBasicQueryExpression((request as RetrieveRequest).RelatedEntitiesQuery.First().Value))
			{
				var relnQuery = (request as RetrieveRequest).RelatedEntitiesQuery.First();
				var query = relnQuery.Value as QueryExpression;

				return _serializedRetrieveRequestWithRelatedQueryFormat.FormatWith(
					(request as RetrieveRequest).Target.LogicalName,
					(request as RetrieveRequest).Target.Id,
					relnQuery.Key.SchemaName,
					relnQuery.Key.PrimaryEntityRole != null ? ((int)relnQuery.Key.PrimaryEntityRole.Value).ToString() : "null",
					query.EntityName);
			}

			if (request is RetrieveAllEntitiesRequest)
			{
				return _serializedRetrieveAllEntitiesRequestFormat.FormatWith(
					(int)(request as RetrieveAllEntitiesRequest).EntityFilters,
					(request as RetrieveAllEntitiesRequest).RetrieveAsIfPublished);
			}

			if (request is RetrieveEntityRequest)
			{
				return _serializedRetrieveEntityRequestFormat.FormatWith(
					(int)(request as RetrieveEntityRequest).EntityFilters,
					(request as RetrieveEntityRequest).LogicalName,
					(request as RetrieveEntityRequest).RetrieveAsIfPublished);
			}

			var rmr = request as RetrieveMultipleRequest;

			if (rmr != null)
			{
				if (IsBasicQueryExpression(rmr.Query))
				{
					var query = rmr.Query as QueryExpression;

					return _serializedRetrieveMultipleRequestFormat.FormatWith(
						query.EntityName,
						query.Distinct);
				}

				var fe = rmr.Query as FetchExpression;

				if (fe != null)
				{
					return _serializedRetrieveMultipleFetchExpressionFormat.FormatWith(fe.Query);
				}
			}

			if (request is RetrieveRelationshipRequest)
			{
				return _serializedRetrieveRelationshipRequestFormat.FormatWith(
					(request as RetrieveRelationshipRequest).Name,
					(request as RetrieveRelationshipRequest).RetrieveAsIfPublished);
			}

			if (request is RetrieveLocLabelsRequest)
			{
				return _serializedRetrieveLocLabelsRequestFormat.FormatWith(
					(request as RetrieveLocLabelsRequest).EntityMoniker.LogicalName,
					(request as RetrieveLocLabelsRequest).EntityMoniker.Id,
					(request as RetrieveLocLabelsRequest).AttributeName,
					(request as RetrieveLocLabelsRequest).IncludeUnpublished);
			}

			if (request is RetrieveProvisionedLanguagesRequest)
			{
				return _serializedRetrieveProvisionedLanguagesRequestFormat;
			}

			return null;
		}

		private static bool IsBasicQueryExpression(QueryBase query)
		{
			var qe = query as QueryExpression;
			if (qe == null) return false;

			return qe.ColumnSet.AllColumns
				&& qe.Criteria.Conditions.Count == 0
				&& qe.Criteria.Filters.Count == 0
				&& qe.LinkEntities.Count == 0
				&& qe.Orders.Count == 0
				&& qe.PageInfo.Count == 0
				&& qe.PageInfo.PageNumber == 0
				&& qe.PageInfo.PagingCookie == null
				&& qe.PageInfo.ReturnTotalRecordCount == false;
		}

		private static string _serializedRetrieveRequestFormat;
		private static string _serializedRetrieveRequestWithRelatedQueryFormat;
		private static string _serializedRetrieveAllEntitiesRequestFormat;
		private static string _serializedRetrieveEntityRequestFormat;
		private static string _serializedRetrieveMultipleRequestFormat;
		private static string _serializedRetrieveMultipleFetchExpressionFormat;
		private static string _serializedRetrieveRelationshipRequestFormat;
		private static string _serializedRetrieveLocLabelsRequestFormat;
		private static string _serializedRetrieveProvisionedLanguagesRequestFormat;

		private static void Initialize()
		{
			const string fetchXml = "__fetchXml__";
			const string schemaName = "__schemaName__";
			const string entityName = "__entityName__";
			const string logicalName = "__logicalName__";
			const string attributeName = "__attributeName__";
			const EntityFilters filters = EntityFilters.All;
			var id = new Guid("ffffffff-ffff-ffff-ffff-ffffffffffff");

			var retrieveRequest = new RetrieveRequest { ColumnSet = new ColumnSet(true), Target = new EntityReference(logicalName, id) };
			_serializedRetrieveRequestFormat = SerializeForFormat(retrieveRequest)
				.Replace(logicalName, @"{0}")
				.Replace(id.ToString(), @"{1}");

			var relationship = new Relationship(schemaName) { PrimaryEntityRole = EntityRole.Referencing };
			var query = new RelationshipQueryCollection { { relationship, new QueryExpression(entityName) { ColumnSet = new ColumnSet(true) } } };
			var retrieveRequestWithRelatedQuery = new RetrieveRequest { ColumnSet = new ColumnSet(), Target = new EntityReference(logicalName, id), RelatedEntitiesQuery = query };
			_serializedRetrieveRequestWithRelatedQueryFormat = SerializeForFormat(retrieveRequestWithRelatedQuery)
				.Replace(logicalName, @"{0}")
				.Replace(id.ToString(), @"{1}")
				.Replace(schemaName, @"{2}")
				.Replace(@"""PrimaryEntityRole"":0", @"""PrimaryEntityRole"":{3}")
				.Replace(entityName, @"{4}");

			var retrieveAllEntitiesRequest = new RetrieveAllEntitiesRequest { EntityFilters = filters, RetrieveAsIfPublished = true };
			_serializedRetrieveAllEntitiesRequestFormat = SerializeForFormat(retrieveAllEntitiesRequest)
				.Replace(((int)filters).ToString(), @"{0}")
				.Replace("true", @"{1}");

			var retrieveEntityRequest = new RetrieveEntityRequest { EntityFilters = filters, LogicalName = logicalName, RetrieveAsIfPublished = true };
			_serializedRetrieveEntityRequestFormat = SerializeForFormat(retrieveEntityRequest)
				.Replace(((int)filters).ToString(), @"{0}")
				.Replace(logicalName, @"{1}")
				.Replace("true", @"{2}");

			var retrieveMultipleRequest = new RetrieveMultipleRequest { Query = new QueryExpression(logicalName) { Distinct = true } };
			_serializedRetrieveMultipleRequestFormat = SerializeForFormat(retrieveMultipleRequest)
				.Replace(logicalName, @"{0}")
				.Replace("true", @"{1}");

			var retrieveMultipleFetchExpressionRequest = new RetrieveMultipleRequest { Query = new FetchExpression(fetchXml) };
			_serializedRetrieveMultipleFetchExpressionFormat = SerializeForFormat(retrieveMultipleFetchExpressionRequest)
				.Replace(fetchXml, @"{0}");

			var retrieveRelationshipRequest = new RetrieveRelationshipRequest { Name = logicalName, RetrieveAsIfPublished = true };
			_serializedRetrieveRelationshipRequestFormat = SerializeForFormat(retrieveRelationshipRequest)
				.Replace(logicalName, @"{0}")
				.Replace("true", @"{1}");

			var retrieveLocLabelsRequest = new RetrieveLocLabelsRequest
			{
				EntityMoniker = new EntityReference(logicalName, id),
				AttributeName = attributeName,
				IncludeUnpublished = true
			};
			_serializedRetrieveLocLabelsRequestFormat =
				SerializeForFormat(retrieveLocLabelsRequest)
					.Replace(logicalName, @"{0}")
					.Replace(id.ToString(), @"{1}")
					.Replace(attributeName, @"{2}")
					.Replace("true", @"{3}");

			var retrieveProvLangsRequest = new RetrieveProvisionedLanguagesRequest();
			_serializedRetrieveProvisionedLanguagesRequestFormat = SerializeForFormat(retrieveProvLangsRequest);
		}

		private static string Serialize(object value)
		{
			using (PerformanceProfiler.Instance.StartMarker(PerformanceMarkerName.Cache, PerformanceMarkerArea.Cms, PerformanceMarkerTagName.SerializeQuery))
			{
				var text = JsonConvert.SerializeObject(value);

				return text;
			}
		}

		private static string SerializeForFormat(object value)
		{
			var text = Serialize(value);

			// escape the {} brackets

			return text.Replace("{", "{{").Replace("}", "}}");
		}

		private void InvalidateCacheDependencies(IEnumerable<string> dependencies)
		{
			foreach (var dependency in dependencies)
			{
				InvalidateCacheDependency(dependency);
			}
		}

		private void InvalidateCacheDependency(string dependency)
		{
			if (!FeatureCheckHelper.IsFeatureEnabled(FeatureNames.PortalAllowStaleData))
			{
				// If Stale data setting is not enabled then do the legacy behaviour of removing the dependency from the cache.
				CacheEventSource.Log.CacheRemove(dependency, CacheRegionName);
				Cache.Remove(dependency);
			}
			else
			{
				// Get the cache items for this dependency and mark those cache items as dirty.
				// To do this we need to enumerate over all the cache item details and which ever cache-item has this dependency mark it dirty.
				// To mark the cache item dirty, set the cacheItemDetail.CacheItemStatus to CacheItemStatus.Dirty.
				// Rest of the logic would go in Lookup and Insert method, we will not have a cache miss but we will need to check if the item is dirty,
				var cacheItems =
					from item in Cache
					let key = item.Key
					let cacheItemDetail = Cache.GetCacheItemDetail(item.Key, CacheRegionName)
					select new { key, cacheItemDetail };

				foreach (var cacheItem in cacheItems)
				{
					if (cacheItem.cacheItemDetail == null)
					{
						continue;
					}

					if (cacheItem.cacheItemDetail.CacheItemStatus == CacheItemStatus.Dirty || cacheItem.cacheItemDetail.CacheItemStatus == CacheItemStatus.BeingProcessed)
					{
						// If the cache item is already marked dirty/BeingProcessed, skip.
						continue;
					}

					if (cacheItem.cacheItemDetail.Policy.ChangeMonitors.Any(item => item.CacheKeys.Contains(dependency)))
					{
						if (!cacheItem.cacheItemDetail.IsStaleDataAllowed)
						{
							// If the stale data is not allowed for the given cache key, remove this item from the cache.
							ADXTrace.Instance.TraceInfo(TraceCategory.CacheInfra, string.Format("Stale data is not allowed for CacheKey = {0}", cacheItem.key));
							CacheEventSource.Log.CacheRemove(cacheItem.key, CacheRegionName);
							Cache.Remove(cacheItem.key);
						}
						// Try setting the cache item status to dirty, if successful:
						// a) store the session id. This session id we will use later to block the thread from returning stale data, if it is the one which marked the cache-item dirty.
						else if (cacheItem.cacheItemDetail.TrySetCacheItemStatus(CacheItemStatus.Dirty))
						{
							ADXTrace.Instance.TraceInfo(TraceCategory.CacheInfra, string.Format("Cache Item is marked dirty, CacheKey = {0}", cacheItem.key));
							cacheItem.cacheItemDetail.SessionId = GetSessionId();

							// Remove the secondary cache item to trigger invalidation of output cache.
							// We must do this otherwise we may get stuck in situation where the output cache is never invalidated, and thus the dirty cache items never get refreshed.
							var outputCacheSecondaryDependencyKey = ObjectCacheOutputCacheProvider.GetSecondaryDependencyKey(cacheItem.key);
							Cache.Remove(outputCacheSecondaryDependencyKey);
						}
					}
				}
			}
		}

		private TResult InternalCloneResponse<TResult>(TResult item)
		{
			using (PerformanceProfiler.Instance.StartMarker(PerformanceMarkerName.Cache, PerformanceMarkerArea.Cms, PerformanceMarkerTagName.CloneResponse))
			{
				return this.CloneResponse(item);
			}
		}

		protected virtual TResult CloneResponse<TResult>(TResult item)
		{
			// clone the responses with potentially mutable data, metadata responses are treated as immutable

			var retrieveResponse = item as RetrieveResponse;
			if (retrieveResponse != null) return (TResult)CloneResponse(retrieveResponse);

			var retrieveMultipleResponse = item as RetrieveMultipleResponse;
			if (retrieveMultipleResponse != null) return (TResult)CloneResponse(retrieveMultipleResponse);

			var retrieveSingleResponse = item as RetrieveSingleResponse;
			if (retrieveSingleResponse != null) return (TResult)CloneResponse(retrieveSingleResponse);

			return item;
		}

		private static object CloneResponse(RetrieveMultipleResponse response)
		{
			var clone = new RetrieveMultipleResponse { ResponseName = response.ResponseName };
			clone["EntityCollection"] = response.EntityCollection.Clone();
			return clone;
		}

		private static object CloneResponse(RetrieveResponse response)
		{
			var clone = new RetrieveResponse { ResponseName = response.ResponseName };
			clone["Entity"] = Microsoft.Xrm.Client.EntityExtensions.Clone(response.Entity);
			return clone;
		}

		private static object CloneResponse(RetrieveSingleResponse response)
		{
			var clone = new RetrieveSingleResponse(response.Request, CloneResponse(response.Response) as RetrieveMultipleResponse);
			return clone;
		}

		/// <summary>
		/// Obtains session id from HttpContext.Current
		/// </summary>
		/// <returns></returns>
		private static string GetSessionId()
		{
			try
			{
				var context = HttpContext.Current;

				if (context == null)
				{
					return string.Empty;
				}

				return context.Session == null
					? string.Empty
					: context.Session.SessionID ?? string.Empty;
			}
			catch
			{
				return string.Empty;
			}
		}
	}
}
