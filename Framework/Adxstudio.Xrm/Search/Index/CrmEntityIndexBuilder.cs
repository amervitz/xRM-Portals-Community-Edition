/*
  Copyright (c) Microsoft Corporation. All rights reserved.
  Licensed under the MIT License. See License.txt in the project root for license information.
*/

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using Lucene.Net.Index;
using Lucene.Net.Search;
using Lucene.Net.Store;
using Polly;
using Adxstudio.Xrm.Threading;
using Fetch = Adxstudio.Xrm.Services.Query;
using Adxstudio.Xrm.Cms;
using Adxstudio.Xrm.Configuration;
using Adxstudio.Xrm.Diagnostics.Trace;

namespace Adxstudio.Xrm.Search.Index
{
	public class CrmEntityIndexBuilder : ICrmEntityIndexBuilder, ICrmEntityIndexUpdater
	{
		private readonly ICrmEntityIndex _index;

		public CrmEntityIndexBuilder(ICrmEntityIndex index)
		{
			if (index == null)
			{
				throw new ArgumentNullException("index");
			}

			_index = index;
		}

		public void BuildIndex()
		{
			var timer = Stopwatch.StartNew();

			ADXTrace.Instance.TraceInfo(TraceCategory.Application, "Start");

			var indexers = _index.GetIndexers();

			ADXTrace.Instance.TraceInfo(TraceCategory.Application, "Retrieving index documents");

			var entityIndexDocuments = indexers.SelectMany(indexer => indexer.GetDocuments());

			UsingWriter(MethodBase.GetCurrentMethod().Name, true, true, writer =>
			{
				foreach (var entityIndexDocument in entityIndexDocuments)
				{
					writer.AddDocument(entityIndexDocument.Document, entityIndexDocument.Analyzer);
				}
			});

			timer.Stop();

			ADXTrace.Instance.TraceInfo(TraceCategory.Application, string.Format("End. Elapsed time: {0}", timer.ElapsedMilliseconds));
		}

		public void Dispose() { }

		public void DeleteEntity(string entityLogicalName, Guid id)
		{
			var indexers = _index.GetIndexers(entityLogicalName).ToArray();

			if (!indexers.Any(indexer => indexer.Indexes(entityLogicalName)))
			{
				ADXTrace.Instance.TraceInfo(TraceCategory.Application, string.Format("Application does not index entity {0}. No update performed.", entityLogicalName));

				return;
			}

			ADXTrace.Instance.TraceInfo(TraceCategory.Application, string.Format("Deleting index for EntityLogicalName: {0}, Guid: {1} ", EntityNamePrivacy.GetEntityName(entityLogicalName), id));

			UsingWriter(MethodBase.GetCurrentMethod().Name, false, false, writer => writer.DeleteDocuments(GetEntityQuery(_index, entityLogicalName, id)));
		}

		public void DeleteEntitySet(string entityLogicalName)
		{
			var indexers = _index.GetIndexers(entityLogicalName).ToArray();

			if (!indexers.Any(indexer => indexer.Indexes(entityLogicalName)))
			{
				ADXTrace.Instance.TraceInfo(TraceCategory.Application, string.Format("Application does not index entity {0}. No update performed.", entityLogicalName));

				return;
			}

			ADXTrace.Instance.TraceInfo(TraceCategory.Application, string.Format("Deleting index set for EntityLogicalName: {0}", EntityNamePrivacy.GetEntityName(entityLogicalName)));

			UsingWriter(MethodBase.GetCurrentMethod().Name, false, true, writer => writer.DeleteDocuments(new Term(_index.LogicalNameFieldName, entityLogicalName)));
		}

		public void UpdateEntity(string entityLogicalName, Guid id)
		{
			var indexers = _index.GetIndexers(entityLogicalName, id).ToArray();

			if (!indexers.Any(indexer => indexer.Indexes(entityLogicalName)))
			{
				ADXTrace.Instance.TraceInfo(TraceCategory.Application, string.Format("Application does not index entity {0}. No update performed.", entityLogicalName));

				return;
			}

			var entityIndexDocuments = indexers.SelectMany(indexer => indexer.GetDocuments()).ToArray();

			UsingWriter(MethodBase.GetCurrentMethod().Name, false, false, writer =>
			{
				writer.DeleteDocuments(GetEntityQuery(_index, entityLogicalName, id));

				foreach (var entityIndexDocument in entityIndexDocuments)
				{
					writer.AddDocument(entityIndexDocument.Document, entityIndexDocument.Analyzer);
				}
			});
		}

