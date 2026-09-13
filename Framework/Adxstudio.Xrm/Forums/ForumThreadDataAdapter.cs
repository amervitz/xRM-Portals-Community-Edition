/*
  Copyright (c) Microsoft Corporation. All rights reserved.
  Licensed under the MIT License. See License.txt in the project root for license information.
*/

namespace Adxstudio.Xrm.Forums
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Threading;
	using System.Web;
	using Azure.Storage.Blobs;
	using Azure.Storage.Blobs.Models;
	using Azure.Storage.Sas;
	using Microsoft.Xrm.Client;
	using Microsoft.Xrm.Client.Security;
	using Microsoft.Xrm.Portal.Web;
	using Microsoft.Xrm.Sdk;
	using Microsoft.Xrm.Sdk.Client;
	using Microsoft.Xrm.Sdk.Query;
	using Adxstudio.Xrm.Core.Flighting;
	using Adxstudio.Xrm.Notes;
	using Adxstudio.Xrm.Web.Mvc;
	using Adxstudio.Xrm.Services;
	using Adxstudio.Xrm.Services.Query;

	public class ForumThreadDataAdapter : IForumThreadDataAdapter
	{
		public ForumThreadDataAdapter(EntityReference forumThread, IDataAdapterDependencies dependencies)
		{
			if (forumThread == null)
			{
				throw new ArgumentNullException("forumThread");
			}

			if (forumThread.LogicalName != "adx_communityforumthread")
			{
				throw new ArgumentException(string.Format("Value must have logical name {0}.", forumThread.LogicalName), "forumThread");
			}

			if (dependencies == null)
			{
				throw new ArgumentNullException("dependencies");
			}

			ForumThread = forumThread;
			Dependencies = dependencies;
		}

		public ForumThreadDataAdapter(IForumThread forumThread, IDataAdapterDependencies dependencies)
			: this(forumThread.EntityReference, dependencies) { }

		protected IDataAdapterDependencies Dependencies { get; private set; }

		protected EntityReference ForumThread { get; private set; }

		public IForumThread Select()
		{
			ADXTrace.Instance.TraceInfo(TraceCategory.Application, string.Format("Thread={0}: Start", ForumThread.Id));

			var serviceContext = Dependencies.GetServiceContext();

			var entity = SelectEntity(serviceContext);

			if (entity == null)
			{
				ADXTrace.Instance.TraceInfo(TraceCategory.Application, string.Format("Thread={0}: Not Found", ForumThread.Id));

				return null;
			}

			var securityProvider = Dependencies.GetSecurityProvider();

			if (!securityProvider.TryAssert(serviceContext, entity, CrmEntityRight.Read))
			{
				ADXTrace.Instance.TraceInfo(TraceCategory.Application, string.Format("Thread={0}: Not Found", ForumThread.Id));

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
				// Only lazily get the post count, because it's unlikely to be used in the common case.
				// SelectPostCount will generally be used instead.
				() => counterStrategy.GetForumThreadPostCount(serviceContext, entity));

			ADXTrace.Instance.TraceInfo(TraceCategory.Application, string.Format("Thread={0}: End", ForumThread.Id));

			if (FeatureCheckHelper.IsFeatureEnabled(FeatureNames.TelemetryFeatureUsage))
			{
				PortalFeatureTrace.TraceInstance.LogFeatureUsage(FeatureTraceCategory.Forum, HttpContext.Current, "read_forum_thread", 1, thread.Entity.ToEntityReference(), "read");
			}

			return thread;
		}

		public void CreateAlert(EntityReference user)
		{
			if (user == null) throw new ArgumentNullException("user");

			if (user.LogicalName != "contact")
			{
				throw new ArgumentException(string.Format("Value must have logical name '{0}'", user.LogicalName), "user");
			}

			ADXTrace.Instance.TraceInfo(TraceCategory.Application, string.Format("Start: {0}:{1}", user.LogicalName, user.Id));

			var serviceContext = Dependencies.GetServiceContextForWrite();

			var existingAlert = SelectAlert(serviceContext, user);

			if (existingAlert != null)
			{
				ADXTrace.Instance.TraceInfo(TraceCategory.Application, string.Format("End: {0}:{1}, Alert Exists", user.LogicalName, user.Id));

				return;
			}

			var alert = new Entity("adx_communityforumalert");

			alert["adx_subscriberid"] = user;
			alert["adx_threadid"] = ForumThread;

			serviceContext.AddObject(alert);
			serviceContext.SaveChanges();

			ADXTrace.Instance.TraceInfo(TraceCategory.Application, string.Format("End: {0}:{1}", user.LogicalName, user.Id));
		}

		public IForumPost CreatePost(IForumPostSubmission forumPost, bool incrementForumThreadCount = false)
		{
			if (forumPost == null) throw new ArgumentNullException("forumPost");

			ADXTrace.Instance.TraceInfo(TraceCategory.Application, "Start");

			var serviceContext = Dependencies.GetServiceContextForWrite();

			var thread = Select();

			var locked = thread.Locked;

			if (locked) throw new InvalidOperationException("You can't create a new post because the forum is locked.");

			var entity = new Entity("adx_communityforumpost");

			entity["adx_forumthreadid"] = ForumThread;
			entity["adx_name"] = Truncate(forumPost.Name, 100);
			entity["adx_isanswer"] = forumPost.IsAnswer;
			entity["adx_authorid"] = forumPost.Author.EntityReference;
			entity["adx_date"] = forumPost.PostedOn;
			entity["adx_content"] = forumPost.Content;
			entity["adx_helpfulvotecount"] = forumPost.HelpfulVoteCount;

			serviceContext.AddObject(entity);
			serviceContext.SaveChanges();

			var threadEntity = SelectEntity(serviceContext);
			var threadUpdate = new Entity(threadEntity.LogicalName) { Id = threadEntity.Id };

			threadUpdate["adx_lastpostdate"] = forumPost.PostedOn;
			threadUpdate["adx_lastpostid"] = entity.ToEntityReference();
			threadUpdate["adx_postcount"] = threadEntity.GetAttributeValue<int?>("adx_postcount").GetValueOrDefault() + 1;

			if (threadEntity.GetAttributeValue<EntityReference>("adx_firstpostid") == null)
			{
				threadUpdate["adx_firstpostid"] = entity.ToEntityReference();
			}

			serviceContext.Detach(threadEntity);
			serviceContext.Attach(threadUpdate);
			serviceContext.UpdateObject(threadUpdate);
			serviceContext.SaveChanges();

			var entityReference = entity.ToEntityReference();

			var forumDataAdapter = new ForumDataAdapter(threadEntity.GetAttributeValue<EntityReference>("adx_forumid"), Dependencies);

			forumDataAdapter.UpdateLatestPost(entityReference, incrementForumThreadCount);

			foreach (var attachment in forumPost.Attachments)
			{
				IAnnotationDataAdapter da = new AnnotationDataAdapter(Dependencies);
				da.CreateAnnotation(entityReference, string.Empty, string.Empty, attachment.Name, attachment.ContentType,
					attachment.Content);
			}

			var post = SelectPost(entityReference.Id);

			ADXTrace.Instance.TraceInfo(TraceCategory.Application, "End");

			if (FeatureCheckHelper.IsFeatureEnabled(FeatureNames.TelemetryFeatureUsage))
			{
				PortalFeatureTrace.TraceInstance.LogFeatureUsage(FeatureTraceCategory.Forum, HttpContext.Current, "create_forum_post", 1, post.Entity.ToEntityReference(), "create");
			}

			return post;
		}

		public void UpdatePost(IForumPostSubmission forumPost)
		{
			if (forumPost == null) throw new ArgumentNullException("forumPost");

			ADXTrace.Instance.TraceInfo(TraceCategory.Application, "Start");

			var entityReference = ((IForumPostInfo)forumPost).EntityReference;

			var serviceContext = Dependencies.GetServiceContextForWrite();

			var update = new Entity("adx_communityforumpost")
			{
				Id = entityReference.Id
			};

			if (forumPost.Name != null)
			{
				update["adx_name"] = Truncate(forumPost.Name, 100);
			}

			if (forumPost.Content != null)
			{
				update["adx_content"] = forumPost.Content;
			}

			if (update.Attributes.Any())
			{
				serviceContext.Attach(update);
				serviceContext.UpdateObject(update);
				serviceContext.SaveChanges();
			}

			foreach (var attachment in forumPost.Attachments)
			{
				IAnnotationDataAdapter da = new AnnotationDataAdapter(Dependencies);
				da.CreateAnnotation(entityReference, string.Empty, string.Empty, attachment.Name, attachment.ContentType,
					attachment.Content);
			}

			if (FeatureCheckHelper.IsFeatureEnabled(FeatureNames.TelemetryFeatureUsage))
			{
				PortalFeatureTrace.TraceInstance.LogFeatureUsage(FeatureTraceCategory.Forum, HttpContext.Current, "edit_forum_post", 1, entityReference, "edit");
			}

			ADXTrace.Instance.TraceInfo(TraceCategory.Application, "End");
		}

		public void DeleteAlert(EntityReference user)
		{
			if (user == null) throw new ArgumentNullException("user");

			ADXTrace.Instance.TraceInfo(TraceCategory.Application, string.Format("Start: {0}:{1}", user.LogicalName, user.Id));

			var serviceContext = Dependencies.GetServiceContextForWrite();

			var existingAlert = SelectAlert(serviceContext, user);

			if (existingAlert == null)
			{
				ADXTrace.Instance.TraceInfo(TraceCategory.Application, string.Format("End: {0}:{1}, Alert Not Found", user.LogicalName, user.Id));

				return;
			}

			serviceContext.DeleteObject(existingAlert);
			serviceContext.SaveChanges();

			ADXTrace.Instance.TraceInfo(TraceCategory.Application, string.Format("End: {0}:{1}", user.LogicalName, user.Id));
		}

		public void DeletePost(EntityReference forumPost)
		{
			if (forumPost == null) throw new ArgumentNullException("forumPost");

			var serviceContext = Dependencies.GetServiceContextForWrite();

			var post = serviceContext.RetrieveSingle(new Fetch
			{
				Entity = new FetchEntity("adx_communityforumpost")
				{
					Filters = new[] { new Filter {
						Conditions = new[]
						{
							new Condition("adx_communityforumpostid", ConditionOperator.Equal, forumPost.Id),
							new Condition("adx_forumthreadid", ConditionOperator.Equal, this.ForumThread.Id)
						} } }
				}
			});

			if (post == null)
			{
				throw new ArgumentException("Unable to find {0} {1}.".FormatWith(forumPost.LogicalName, forumPost.Id), "forumPost");
			}

			var thread = SelectEntity(serviceContext);

			if (thread == null)
			{
				serviceContext.DeleteObject(post);
				serviceContext.SaveChanges();

				return;
			}

			var postReference = post.ToEntityReference();

			var counterStrategy = Dependencies.GetCounterStrategy();
			var threadPostCount = counterStrategy.GetForumThreadPostCount(serviceContext, thread);
			var threadUpdate = new Entity(thread.LogicalName) { Id = thread.Id };
			threadUpdate["adx_postcount"] = threadPostCount < 1 ? 0 : threadPostCount - 1;

			// If deleting first post, delete entire thread
			if (Equals(thread.GetAttributeValue<EntityReference>("adx_firstpostid"), postReference))
			{
				var forumDataAdapter = new ForumDataAdapter(thread.GetAttributeValue<EntityReference>("adx_forumid"), Dependencies);

				forumDataAdapter.DeleteThread(thread.ToEntityReference());
			}

			// Determine the new last post in the thread.
			else if (Equals(thread.GetAttributeValue<EntityReference>("adx_lastpostid"), postReference))
			{
				var lastPostFetch = new Fetch
				{
					Entity = new FetchEntity("adx_communityforumpost")
					{
						Filters = new[] 
						{
							new Filter
							{
								Conditions = new[]
								{
									new Condition("adx_forumthreadid", ConditionOperator.Equal, thread.Id),
									new Condition("adx_communityforumpostid", ConditionOperator.NotEqual, post.Id)
								}
							}
						},
						Orders = new[] { new Order("adx_date", OrderType.Descending) }
					},
					PageNumber = 1,
					PageSize = 1
				};

				var lastPost = serviceContext.RetrieveSingle(lastPostFetch);

				if (lastPost == null)
				{
					throw new InvalidOperationException("Unable to determine new last post in thread.");
				}

				threadUpdate["adx_lastpostid"] = lastPost.ToEntityReference();
				threadUpdate["adx_lastpostdate"] = lastPost.GetAttributeValue<DateTime?>("adx_date");

				serviceContext.Detach(thread);
				serviceContext.Attach(threadUpdate);
				serviceContext.UpdateObject(threadUpdate);
				serviceContext.DeleteObject(post);
				serviceContext.SaveChanges();
			}

			var forumId = thread.GetAttributeValue<EntityReference>("adx_forumid");

			if (forumId == null)
			{
				return;
			}

			var forumServiceContext = Dependencies.GetServiceContextForWrite();

			var forum = forumServiceContext.RetrieveSingle("adx_communityforum", "adx_communityforumid", forumId.Id, FetchAttribute.All);

			if (forum == null)
			{
				return;
			}

			// Update forum counts and pointers.
			var forumCounts = counterStrategy.GetForumCounts(forumServiceContext, forum);
			var forumUpdate = new Entity(forum.LogicalName) { Id = forum.Id };
			forumUpdate["adx_postcount"] = forumCounts.PostCount < 1 ? 0 : forumCounts.PostCount - 1;

			var forumLastPost = forum.GetAttributeValue<EntityReference>("adx_lastpostid");

			if (Equals(forumLastPost, postReference))
			{
				var lastUpdatedThreadFetch = new Fetch
				{
					Entity = new FetchEntity("adx_communityforumthread")
					{
						Filters = new[] 
						{
							new Filter
							{
								Conditions = new[]
								{
									new Condition("adx_forumid", ConditionOperator.Equal, forum.Id),
									new Condition("adx_lastpostid", ConditionOperator.NotNull)
								}
							}
						},
						Orders = new[] { new Order("adx_lastpostdate", OrderType.Descending) }
					},
					PageNumber = 1,
					PageSize = 1
				};

				var lastUpdatedThread = forumServiceContext.RetrieveSingle(lastUpdatedThreadFetch);

				forumUpdate["adx_lastpostid"] = lastUpdatedThread == null
					? null
					: lastUpdatedThread.GetAttributeValue<EntityReference>("adx_lastpostid");
			}

			forumServiceContext.Detach(forum);
			forumServiceContext.Attach(forumUpdate);
			forumServiceContext.UpdateObject(forumUpdate);
			forumServiceContext.SaveChanges();

			if (FeatureCheckHelper.IsFeatureEnabled(FeatureNames.TelemetryFeatureUsage))
			{
				PortalFeatureTrace.TraceInstance.LogFeatureUsage(FeatureTraceCategory.Forum, HttpContext.Current, "delete_forum_post", 1, forumPost, "delete");
			}
		}

		public void MarkAsAnswer(Guid forumPostId)
		{
			if (forumPostId == null) throw new ArgumentNullException("forumPostId");

			var serviceContext = Dependencies.GetServiceContextForWrite();
			var postFetch = new Fetch
			{
				Entity = new FetchEntity("adx_communityforumpost")
				{
					Filters = new[] { new Filter
					{
						Conditions = new[]
						{
							new Condition("adx_communityforumpostid", ConditionOperator.Equal, forumPostId),
							new Condition("adx_forumthreadid", ConditionOperator.Equal, this.ForumThread.Id)
						}
					} }
				}
			};

			var post = serviceContext.RetrieveSingle(postFetch);

			if (post == null)
			{
				throw new ArgumentException("Unable to find {0} {1}.".FormatWith("adx_communityforumpost", forumPostId), "forumPostId");
			}

			var thread = SelectEntity(serviceContext);

			var postUpdate = new Entity(post.LogicalName) { Id = post.Id };
			var threadUpdate = new Entity(thread.LogicalName) { Id = thread.Id };

			postUpdate["adx_isanswer"] = true;
			threadUpdate["adx_isanswered"] = true;

			serviceContext.Detach(post);
			serviceContext.Detach(thread);
			serviceContext.Attach(postUpdate);
			serviceContext.Attach(threadUpdate);
			serviceContext.UpdateObject(postUpdate);
			serviceContext.UpdateObject(threadUpdate);
			serviceContext.SaveChanges();
		}

		public void UnMarkAsAnswer(Guid forumPostId)
		{
			if (forumPostId == null) throw new ArgumentNullException("forumPostId");

			var serviceContext = Dependencies.GetServiceContextForWrite();

			var answerPostsFetch = new Fetch
			{
				Entity = new FetchEntity("adx_communityforumpost")
				{
					Filters = new[] { new Filter
					{
						Conditions = new[]
						{
							new Condition("adx_isanswer", ConditionOperator.Equal, true),
							new Condition("adx_forumthreadid", ConditionOperator.Equal, this.ForumThread.Id)
						}
					} }
				}
			};

			var answerPosts = serviceContext.RetrieveMultiple(answerPostsFetch).Entities;

			var post = answerPosts.FirstOrDefault(e => e.GetAttributeValue<Guid>("adx_communityforumpostid") == forumPostId);

			if (post == null)
			{
				// The post was already not an answer, so do nothing.
				return;
			}

			var thread = SelectEntity(serviceContext);

			var postUpdate = new Entity(post.LogicalName) { Id = post.Id };
			var threadUpdate = new Entity(thread.LogicalName) { Id = thread.Id };

			postUpdate["adx_isanswer"] = false;

			// If the thread will still have at least one other answer post, after the given post is updated, it is still answered.
			threadUpdate["adx_isanswered"] = answerPosts.Count > 1;

			serviceContext.Detach(thread);
			serviceContext.Detach(post);
			serviceContext.Attach(postUpdate);
			serviceContext.Attach(threadUpdate);
			serviceContext.UpdateObject(postUpdate);
			serviceContext.UpdateObject(threadUpdate);
			serviceContext.SaveChanges();
		}

		public bool HasAlert(EntityReference user)
		{
			if (user == null) throw new ArgumentNullException("user");

			ADXTrace.Instance.TraceInfo(TraceCategory.Application, string.Format("Start: {0}:{1}", user.LogicalName, user.Id));

			var serviceContext = Dependencies.GetServiceContext();
			var existingAlert = SelectAlert(serviceContext, user);

			var hasAlert = existingAlert != null;

			ADXTrace.Instance.TraceInfo(TraceCategory.Application, string.Format("End: {0}:{1}, {2}", user.LogicalName, user.Id, hasAlert));

			return hasAlert;
		}

		public int SelectPostCount()
		{
			ADXTrace.Instance.TraceInfo(TraceCategory.Application, "Start");

			var serviceContext = Dependencies.GetServiceContext();
			var entity = SelectEntity(serviceContext);

			if (entity == null)
			{
				return 0;
			}

			var counterStrategy = Dependencies.GetCounterStrategy();

			var postCount = counterStrategy.GetForumThreadPostCount(serviceContext, entity);

			ADXTrace.Instance.TraceInfo(TraceCategory.Application, "End");

			return postCount;
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

			if (startRowIndex < 0)
			{
				throw new ArgumentException("Value must be a positive integer.", "startRowIndex");
			}

			if (maximumRows == 0)
			{
				return new IForumPost[] { };
			}

			var serviceContext = Dependencies.GetServiceContext();
			var securityProvider = Dependencies.GetSecurityProvider();

			var query = serviceContext.CreateQuery("adx_communityforumpost")
				.Where(e => e.GetAttributeValue<EntityReference>("adx_forumthreadid") == ForumThread);

			query = descending
				? query.OrderByDescending(e => e.GetAttributeValue<DateTime?>("adx_date"))
				: query.OrderBy(e => e.GetAttributeValue<DateTime?>("adx_date"));

			if (startRowIndex > 0)
			{
				query = query.Skip(startRowIndex);
			}

			if (maximumRows > 0)
			{
				query = query.Take(maximumRows);
			}

			var website = Dependencies.GetWebsite();
			var entities = query.ToArray();
			var firstPostEntity = entities.FirstOrDefault();

			var cloudStorageAccount = AnnotationDataAdapter.GetStorageAccount(serviceContext);
			var cloudStorageContainerName = AnnotationDataAdapter.GetStorageContainerName(serviceContext);
			BlobContainerClient cloudStorageContainer = null;
			if (cloudStorageAccount != null)
			{
				cloudStorageContainer = AnnotationDataAdapter.GetBlobContainer(cloudStorageAccount, cloudStorageContainerName);
			}

			var postInfos = serviceContext.FetchForumPostInfos(entities.Select(e => e.Id), website.Id, cloudStorageContainer: cloudStorageContainer);
			var urlProvider = Dependencies.GetUrlProvider();
			var thread = Select();
			var user = Dependencies.GetPortalUser();

			var posts = entities.Select(entity =>
			{
				IForumPostInfo postInfo;
				postInfo = postInfos.TryGetValue(entity.Id, out postInfo) ? postInfo : new UnknownForumPostInfo();
				var viewEntity = new PortalViewEntity(serviceContext, entity, securityProvider, urlProvider);

				return new ForumPost(entity, viewEntity, postInfo,
					new Lazy<ApplicationPath>(() => Dependencies.GetEditPath(entity.ToEntityReference()), LazyThreadSafetyMode.None),
					new Lazy<ApplicationPath>(() => Dependencies.GetDeletePath(entity.ToEntityReference()), LazyThreadSafetyMode.None),
					new Lazy<bool>(() => thread.Editable, LazyThreadSafetyMode.None),
					new Lazy<bool>(() =>
						thread.ThreadType != null
						&& thread.ThreadType.RequiresAnswer
						&& (firstPostEntity == null || !firstPostEntity.ToEntityReference().Equals(entity.ToEntityReference()))
						&& ((user != null && user.Equals(thread.Author.EntityReference)) || thread.Editable)
						&& !thread.Locked,
						LazyThreadSafetyMode.None),
					canEdit: new Lazy<bool>(() =>
						user != null
						&& user.Equals(postInfo.Author.EntityReference)
						&& !thread.Locked, LazyThreadSafetyMode.None));
			}).ToArray();

			ADXTrace.Instance.TraceInfo(TraceCategory.Application, "End");

			return posts;
		}

		public IEnumerable<IForumPost> SelectPostsDescending()
		{
			return SelectPostsDescending(0);
		}

		public IEnumerable<IForumPost> SelectPostsDescending(int startRowIndex, int maximumRows = -1)
		{
			return SelectPosts(true, startRowIndex, maximumRows);
		}

		protected Entity SelectAlert(OrganizationServiceContext serviceContext, EntityReference user)
		{
			if (serviceContext == null) throw new ArgumentNullException("serviceContext");
			if (user == null) throw new ArgumentNullException("user");

			return serviceContext.RetrieveSingle(new Fetch
			{
				Entity = new FetchEntity("adx_communityforumalert")
				{
					Filters = new[] 
					{
						new Filter
						{
							Conditions = new[] {
								new Condition("adx_subscriberid", ConditionOperator.Equal, user.Id),
								new Condition("adx_threadid", ConditionOperator.Equal, this.ForumThread.Id) }
						}
					}
				}
			});
		}

		public IForumPost SelectPost(Guid forumPostId)
		{
			var serviceContext = Dependencies.GetServiceContext();

			var postFetch = new Fetch
			{
				Entity = new FetchEntity("adx_communityforumpost")
				{
					Filters = new[]
					{
						new Filter
						{
							Conditions = new[] {
								new Condition("adx_communityforumpostid", ConditionOperator.Equal, forumPostId),
								new Condition("adx_forumthreadid", ConditionOperator.Equal, this.ForumThread.Id) }
						}
					}
				}
			};
			var entity = serviceContext.RetrieveSingle(postFetch);

			if (entity == null) return null;

			var website = Dependencies.GetWebsite();
			var securityProvider = Dependencies.GetSecurityProvider();
			var urlProvider = Dependencies.GetUrlProvider();
			var viewEntity = new PortalViewEntity(serviceContext, entity, securityProvider, urlProvider);

			var cloudStorageAccount = AnnotationDataAdapter.GetStorageAccount(serviceContext);
			var cloudStorageContainerName = AnnotationDataAdapter.GetStorageContainerName(serviceContext);
			BlobContainerClient cloudStorageContainer = null;
			if (cloudStorageAccount != null)
			{
				cloudStorageContainer = AnnotationDataAdapter.GetBlobContainer(cloudStorageAccount, cloudStorageContainerName);
			}

			var postInfo = serviceContext.FetchForumPostInfo(entity.Id, website.Id, cloudStorageContainer);
			var user = Dependencies.GetPortalUser();

			return new ForumPost(entity, viewEntity, postInfo,
				new Lazy<ApplicationPath>(() => Dependencies.GetEditPath(entity.ToEntityReference()), LazyThreadSafetyMode.None),
				new Lazy<ApplicationPath>(() => Dependencies.GetDeletePath(entity.ToEntityReference()), LazyThreadSafetyMode.None),
				canEdit: new Lazy<bool>(() =>
					user != null
					&& user.Equals(postInfo.Author.EntityReference), LazyThreadSafetyMode.None));
		}

		public IForumPost SelectFirstPost()
		{
			var serviceContext = Dependencies.GetServiceContext();

			var thread = serviceContext.RetrieveSingle(
				"adx_communityforumthread",
				"adx_communityforumthreadid",
				this.ForumThread.Id,
				new[] { new FetchAttribute("adx_firstpostid") });

			return thread == null || thread.GetAttributeValue<EntityReference>("adx_firstpostid") == null
						? null
						: SelectPost(thread.GetAttributeValue<EntityReference>("adx_firstpostid").Id);
		}

		public IForumPost SelectLatestPost()
		{
			var serviceContext = Dependencies.GetServiceContext();

			var thread = serviceContext.RetrieveSingle(
				"adx_communityforumthread",
				"adx_communityforumthreadid",
				this.ForumThread.Id,
				new[] { new FetchAttribute("adx_lastpostid") });

			return thread == null || thread.GetAttributeValue<EntityReference>("adx_lastpostid") == null
						? null
						: SelectPost(thread.GetAttributeValue<EntityReference>("adx_lastpostid").Id);
		}

		private Entity SelectEntity(OrganizationServiceContext serviceContext)
		{
			var entity = serviceContext.RetrieveSingle(
				"adx_communityforumthread",
				"adx_communityforumthreadid",
				this.ForumThread.Id,
				FetchAttribute.All);

			return entity;
		}

		private static string Truncate(string value, int maxLength)
		{
			if (value == null)
			{
				return null;
			}

			return value.Length <= maxLength ? value : value.Substring(0, maxLength);
		}
	}
}
