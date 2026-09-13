/*
  Copyright (c) Microsoft Corporation. All rights reserved.
  Licensed under the MIT License. See License.txt in the project root for license information.
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Adxstudio.Xrm.Notes;
using Adxstudio.Xrm.Resources;
using Adxstudio.Xrm.Web.Mvc;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using Microsoft.Xrm.Client.Diagnostics;
using Microsoft.Xrm.Client.Security;
using Microsoft.Xrm.Portal.Web;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Client;

namespace Adxstudio.Xrm.Forums
{
	/// <summary>
	/// Provides query access to all Forum Posts (adx_communityforum) in a given Website (adx_website). Also provides
	/// query access to latest Forum Threads (adx_communityforumthread) across all Forums in that Website.
	/// </summary>
	public class WebsiteForumPostUserAggregationDataAdapter : IForumPostAggregationDataAdapter
	{
		/// <summary>
		/// Constructor.
		/// </summary>
		/// <param name="userId">The unique identifier of the portal user to aggregate data for.</param>
		/// <param name="dependencies">The dependencies to use for getting data.</param>
		public WebsiteForumPostUserAggregationDataAdapter(Guid userId, IDataAdapterDependencies dependencies)
		{
			dependencies.ThrowOnNull("dependencies");

			var website = dependencies.GetWebsite();
			website.ThrowOnNull("dependencies", ResourceManager.GetString("Website_Reference_Retrieval_Exception"));
			website.AssertLogicalName("adx_website");

			Website = website;
			Dependencies = dependencies;
			UserId = userId;

		}

		protected Guid UserId { get; private set; }

		protected IDataAdapterDependencies Dependencies { get; private set; }

		protected EntityReference Website { get; private set; }

		protected Func<OrganizationServiceContext, IQueryable<Entity>> SelectThreadEntities { get; private set; }

		public int SelectPostCount()
		{
			var serviceContext = Dependencies.GetServiceContext();
			var website = Dependencies.GetWebsite();

			return serviceContext.FetchAuthorForumPostCount(UserId, website.Id);
		}

		public IEnumerable<IForumPost> SelectPosts()
		{
			return SelectPosts(0);
		}

		public IEnumerable<IForumPost> SelectPosts(bool descending)
		{
			return SelectPosts(descending, 0);
		}

		public IEnumerable<IForumPost> SelectPosts(int startRowIndex, int maximumRows = -1)
		{
			return SelectPosts(false, startRowIndex, maximumRows);
		}

		public IEnumerable<IForumPost> SelectPosts(bool descending, int startRowIndex, int maximumRows = -1)
		{
            ADXTrace.Instance.TraceInfo(TraceCategory.Application, string.Format("startRowIndex={0}, maximumRows={1}: Start", startRowIndex, maximumRows));

			var serviceContext = Dependencies.GetServiceContext();
			var securityProvider = Dependencies.GetSecurityProvider();
			var entityUrlProvider = Dependencies.GetUrlProvider();
			var user = Dependencies.GetPortalUser();
			var website = Dependencies.GetWebsite();

			if (startRowIndex < 0)
			{
                throw new ArgumentException("Value must be a positive integer.", "startRowIndex");
			}

			if (maximumRows == 0)
			{
				return new IForumPost[] { };
			}

			var query = serviceContext.CreateQuery("adx_communityforumpost")
				.Join(serviceContext.CreateQuery("adx_communityforumthread"), fp => fp.GetAttributeValue<Guid>("adx_forumthreadid"), ft => ft.GetAttributeValue<Guid>("adx_communityforumthreadid"), (fp, ft) => new { Post = fp, Thread = ft })
				.Join(serviceContext.CreateQuery("adx_communityforum"), e => e.Thread.GetAttributeValue<Guid>("adx_forumid"), f => f.GetAttributeValue<Guid>("adx_communityforumid"), (e, f) => new { e, Forum = f })
				.Where(e => e.e.Post.GetAttributeValue<EntityReference>("adx_authorid").Id == UserId)
				.Where(e => e.Forum.GetAttributeValue<EntityReference>("adx_websiteid") == website);

			query = descending
				? query.OrderByDescending(e => e.e.Post.GetAttributeValue<DateTime?>("adx_date"))
				: query.OrderBy(e => e.e.Post.GetAttributeValue<DateTime?>("adx_date"));

			if (startRowIndex > 0)
			{
				query = query.Skip(startRowIndex);
			}

			if (maximumRows > 0)
			{
				query = query.Take(maximumRows);
			}

			var entities = query.Select(e => e.e.Post).ToArray();
			var firstPostEntity = entities.FirstOrDefault();
			
			var cloudStorageAccount = AnnotationDataAdapter.GetStorageAccount(serviceContext);
			var cloudStorageContainerName = AnnotationDataAdapter.GetStorageContainerName(serviceContext);
			BlobContainerClient cloudStorageContainer = null;
			if (cloudStorageAccount != null)
			{
				cloudStorageContainer = AnnotationDataAdapter.GetBlobContainer(cloudStorageAccount, cloudStorageContainerName);
			}

			var postInfos = serviceContext.FetchForumPostInfos(entities.Select(e => e.Id), website.Id, cloudStorageContainer);
			var urlProvider = new ForumPostUrlProvider();

			var posts = entities.Select(entity =>
			{
				IForumPostInfo postInfo;
				postInfo = postInfos.TryGetValue(entity.Id, out postInfo) ? postInfo : new UnknownForumPostInfo();
				var viewEntity = new PortalViewEntity(serviceContext, entity, securityProvider, entityUrlProvider);
				var thread = GetThread(postInfo.ThreadEntity.Id);

				return new ForumPost(entity, viewEntity, postInfo,
					new Lazy<ApplicationPath>(() => Dependencies.GetEditPath(entity.ToEntityReference()), LazyThreadSafetyMode.None),
					new Lazy<ApplicationPath>(() => Dependencies.GetDeletePath(entity.ToEntityReference()), LazyThreadSafetyMode.None),
					new Lazy<bool>(() => thread.Editable, LazyThreadSafetyMode.None),
					new Lazy<bool>(() =>
						thread.ThreadType != null
						&& thread.ThreadType.RequiresAnswer
						&& (firstPostEntity == null || !firstPostEntity.ToEntityReference().Equals(entity.ToEntityReference()))
						&& ((user != null && user.Equals(thread.Author.EntityReference)) || thread.Editable),
						LazyThreadSafetyMode.None),
					urlProvider.GetPostUrl(thread, entity.Id),
					thread,
					new Lazy<bool>(() =>
						user != null
						&& user.Equals(postInfo.Author.EntityReference), LazyThreadSafetyMode.None));
			}).ToArray();

            ADXTrace.Instance.TraceInfo(TraceCategory.Application, "End");

			return posts;
		}

		public IEnumerable<IForumPost> SelectPostsDescending()
		{
			return SelectPosts(true, 0);
		}

		public IEnumerable<IForumPost> SelectPostsDescending(int startRowIndex, int maximumRows = -1)
		{
			return SelectPosts(true, startRowIndex, maximumRows);
		}

		public IForumThread GetThread(Guid threadId)
		{
			var serviceContext = Dependencies.GetServiceContext();

			var entity = serviceContext.CreateQuery("adx_communityforumthread")
				.FirstOrDefault(ft => ft.GetAttributeValue<Guid>("adx_communityforumthreadid") == threadId);

			if (entity == null) return null;

			var securityProvider = Dependencies.GetSecurityProvider();

			if (!securityProvider.TryAssert(serviceContext, entity, CrmEntityRight.Read))
			{
                ADXTrace.Instance.TraceInfo(TraceCategory.Application, string.Format("Thread={0}: Not Found", threadId));

				return null;
			}

			var viewEntity = new PortalViewEntity(serviceContext, entity, securityProvider, Dependencies.GetUrlProvider());
			var website = Dependencies.GetWebsite();
			var threadInfo = serviceContext.FetchForumThreadInfo(entity.Id, website.Id);
			var counterStrategy = Dependencies.GetCounterStrategy();

			var thread = new ForumThread(
				entity,
				viewEntity,
				threadInfo,
				() => counterStrategy.GetForumThreadPostCount(serviceContext, entity),
				Dependencies.GetUrlProvider().GetUrl(serviceContext, entity));

            ADXTrace.Instance.TraceInfo(TraceCategory.Application, string.Format("Thread={0}: End", threadId));

			return thread;
		}

	}
}