		public void UpdateEntitySet(string entityLogicalName)
		{
			var indexers = _index.GetIndexers(entityLogicalName).ToArray();

			if (!indexers.Any(indexer => indexer.Indexes(entityLogicalName)))
			{
				ADXTrace.Instance.TraceInfo(TraceCategory.Application, string.Format("Application does not index entity {0}. No update performed.", entityLogicalName));

				return;
			}

			var entityIndexDocuments = indexers.SelectMany(indexer => indexer.GetDocuments());

			UsingWriter(MethodBase.GetCurrentMethod().Name, false, true, writer =>
			{
				writer.DeleteDocuments(new Term(_index.LogicalNameFieldName, entityLogicalName));

				foreach (var entityIndexDocument in entityIndexDocuments)
				{
					writer.AddDocument(entityIndexDocument.Document, entityIndexDocument.Analyzer);
				}
			});
		}

		public void UpdateEntitySet(string entityLogicalName, string entityAttribute, List<Guid> entityIds)
		{
			var filter = new Fetch.Filter
			{
				Type = Microsoft.Xrm.Sdk.Query.LogicalOperator.Or,
				Conditions = new List<Fetch.Condition>()
					{
						new Fetch.Condition
						{
							Attribute = entityAttribute,
							Operator = Microsoft.Xrm.Sdk.Query.ConditionOperator.In,
							Values = entityIds.Cast<object>().ToList()
						},
					}
			};

			var entityIndexers = _index.GetIndexers(entityLogicalName, filters: new List<Fetch.Filter> { filter });
			UpdateWithIndexers(entityLogicalName, entityIndexers);
		}

