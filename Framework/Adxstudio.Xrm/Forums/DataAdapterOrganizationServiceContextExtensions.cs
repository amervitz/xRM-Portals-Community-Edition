/*
  Copyright (c) Microsoft Corporation. All rights reserved.
  Licensed under the MIT License. See License.txt in the project root for license information.
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using System.Xml.XPath;
using Adxstudio.Xrm.Data;
using Adxstudio.Xrm.Services.Query;
using Adxstudio.Xrm.Tagging;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using Microsoft.Xrm.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Client;
using Adxstudio.Xrm.Services;

namespace Adxstudio.Xrm.Forums
{
	internal static class DataAdapterOrganizationServiceContextExtensions
	{
		public static ForumCounts FetchForumCounts(this OrganizationServiceContext serviceContext, Guid forumId)
		{
			var counts = FetchForumCounts(serviceContext, new[] { forumId });
			ForumCounts count;

			return counts.TryGetValue(forumId, out count) ? count : new ForumCounts(0, 0);
		}

		public static IDictionary<Guid, ForumCounts> FetchForumCounts(this OrganizationServiceContext serviceContext, IEnumerable<Guid> forumIds)
		{
			if (!forumIds.Any())
			{
				return new Dictionary<Guid, ForumCounts>();
			}

			var ids = forumIds.ToArray();

			var fetchXml = XDocument.Parse(@"
				<fetch mapping=""logical"" aggregate=""true"">
					<entity name=""adx_communityforum"">
						<attribute name=""adx_communityforumid"" alias=""id"" groupby=""true""/>
						<filter type=""and""/>
						<link-entity name=""adx_communityforumthread"" from=""adx_forumid"" to=""adx_communityforumid"">
							<attribute name=""adx_communityforumthreadid"" alias=""threadid"" groupby=""true""/>
							<link-entity name=""adx_communityforumpost"" from=""adx_forumthreadid"" to=""adx_communityforumthreadid"">
								<attribute name=""adx_communityforumpostid"" alias=""postcount"" aggregate=""count""/>
							</link-entity>
						</link-entity>
					</entity>
				</fetch>");

			var filter = fetchXml.XPathSelectElement("//entity[@name='adx_communityforum']/filter");

			if (filter == null)
			{
				throw new InvalidOperationException(string.Format("Unable to select {0} element.", "adx_communityforum filter"));
			}

			filter.AddFetchXmlFilterInCondition("adx_communityforumid", ids.Select(id => id.ToString()));

			var fetch = Fetch.Parse(fetchXml.ToString());
			var response = (serviceContext as IOrganizationService).RetrieveMultiple(fetch);

			var results = response.Entities
				.GroupBy(e => (Guid)e.GetAttributeValue<AliasedValue>("id").Value, e => e)
				.Select(forumThreadGrouping => new KeyValuePair<Guid, ForumCounts>(
					forumThreadGrouping.Key,
					new ForumCounts(
						forumThreadGrouping.Count(),
						forumThreadGrouping.Sum(postCounts => postCounts.GetAttributeAliasedValue<int?>("postcount").GetValueOrDefault()))));

			var counts = ids.ToDictionary(id => id, id => new ForumCounts(0, 0));

			foreach (var result in results)
			{
				counts[result.Key] = result.Value;
			}

			return counts;
		}

		public static ForumCounts FetchForumCountsForWebsite(this OrganizationServiceContext serviceContext, Guid websiteId)
		{
			var fetchXml = XDocument.Parse(@"
				<fetch mapping=""logical"" aggregate=""true"">
					<entity name=""adx_communityforum"">
						<attribute name=""adx_communityforumid"" alias=""id"" groupby=""true""/>
						<filter type=""and"">
							<condition attribute=""adx_websiteid"" operator=""eq"" />
						</filter>
						<link-entity name=""adx_communityforumthread"" from=""adx_forumid"" to=""adx_communityforumid"">
							<attribute name=""adx_communityforumthreadid"" alias=""threadid"" groupby=""true""/>
							<link-entity name=""adx_communityforumpost"" from=""adx_forumthreadid"" to=""adx_communityforumthreadid"">
								<attribute name=""adx_communityforumpostid"" alias=""postcount"" aggregate=""count""/>
							</link-entity>
						</link-entity>
					</entity>
				</fetch>");

			var websiteIdCondition = fetchXml.XPathSelectElement("//entity[@name='adx_communityforum']/filter/condition[@attribute='adx_websiteid']");

			if (websiteIdCondition == null)
			{
				throw new InvalidOperationException("Unable to select the adx_websiteid filter condition element.");
			}

			websiteIdCondition.SetAttributeValue("value", websiteId.ToString());

			var fetch = Fetch.Parse(fetchXml.ToString());
			var response = (serviceContext as IOrganizationService).RetrieveMultiple(fetch);

			var forumCounts = response.Entities
				.GroupBy(e => (Guid)e.GetAttributeValue<AliasedValue>("id").Value, e => e)
				.Select(forumThreadGrouping =>
					new ForumCounts(
						forumThreadGrouping.Count(),
						forumThreadGrouping.Sum(postCounts => postCounts.GetAttributeAliasedValue<int?>("postcount").GetValueOrDefault())))
				.ToArray();

			return new ForumCounts(forumCounts.Sum(c => c.ThreadCount), forumCounts.Sum(c => c.PostCount));
		}

		public static ForumCounts FetchForumCountsForWebsiteWithTag(this OrganizationServiceContext serviceContext, Guid websiteId, string tag)
		{
			return FetchForumCountsForWebsiteWithTags(serviceContext, websiteId, new[] { tag });
		}

		public static ForumCounts FetchForumCountsForWebsiteWithTags(this OrganizationServiceContext serviceContext, Guid websiteId, string[] tags)
		{
			var fetchXml = XDocument.Parse(@"
				<fetch mapping=""logical"" aggregate=""true"">
					<entity name=""adx_communityforum"">
						<attribute name=""adx_communityforumid"" alias=""id"" groupby=""true""/>
						<filter type=""and"">
							<condition attribute=""adx_websiteid"" operator=""eq"" />
						</filter>
						<link-entity name=""adx_communityforumthread"" from=""adx_forumid"" to=""adx_communityforumid"">
							<attribute name=""adx_communityforumthreadid"" alias=""threadid"" groupby=""true""/>
							<link-entity name=""adx_communityforumpost"" from=""adx_forumthreadid"" to=""adx_communityforumthreadid"">
								<attribute name=""adx_communityforumpostid"" alias=""postcount"" aggregate=""count""/>
							</link-entity>
							<link-entity name=""adx_communityforumthread_tag"" from=""adx_communityforumthreadid"" to=""adx_communityforumthreadid"">
								<link-entity name=""adx_tag"" from=""adx_tagid"" to=""adx_tagid"">
									<filter type=""and"">
										<condition attribute=""adx_websiteid"" operator=""eq"" />
									</filter>
								</link-entity>
							</link-entity>
						</link-entity>
					</entity>
				</fetch>");

			var websiteIdConditions = fetchXml.XPathSelectElements("//filter/condition[@attribute='adx_websiteid']");

			foreach (var websiteIdCondition in websiteIdConditions)
			{
				websiteIdCondition.SetAttributeValue("value", websiteId.ToString());
			}

			var tagNameFilter = fetchXml.XPathSelectElement("//link-entity[@name='adx_tag']/filter[@type='and']");

			if (tagNameFilter == null)
			{
				throw new InvalidOperationException("Unable to select the tag name filter condition element.");
			}

			foreach (var tag in tags)
			{
				tagNameFilter.AddFetchXmlFilterCondition("adx_name", "eq", tag);
			}

			var fetch = Fetch.Parse(fetchXml.ToString());
			var response = (serviceContext as IOrganizationService).RetrieveMultiple(fetch);

			var forumCounts = response.Entities
				.GroupBy(e => (Guid)e.GetAttributeValue<AliasedValue>("id").Value, e => e)
				.Select(forumThreadGrouping =>
					new ForumCounts(
						forumThreadGrouping.Count(),
						forumThreadGrouping.Sum(postCounts => postCounts.GetAttributeAliasedValue<int?>("postcount").GetValueOrDefault())))
				.ToArray();

			return new ForumCounts(forumCounts.Sum(c => c.ThreadCount), forumCounts.Sum(c => c.PostCount));
		}

		public static IForumInfo FetchForumInfo(this OrganizationServiceContext serviceContext, Guid forumId)
		{
			var infos = FetchForumInfos(serviceContext, new[] { forumId });
			IForumInfo info;

			return infos.TryGetValue(forumId, out info) ? info : new UnknownForumInfo();
		}
		
		public static IDictionary<Guid, IForumInfo> FetchForumInfos(this OrganizationServiceContext serviceContext, IEnumerable<Guid> forumIds)
		{
			if (!forumIds.Any())
			{
				return new Dictionary<Guid, IForumInfo>();
			}

			var ids = forumIds.ToArray();

			var fetchXml = XDocument.Parse(@"
				<fetch mapping=""logical"">
					<entity name=""adx_communityforum"">
						<filter type=""and"">
						</filter>
						<link-entity name=""adx_communityforumpost"" from=""adx_communityforumpostid"" to=""adx_lastpostid"" alias=""lastpost"">
							<attribute name=""adx_communityforumpostid"" />
							<attribute name=""adx_date"" />
							<link-entity link-type=""outer"" name=""contact"" from=""contactid"" to=""adx_authorid"" alias=""lastpostauthor"">
								<attribute name=""contactid"" />
								<attribute name=""fullname"" />
								<attribute name=""emailaddress1"" />
							</link-entity>
						</link-entity>
					</entity>
				</fetch>");

			var filter = fetchXml.XPathSelectElement("//entity[@name='adx_communityforum']/filter");

			if (filter == null)
			{
				throw new InvalidOperationException(string.Format("Unable to select {0} element.", "adx_communityforum filter"));
			}

			filter.AddFetchXmlFilterInCondition("adx_communityforumid", ids.Select(id => id.ToString()));

			var fetch = Fetch.Parse(fetchXml.ToString());
			var forumpostLink = fetch.Entity.Links.FirstOrDefault(l => l.Name.Equals("adx_communityforumpost"));
			if (forumpostLink != null)
			{
				forumpostLink.IsUnique = true;

				var contactLink = forumpostLink.Links.FirstOrDefault(l => l.Name.Equals("contact"));
				if (contactLink != null)
				{
					contactLink.IsUnique = true;
				}
			}

			var response = (serviceContext as IOrganizationService).RetrieveMultiple(fetch);

			return ids.ToDictionary(id => id, id =>
			{
				IForumInfo unknownInfo = new UnknownForumInfo();

				var entity = response.Entities.FirstOrDefault(e => e.Id == id);

				if (entity == null)
				{
					return unknownInfo;
				}

				var latestPostAuthor = GetForumAuthor("lastpostauthor", entity);
				var latestPostInfo = GetForumPostInfo("lastpost", entity, latestPostAuthor);

				return new ForumInfo(latestPostInfo);
			});
		}

		public static int FetchAuthorForumPostCount(this OrganizationServiceContext serviceContext, Guid authorId, Guid websiteId)
		{
			var fetchXml = XDocument.Parse(@"
				<fetch mapping=""logical"" aggregate=""true"">
					<entity name=""adx_communityforumpost"">
						<attribute name=""adx_communityforumpostid"" alias=""forumpostcount"" aggregate=""countcolumn""/>
						<link-entity name=""contact"" from=""contactid"" to=""adx_authorid"">
							<filter type=""and"">
								<condition attribute=""contactid"" operator=""eq"" />
							</filter>
						</link-entity>
						<link-entity name=""adx_communityforumthread"" from=""adx_communityforumthreadid"" to=""adx_forumthreadid"">
							<link-entity name=""adx_communityforum"" from=""adx_communityforumid"" to=""adx_forumid"">
								<filter type=""and"">
									<condition attribute=""adx_websiteid"" operator=""eq"" />
								</filter>
							</link-entity>
						</link-entity>
					</entity>
				</fetch>");

			var contactIdConditions = fetchXml.XPathSelectElements("//filter/condition[@attribute='contactid']");

			foreach (var condition in contactIdConditions)
			{
				condition.SetAttributeValue("value", authorId.ToString());
			}

			var websiteIdConditions = fetchXml.XPathSelectElements("//filter/condition[@attribute='adx_websiteid']");

			foreach (var condition in websiteIdConditions)
			{
				condition.SetAttributeValue("value", websiteId.ToString());
			}

			var fetch = Fetch.Parse(fetchXml.ToString());
			var contactLink = fetch.Entity.Links.FirstOrDefault(l => l.Name.Equals("contact"));
			if (contactLink != null)
			{
				contactLink.IsUnique = true;
			}

			var entity = serviceContext.RetrieveSingle(fetch);

			var count = (entity != null) ? entity.GetAttributeAliasedValue<int?>("forumpostcount") ?? 0 : 0;

			return count;
		}

		public static IForumPostInfo FetchForumPostInfo(this OrganizationServiceContext serviceContext, Guid forumPostId, Guid websiteId, BlobContainerClient cloudStorageContainer = null)
		{
			var infos = FetchForumPostInfos(serviceContext, new[] { forumPostId }, websiteId, cloudStorageContainer);

			IForumPostInfo info;
			return infos.TryGetValue(forumPostId, out info) ? info : new UnknownForumPostInfo();
		}

		public static IDictionary<Guid, IForumPostInfo> FetchForumPostInfos(this OrganizationServiceContext serviceContext, IEnumerable<Guid> forumPostIds, Guid websiteId, BlobContainerClient cloudStorageContainer = null)
		{
			if (!forumPostIds.Any())
			{
				return new Dictionary<Guid, IForumPostInfo>();
			}

			var ids = forumPostIds.ToArray();

			var fetchXml = XDocument.Parse(@"
				<fetch mapping=""logical"">
					<entity name=""adx_communityforumpost"">
						<filter type=""and"">
						</filter>
						<attribute name=""adx_date"" />
						<link-entity link-type=""outer"" name=""contact"" from=""contactid"" to=""adx_authorid"" alias=""author"">
							<attribute name=""contactid"" />
							<attribute name=""fullname"" />
							<attribute name=""emailaddress1"" />
						</link-entity>
						<link-entity link-type=""outer"" name=""annotation"" from=""objectid"" to=""adx_communityforumpostid"" alias=""attachment"">
							<attribute name=""annotationid"" />
							<attribute name=""mimetype"" />
							<attribute name=""filename"" />
							<attribute name=""filesize"" />
						</link-entity>
						<link-entity link-type=""outer"" name=""adx_communityforumthread"" from=""adx_communityforumthreadid"" to=""adx_forumthreadid"" alias=""thread"">
							<attribute name=""adx_communityforumthreadid"" />
						</link-entity>
					</entity>
				</fetch>");

			var filter = fetchXml.Descendants("filter").First();

			filter.AddFetchXmlFilterInCondition("adx_communityforumpostid", ids.Select(id => id.ToString()));

			var fetch = Fetch.Parse(fetchXml.ToString());
			var contactLink = fetch.Entity.Links.FirstOrDefault(l => l.Name.Equals("contact"));
			if (contactLink != null)
			{
				contactLink.IsUnique = true;
			}

			var response = (serviceContext as IOrganizationService).RetrieveMultiple(fetch);
			
			return ids.ToDictionary(id => id, id =>
			{
				IForumPostInfo unknownInfo = new UnknownForumPostInfo();

				var entities = response.Entities.Where(e => e.Id == id).ToArray();

				if (!entities.Any())
				{
					return unknownInfo;
				}

				var attachmentInfo = entities.Select(e =>
				{
					var annotationId = e.GetAttributeAliasedValue<Guid?>("attachment.annotationid");
					var filename = e.GetAttributeAliasedValue<string>("attachment.filename");
					var mimetype = e.GetAttributeAliasedValue<string>("attachment.mimetype");
					var filesize = e.GetAttributeAliasedValue<int?>("attachment.filesize").GetValueOrDefault();

					if (annotationId == null || string.IsNullOrEmpty(filename))
					{
						return null;
					}

					return new NoteAttachmentInfo(new EntityReference("annotation", annotationId.Value), filename, mimetype, filesize, websiteId, cloudStorageContainer);
				}).Where(info => info != null);

				var entity = entities.First();

				var author = GetForumAuthor("author", entity);

				var thread = new EntityReference(("adx_communityforumthread"),
					entity.GetAttributeAliasedValue<Guid>("thread.adx_communityforumthreadid"));

				return new ForumPostInfo(
					new EntityReference("adx_communityforumpost", entity.Id),
					author,
					entity.GetAttributeAliasedValue<DateTime?>("adx_date").GetValueOrDefault(unknownInfo.PostedOn),
					attachmentInfo,
					thread);
			});
		}

		public static IForumThreadInfo FetchForumThreadInfo(this OrganizationServiceContext serviceContext, Guid forumThreadId, Guid websiteId)
		{
			var infos = FetchForumThreadInfos(serviceContext, new[] { forumThreadId }, websiteId);

			IForumThreadInfo info;
			return infos.TryGetValue(forumThreadId, out info) ? info : new UnknownForumThreadInfo();
		}

		public static IDictionary<Guid, IForumThreadInfo> FetchForumThreadInfos(this OrganizationServiceContext serviceContext, IEnumerable<Guid> forumThreadIds, Guid websiteId)
		{
			if (!forumThreadIds.Any())
			{
				return new Dictionary<Guid, IForumThreadInfo>();
			}

			var ids = forumThreadIds.ToArray();

			var fetchXml = XDocument.Parse(@"
				<fetch mapping=""logical"">
					<entity name=""adx_communityforumthread"">
						<filter type=""and"">
						</filter>
						<link-entity name=""adx_communityforumpost"" from=""adx_communityforumpostid"" to=""adx_firstpostid"" alias=""firstpost"">
							<attribute name=""adx_communityforumpostid"" />
							<attribute name=""adx_date"" />
							<link-entity link-type=""outer"" name=""contact"" from=""contactid"" to=""adx_authorid"" alias=""firstpostauthor"">
								<attribute name=""contactid"" />
								<attribute name=""fullname"" />
								<attribute name=""emailaddress1"" />
							</link-entity>
						</link-entity>
						<link-entity name=""adx_communityforumpost"" from=""adx_communityforumpostid"" to=""adx_lastpostid"" alias=""lastpost"">
							<attribute name=""adx_communityforumpostid"" />
							<attribute name=""adx_date"" />
							<link-entity link-type=""outer"" name=""contact"" from=""contactid"" to=""adx_authorid"" alias=""lastpostauthor"">
								<attribute name=""contactid"" />
								<attribute name=""fullname"" />
								<attribute name=""emailaddress1"" />
							</link-entity>
						</link-entity>
						<link-entity link-type=""outer"" name=""adx_forumthreadtype"" from=""adx_forumthreadtypeid"" to=""adx_typeid"" alias=""threadtype"">
							<attribute name=""adx_forumthreadtypeid"" />
							<attribute name=""adx_name"" />
							<attribute name=""adx_allowsvoting"" />
							<attribute name=""adx_displayorder"" />
							<attribute name=""adx_isdefault"" />
							<attribute name=""adx_requiresanswer"" />
						</link-entity>
						<link-entity link-type=""outer"" name=""adx_communityforumthread_tag"" from=""adx_communityforumthreadid"" to=""adx_communityforumthreadid"">
							<link-entity link-type=""outer"" name=""adx_tag"" from=""adx_tagid"" to=""adx_tagid"" alias=""tag"">
								<attribute name=""adx_name"" />
								<filter type=""and"">
									<condition attribute=""adx_websiteid"" operator=""eq"" />
								</filter>
							</link-entity>
						</link-entity>
					</entity>
				</fetch>");

			var threadFilter = fetchXml.XPathSelectElement("//entity[@name='adx_communityforumthread']/filter");

			if (threadFilter == null)
			{
				throw new InvalidOperationException(string.Format("Unable to select {0} element.", "adx_communityforumthreadid filter condition"));
			}

			threadFilter.AddFetchXmlFilterInCondition("adx_communityforumthreadid", ids.Select(id => id.ToString()));

			var websiteConditions = fetchXml.XPathSelectElements("//filter/condition[@attribute='adx_websiteid']");

			foreach (var websiteCondition in websiteConditions)
			{
				websiteCondition.SetAttributeValue("value", websiteId.ToString());
			}

			var fetch = Fetch.Parse(fetchXml.ToString());
			var threadTypeLink = fetch.Entity.Links.FirstOrDefault(l => l.Name.Equals("adx_forumthreadtype"));
			if (threadTypeLink != null)
			{
				threadTypeLink.IsUnique = true;
			}

			var response = (serviceContext as IOrganizationService).RetrieveMultiple(fetch);

			return ids.ToDictionary(id => id, id =>
			{
				IForumThreadInfo unknownInfo = new UnknownForumThreadInfo();

				var entities = response.Entities.Where(e => e.Id == id).ToArray();

				if (!entities.Any())
				{
					return unknownInfo;
				}

				var entity = entities.First();

				var author = GetForumAuthor("firstpostauthor", entity);
				var postedOn = entity.GetAttributeAliasedValue<DateTime?>("firstpost.adx_date").GetValueOrDefault(unknownInfo.PostedOn);
				var threadType = GetForumThreadType("threadtype", entity);
				var latestPostAuthor = GetForumAuthor("lastpostauthor", entity);
				var latestPostInfo = GetForumPostInfo("lastpost", entity, latestPostAuthor);
				var tags = GetForumThreadTags("tag", entities);

				return new ForumThreadInfo(author, postedOn, threadType, tags, latestPostInfo);
			});
		}

		public static int FetchForumThreadPostCount(this OrganizationServiceContext serviceContext, Guid forumThreadId)
		{
			var counts = FetchForumThreadPostCounts(serviceContext, new[] { forumThreadId });
			int count;

			return counts.TryGetValue(forumThreadId, out count) ? count : 0;
		}

		public static IDictionary<Guid, int> FetchForumThreadPostCounts(this OrganizationServiceContext serviceContext, IEnumerable<Guid> forumThreadIds)
		{
			return serviceContext.FetchCounts(
				"adx_communityforumpost",
				"adx_communityforumpostid",
				"adx_communityforumthread",
				"adx_communityforumthreadid",
				"adx_forumthreadid",
				forumThreadIds);
		}

		public static IEnumerable<ITagInfo> FetchForumThreadTagInfo(this OrganizationServiceContext serviceContext, Guid forumId)
		{
			var fetchXml = XDocument.Parse(@"
				<fetch mapping=""logical"" aggregate=""true"" distinct=""false"">
					<entity name=""adx_communityforumthread_tag"">
						<attribute name=""adx_communityforumthreadid"" alias=""count"" aggregate=""count"" />
						<link-entity name=""adx_tag"" from=""adx_tagid"" to=""adx_tagid"">
							<attribute name=""adx_name"" groupby=""true"" alias=""tag"" />
						</link-entity>
						<link-entity name=""adx_communityforumthread"" from=""adx_communityforumthreadid"" to=""adx_communityforumthreadid"">
							<filter type=""and"">
								<condition attribute=""adx_forumid"" operator=""eq"" />
							</filter>
						</link-entity>
					</entity>
				</fetch>");

			var forumIdCondition = fetchXml.XPathSelectElement("//link-entity[@name='adx_communityforumthread']/filter/condition[@attribute='adx_forumid']");

			if (forumIdCondition == null)
			{
				throw new InvalidOperationException(string.Format("Unable to select {0} element.", "adx_forumid filter condition"));
			}

			forumIdCondition.SetAttributeValue("value", forumId.ToString());

			var fetch = Fetch.Parse(fetchXml.ToString());
			var threadLink = fetch.Entity.Links.FirstOrDefault(l => l.Name.Equals("adx_communityforumthread"));
			if (threadLink != null)
			{
				threadLink.IsUnique = true;
			}

			var response = (serviceContext as IOrganizationService).RetrieveMultiple(fetch);

			return response.Entities.Select(e =>
			{
				var tag = e.GetAttributeAliasedValue<string>("tag");
				var count = e.GetAttributeAliasedValue<int?>("count").GetValueOrDefault();

				return new ForumThreadWeightedTagInfo(tag, count);
			}).ToArray();
		}

		public static IEnumerable<ITagInfo> FetchForumThreadTagInfoForWebsite(this OrganizationServiceContext serviceContext, Guid websiteId)
		{
			var fetchXml = XDocument.Parse(@"
				<fetch mapping=""logical"" aggregate=""true"" distinct=""false"">
					<entity name=""adx_communityforumthread_tag"">
						<attribute name=""adx_communityforumthreadid"" alias=""count"" aggregate=""count"" />
						<link-entity name=""adx_tag"" from=""adx_tagid"" to=""adx_tagid"">
							<attribute name=""adx_name"" groupby=""true"" alias=""tag"" />
						</link-entity>
						<link-entity name=""adx_communityforumthread"" from=""adx_communityforumthreadid"" to=""adx_communityforumthreadid"">
							<link-entity name=""adx_communityforum"" from=""adx_communityforumid"" to=""adx_forumid"">
								<filter type=""and"">
									<condition attribute=""adx_websiteid"" operator=""eq"" />
								</filter>
							</link-entity>
						</link-entity>
					</entity>
				</fetch>");

			var websiteIdCondition = fetchXml.XPathSelectElement("//link-entity[@name='adx_communityforum']/filter/condition[@attribute='adx_websiteid']");

			if (websiteIdCondition == null)
			{
				throw new InvalidOperationException("Unable to select the adx_websiteid filter condition element.");
			}

			websiteIdCondition.SetAttributeValue("value", websiteId.ToString());

			var fetch = Fetch.Parse(fetchXml.ToString());

			var response = (serviceContext as IOrganizationService).RetrieveMultiple(fetch);

			return response.Entities.Select(e =>
			{
				var tag = e.GetAttributeAliasedValue<string>("tag");
				var count = e.GetAttributeAliasedValue<int?>("count").GetValueOrDefault();

				return new ForumThreadWeightedTagInfo(tag, count);
			}).ToArray();
		}

		private static IForumAuthor GetForumAuthor(string alias, Entity entity)
		{
			if (entity == null) throw new ArgumentNullException("entity");

			IForumAuthor unknown = new UnknownForumAuthor();
			var authorId = entity.GetAttributeAliasedValue<Guid?>("{0}.contactid".FormatWith(alias));

			return authorId == null
				? unknown
				: new ForumAuthor(
					new EntityReference("contact", authorId.Value),
					entity.GetAttributeAliasedValue<string>("{0}.fullname".FormatWith(alias)) ?? unknown.DisplayName,
					entity.GetAttributeAliasedValue<string>("{0}.emailaddress1".FormatWith(alias)) ?? unknown.EmailAddress);
		}

		private static IForumPostInfo GetForumPostInfo(string alias, Entity entity, IForumAuthor author)
		{
			if (entity == null) throw new ArgumentNullException("entity");
			if (author == null) throw new ArgumentNullException("author");

			IForumPostInfo unknownLatestPostInfo = new UnknownForumPostInfo();
			var latestPostId = entity.GetAttributeAliasedValue<Guid?>("{0}.adx_communityforumpostid".FormatWith(alias));

			return latestPostId == null
				? unknownLatestPostInfo
				: new ForumPostInfo(
					new EntityReference("adx_communityforumpost", latestPostId.Value),
					author,
					entity.GetAttributeAliasedValue<DateTime?>("{0}.adx_date".FormatWith(alias)).GetValueOrDefault(unknownLatestPostInfo.PostedOn));
		}

		private static IEnumerable<IForumThreadTag> GetForumThreadTags(string alias, IEnumerable<Entity> entities)
		{
			if (entities == null) throw new ArgumentNullException("entities");

			return entities
				.Select(e => e.GetAttributeAliasedValue<string>("{0}.adx_name".FormatWith(alias)))
				.Where(name => !string.IsNullOrWhiteSpace(name))
				.Select(name => new ForumThreadTag(name));
		}

		private static IForumThreadType GetForumThreadType(string alias, Entity entity)
		{
			if (entity == null) throw new ArgumentNullException("entity");

			IForumThreadType unknownForumThreadType = new UnknownForumThreadType();
			var threadTypeId = entity.GetAttributeAliasedValue<Guid?>("{0}.adx_forumthreadtypeid".FormatWith(alias));

			return threadTypeId == null
				? unknownForumThreadType
				: new ForumThreadType(
					new EntityReference("adx_forumthreadtype", threadTypeId.Value),
					entity.GetAttributeAliasedValue<string>("{0}.adx_name".FormatWith(alias)) ?? unknownForumThreadType.Name,
					entity.GetAttributeAliasedValue<int?>("{0}.adx_displayorder".FormatWith(alias)).GetValueOrDefault(unknownForumThreadType.DisplayOrder),
					entity.GetAttributeAliasedValue<bool?>("{0}.adx_allowsvoting".FormatWith(alias)).GetValueOrDefault(unknownForumThreadType.AllowsVoting),
					entity.GetAttributeAliasedValue<bool?>("{0}.adx_isdefault".FormatWith(alias)).GetValueOrDefault(unknownForumThreadType.IsDefault),
					entity.GetAttributeAliasedValue<bool?>("{0}.adx_requiresanswer".FormatWith(alias)).GetValueOrDefault(unknownForumThreadType.RequiresAnswer));
		}
	}
}