		public void UpdateCmsEntityTree(string entityLogicalName, Guid rootEntityId, int? lcid = null)
		{
			ADXTrace.Instance.TraceInfo(TraceCategory.Application, string.Format("Begin updating Cms Entity Tree for logical name: {0}, rootEntityId: {1}", entityLogicalName, rootEntityId));
			var timer = Stopwatch.StartNew();

			if (entityLogicalName == "adx_webpage")
			{
				IContentMapProvider contentMapProvider = AdxstudioCrmConfigurationManager.CreateContentMapProvider();
				Guid[] descendantLocalizedWebPagesGuids = CmsIndexHelper.GetDescendantLocalizedWebpagesForWebpage(contentMapProvider, rootEntityId, lcid).ToArray();
				Guid[] descendantRootWebPagesGuids = CmsIndexHelper.GetDescendantRootWebpagesForWebpage(contentMapProvider, rootEntityId).ToArray();

				// -------------------- WEB PAGES ------------------------------
				if (descendantLocalizedWebPagesGuids.Any())
				{
					var localizedWebPagesUnderTargetWebPageFilter = new Fetch.Filter
					{
						Type = Microsoft.Xrm.Sdk.Query.LogicalOperator.Or,
						Conditions = new List<Fetch.Condition>()
						{
							new Fetch.Condition
							{
								Attribute = "adx_webpageid",
								Operator = Microsoft.Xrm.Sdk.Query.ConditionOperator.In,
								Values = descendantLocalizedWebPagesGuids.Cast<object>().ToList()
							},
						}
					};

					var webPageIndexers = _index.GetIndexers("adx_webpage", filters: new List<Fetch.Filter> { localizedWebPagesUnderTargetWebPageFilter });

					UpdateWithIndexers("adx_webpage", webPageIndexers);
				}

				// -------------------- FORUMS ------------------------------
				if (descendantRootWebPagesGuids.Any())
				{
					var rootWebPagesUnderTargetWebPageFilter = new Fetch.Filter
					{
						Type = Microsoft.Xrm.Sdk.Query.LogicalOperator.Or,
						Conditions = new List<Fetch.Condition>()
						{
							new Fetch.Condition
							{
								Attribute = "adx_webpageid",
								Operator = Microsoft.Xrm.Sdk.Query.ConditionOperator.In,
								Values = descendantRootWebPagesGuids.Cast<object>().ToList()
							},
						}
					};

					var forumBlogToParentPageLink = new Fetch.Link
					{
						Name = "adx_webpage",
						FromAttribute = "adx_webpageid",
						ToAttribute = "adx_parentpageid",
						Filters = new List<Fetch.Filter>()
						{
							rootWebPagesUnderTargetWebPageFilter
						}
					};

					Fetch.Link languageFilter = null;

					if (lcid.HasValue)
					{
						languageFilter = new Fetch.Link
						{
							Name = "adx_websitelanguage",
							FromAttribute = "adx_websitelanguageid",
							ToAttribute = "adx_websitelanguageid",
							Type = Microsoft.Xrm.Sdk.Query.JoinOperator.Inner,
							Alias = "websitelangforupdatefilter",
							Links = new List<Fetch.Link>()
							{
								new Fetch.Link
								{
									Name = "adx_portallanguage",
									FromAttribute = "adx_portallanguageid",
									ToAttribute = "adx_portallanguageid",
									Type = Microsoft.Xrm.Sdk.Query.JoinOperator.Inner,
									Alias = "portallangforupdatefilter",
									Filters = new List<Fetch.Filter>()
									{
										new Fetch.Filter
										{
											Type = Microsoft.Xrm.Sdk.Query.LogicalOperator.And,
											Conditions = new List<Fetch.Condition>()
											{
												new Fetch.Condition
												{
													Attribute = "adx_lcid",
													Operator = Microsoft.Xrm.Sdk.Query.ConditionOperator.Equal,
													Value = lcid.Value
												}
											}
										}
									}
								}
							}
						};
					}

					var forumBlogLinks = new List<Fetch.Link>() { forumBlogToParentPageLink };
					if (languageFilter != null)
					{
						forumBlogLinks.Add(languageFilter);
					}

					var forumIndexers = _index.GetIndexers("adx_communityforum", links: forumBlogLinks);
					UpdateWithIndexers("adx_communityforum", forumIndexers);

					var forumThreadForumLinks = new List<Fetch.Link>() { forumBlogToParentPageLink };
					if (languageFilter != null)
					{
						forumThreadForumLinks.Add(languageFilter);
					}

					var forumThreadToParentPageLink = new Fetch.Link
					{
						Name = "adx_communityforum",
						FromAttribute = "adx_communityforumid",
						ToAttribute = "adx_forumid",
						Links = forumThreadForumLinks
					};

					var forumThreadIndexers = _index.GetIndexers("adx_communityforumthread",
						links: new List<Fetch.Link>() { forumThreadToParentPageLink });
					UpdateWithIndexers("adx_communityforumthread", forumThreadIndexers);

					var forumPostToParentPageLink = new Fetch.Link
					{
						Name = "adx_communityforumthread",
						FromAttribute = "adx_communityforumthreadid",
						ToAttribute = "adx_forumthreadid",
						Alias = "adx_communityforumpost_communityforumthread",
						Links = new List<Fetch.Link>()
						{
							forumThreadToParentPageLink
						}
					};

					var forumPostIndexers = _index.GetIndexers("adx_communityforumpost",
						links: new List<Fetch.Link>() { forumPostToParentPageLink });
					UpdateWithIndexers("adx_communityforumpost", forumPostIndexers);

					// -------------------- BLOGS ------------------------------
					var blogIndexers = _index.GetIndexers("adx_blog", links: forumBlogLinks);
					UpdateWithIndexers("adx_blog", blogIndexers);

					var blogPostBlogLinks = new List<Fetch.Link>() { forumBlogToParentPageLink };
					if (languageFilter != null)
					{
						blogPostBlogLinks.Add(languageFilter);
					}

					var blogPostParentPageLink = new Fetch.Link
					{
						Name = "adx_blog",
						FromAttribute = "adx_blogid",
						ToAttribute = "adx_blogid",
						Alias = "adx_blog_blogpost",
						Links = blogPostBlogLinks
					};

					var blogPostIndexers = _index.GetIndexers("adx_blogpost", links: new List<Fetch.Link> { blogPostParentPageLink });
					UpdateWithIndexers("adx_blogpost", blogPostIndexers);
				}
			}
			else if (entityLogicalName == "adx_communityforum")
			{
				UpdateEntity("adx_communityforum", rootEntityId);

				var inForumFilterForThread = new Fetch.Filter
				{
					Type = Microsoft.Xrm.Sdk.Query.LogicalOperator.And,
					Conditions = new List<Fetch.Condition>()
					{
						new Fetch.Condition
						{
							Attribute = "adx_forumid",
							Operator = Microsoft.Xrm.Sdk.Query.ConditionOperator.Equal,
							Value = rootEntityId
						}
					}
				};

				var forumThreadIndexers = _index.GetIndexers("adx_communityforumthread", filters: new List<Fetch.Filter> { inForumFilterForThread });
				UpdateWithIndexers("adx_communityforumthread", forumThreadIndexers);

				var inForumFilterForPost = new Fetch.Link
				{
					Name = "adx_communityforumthread",
					FromAttribute = "adx_communityforumthreadid",
					ToAttribute = "adx_forumthreadid",
					Alias = "adx_communityforumpost_communityforumthread",
					Filters = new List<Fetch.Filter>()
					{
					   inForumFilterForThread
					}
				};

				var forumPostIndexers = _index.GetIndexers("adx_communityforumpost", links: new List<Fetch.Link> { inForumFilterForPost });
				UpdateWithIndexers("adx_communityforumpost", forumPostIndexers);
			}
			else if (entityLogicalName == "adx_ideaforum")
			{
				UpdateEntity("adx_ideaforum", rootEntityId);

				var inIdeaForumFilter = new Fetch.Filter
				{
					Type = Microsoft.Xrm.Sdk.Query.LogicalOperator.And,
					Conditions = new List<Fetch.Condition>()
					{
						new Fetch.Condition
						{
							Attribute = "adx_ideaforumid",
							Operator = Microsoft.Xrm.Sdk.Query.ConditionOperator.Equal,
							Value = rootEntityId
						}
					}
				};

				var ideaIndexers = _index.GetIndexers("adx_idea", filters: new List<Fetch.Filter> { inIdeaForumFilter });
				UpdateWithIndexers("adx_idea", ideaIndexers);
			}

			timer.Stop();
			ADXTrace.Instance.TraceInfo(TraceCategory.Application, string.Format("Cms Entity Tree updated for logical name: {0}, rootEntityId: {1}, timespan: {2}", entityLogicalName, rootEntityId, timer.ElapsedMilliseconds));
		}

		private void UpdateWithIndexers(string entityLogicalName, IEnumerable<ICrmEntityIndexer> indexers)
		{
			if (!indexers.Any(indexer => indexer.Indexes(entityLogicalName)))
			{
				ADXTrace.Instance.TraceInfo(TraceCategory.Application, string.Format("Application does not index entity {0}. No update performed.", entityLogicalName));

				return;
			}

			var entityIndexDocuments = indexers.SelectMany(indexer => indexer.GetDocuments()).ToArray();

			UsingWriter(MethodBase.GetCurrentMethod().Name, false, true, writer =>
			{
				foreach (var entityDoc in entityIndexDocuments)
				{
					writer.DeleteDocuments(GetEntityQuery(_index, entityLogicalName, entityDoc.PrimaryKey));
				}
			});

			int currentIndex = 0;
			while (currentIndex < entityIndexDocuments.Length)
			{
				UsingWriter(MethodBase.GetCurrentMethod().Name, false, true, writer =>
				{
					var stopwatch = new Stopwatch();
					stopwatch.Start();

					for (; currentIndex < entityIndexDocuments.Length; currentIndex++)
					{
						writer.AddDocument(entityIndexDocuments[currentIndex].Document, entityIndexDocuments[currentIndex].Analyzer);

						// We've held onto the write lock too long, there might be other updates waiting on us.
						// Release the lock so they don't time out, then re-enter the queue for the write lock.
						if (stopwatch.Elapsed.TotalSeconds > 10)
						{
							// break;
						}
					}
				});
			}
		}

		public override string ToString()
		{
			return _index.Directory.ToString();
		}

		protected virtual void UsingWriter(string description, bool create, bool optimize, Action<IndexWriter> action)
		{
			ADXTrace.Instance.TraceInfo(TraceCategory.Application, string.Format("{0}: Start", description));

			var stopwatch = new Stopwatch();

			stopwatch.Start();

			try
			{
				var retryPolicy = RetryPolicies.Create(new LockObtainTransientErrorDetectionStrategy().IsTransient, RetryPolicies.Incremental(25, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(2)));

				using (var writer = retryPolicy.Execute(() => new IndexWriter(_index.Directory, _index.Analyzer, create, IndexWriter.MaxFieldLength.UNLIMITED)))
				{
					try
					{
						ADXTrace.Instance.TraceInfo(TraceCategory.Application, string.Format("{0}: Acquired write lock, writing", description));

						action(writer);

						if (optimize)
						{
							ADXTrace.Instance.TraceInfo(TraceCategory.Application, string.Format("{0}: Optimizing index", description));

							writer.Optimize();
						}
					}
					catch (Exception e)
					{
						ADXTrace.Instance.TraceWarning(TraceCategory.Application, string.Format("{0}: Error during index write: rollback, rethrow: {1}", description, e));

						writer.Rollback();

						throw;
					}
				}

				ADXTrace.Instance.TraceInfo(TraceCategory.Application, string.Format("{0}: Index writer closed", description));
			}
			catch (Exception e)
			{
				SearchEventSource.Log.WriteError(e);

				throw;
			}
			finally
			{
				stopwatch.Stop();

				ADXTrace.Instance.TraceInfo(TraceCategory.Application, string.Format("{0}: End (Elapsed time: {1})", description, stopwatch.Elapsed));
			}
		}

		private static Query GetEntityQuery(ICrmEntityIndex index, string entityLogicalName, Guid id)
		{
			return new BooleanQuery
			{
				{ new TermQuery(new Term(index.LogicalNameFieldName, entityLogicalName)), Occur.MUST },
				{ new TermQuery(new Term(index.PrimaryKeyFieldName, id.ToString())), Occur.MUST }
			};
		}

		private class LockObtainTransientErrorDetectionStrategy
		{
			public bool IsTransient(Exception e)
			{
				return (e is LockObtainFailedException
					|| e is IOException
					|| e is UnauthorizedAccessException);
			}
		}
	}
}
