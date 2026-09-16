/*
  Copyright (c) Microsoft Corporation. All rights reserved.
  Licensed under the MIT License. See License.txt in the project root for license information.
*/

namespace Site.Areas.Ideas.Controllers
{
	using System;
	using System.Linq;
	using System.Web.Mvc;
	using System.Web;
	using Adxstudio.Xrm;
	using Adxstudio.Xrm.AspNet.Cms;
	using Microsoft.Xrm.Client;
	using Microsoft.Xrm.Client.Security;
	using Adxstudio.Xrm.Cms;
	using Adxstudio.Xrm.Core.Flighting;
	using Adxstudio.Xrm.Data;
	using Adxstudio.Xrm.Globalization;
	using Adxstudio.Xrm.Ideas;
	using Adxstudio.Xrm.Resources;
	using Adxstudio.Xrm.Services;
	using Adxstudio.Xrm.Services.Query;
	using Microsoft.Xrm.Portal.Configuration;
	using Microsoft.Xrm.Sdk;
	using Microsoft.Xrm.Sdk.Client;
	using Microsoft.Xrm.Sdk.Query;
	using Adxstudio.Xrm.Web;
	using Adxstudio.Xrm.Web.Mvc;
	using Site.Areas.Account;
	using Site.Areas.Ideas.ViewModels;
	using OrganizationServiceContextExtensions = Adxstudio.Xrm.Cms.OrganizationServiceContextExtensions;

	[PortalView, PortalSecurity]
	public class IdeasController : Controller
	{
		private const string PageNotFoundSiteMarker = "Page Not Found";

		// GET: /ideas/{ideaForumPartialUrl}/{ideaPartialUrl}
		[HttpGet]
		[LanguageActionFilter]
		public ActionResult Ideas(string ideaForumPartialUrl, string ideaPartialUrl, int? page)
		{
			var context = PortalCrmConfigurationManager.CreateServiceContext();

			if (string.IsNullOrWhiteSpace(ideaForumPartialUrl))
			{
				return GetIdeasViewOrRedirectToOnlyIdeaForum(context);
			}

			var ideaForum = GetIdeaForum(ideaForumPartialUrl, context, this.HttpContext);

			if (ideaForum == null || !Authorized(context, ideaForum))
			{
				return RedirectToPageNotFound();
			}
			
			if (string.IsNullOrWhiteSpace(ideaPartialUrl))
			{
				return GetIdeaForumView(ideaForum, page, context);
			}

			var idea = GetIdea(ideaPartialUrl, ideaForum, context);

			if (idea == null)
			{
				return RedirectToPageNotFound();
			}

			return GetIdeaView(ideaForum, idea, page);
		}

		// GET: /ideas/{ideaForumPartialUrl}/filter/{filter}/{timeSpan}/{status}
		[HttpGet]
		public ActionResult Filter(string ideaForumPartialUrl, string filter, string timeSpan, int? status, int? page)
		{
			var context = PortalCrmConfigurationManager.CreateServiceContext();

			if (string.IsNullOrWhiteSpace(ideaForumPartialUrl))
			{
				return RedirectToAction("Ideas");
			}

			var ideaForum = GetIdeaForum(ideaForumPartialUrl, context, this.HttpContext);

			if (ideaForum == null || !Authorized(context, ideaForum))
			{
				return RedirectToAction("Ideas");
			}

			page = page ?? 1;

			return GetIdeaForumView(ideaForum, page, context, filter, timeSpan, status);
		}

		private static bool Authorized(OrganizationServiceContext context, Entity entity)
		{
			var securityProvider = PortalCrmConfigurationManager.CreateCrmEntitySecurityProvider();

			return securityProvider.TryAssert(context, entity, CrmEntityRight.Read);
		}

		private static Entity GetIdea(string ideaPartialUrl, Entity ideaForum, OrganizationServiceContext serviceContext)
		{
			var fetch = new Fetch
			{
				Entity = new FetchEntity("adx_idea")
				{
					Filters = new[] 
					{
						new Adxstudio.Xrm.Services.Query.Filter
						{
							Conditions = new[]
							{
								new Condition("adx_ideaforumid", ConditionOperator.Equal, ideaForum.Id),
								new Condition("adx_partialurl", ConditionOperator.Equal, ideaPartialUrl),
								new Condition("adx_approved", ConditionOperator.Equal, true),
								new Condition("statecode", ConditionOperator.Equal, 0)
							}
						}
					}
				}
			};

			var idea = serviceContext.RetrieveSingle(fetch);

			return idea;
		}

		private static Entity GetIdeaForum(string ideaForumPartialUrl, OrganizationServiceContext serviceContext, HttpContextBase httpContext)
		{
			var website = httpContext.GetWebsite();

			var filter = new Adxstudio.Xrm.Services.Query.Filter
			{
				Conditions = new[]
				{
					new Condition("adx_websiteid", ConditionOperator.Equal, website.Id),
					new Condition("adx_partialurl", ConditionOperator.Equal, ideaForumPartialUrl),
					new Condition("statecode", ConditionOperator.Equal, 0)
				}
			};

			var languageInfo = httpContext.GetContextLanguageInfo();
			if (languageInfo.IsCrmMultiLanguageEnabled)
			{
				filter.Filters = new[]
				{
					new Adxstudio.Xrm.Services.Query.Filter
					{
						Type = LogicalOperator.Or,
						Conditions = new[]
						{
							new Condition("adx_websitelanguageid", ConditionOperator.Null),
							new Condition("adx_websitelanguageid", ConditionOperator.Equal, languageInfo.ContextLanguage.EntityReference.Id)
						}
					}
				};
			}
			
			var fetch = new Fetch
			{
				Entity = new FetchEntity("adx_ideaforum")
				{
					Filters = new[] { filter }
				}
			};

			var ideaForum = serviceContext.RetrieveSingle(fetch);

			return ideaForum;
		}

		private ActionResult GetIdeaForumView(Entity ideaForum, int? page, OrganizationServiceContext context, string filter = null, string timeSpan = null, int? status = 1)
		{
			IIdeaForumDataAdapter ideaForumDataAdapter = IdeaForumDataAdapterFactory.Instance.CreateIdeaForumDataAdapter(ideaForum, filter, timeSpan, status);

			var currentIdeaForum = ideaForumDataAdapter.Select();
			if (currentIdeaForum == null)
			{
				return RedirectToPageNotFound();
			}

			var ideaForumViewModel = new IdeaForumViewModel
			{
				IdeaForum = currentIdeaForum,
				Ideas = new PaginatedList<IIdea>(page, ideaForumDataAdapter.SelectIdeaCount(), ideaForumDataAdapter.SelectIdeas)
			};

			foreach (var idea in ideaForumViewModel.Ideas)
			{
				idea.Url = context.GetUrl(idea.Entity);
			}

			// Set label for status displayed in dropdown
			ideaForumViewModel.CurrentStatusLabel = status == null ?
				currentIdeaForum.IdeaStatusOptionSetMetadata.Options.Where(o => o.Value == (int?)IdeaStatus.New).FirstOrDefault().Label.GetLocalizedLabelString().ToLower() :
				status == (int?)IdeaStatus.Any ? ResourceManager.GetString("IdeaForum_Any") :
				currentIdeaForum.IdeaStatusOptionSetMetadata.Options.Where(o => o.Value == status).FirstOrDefault().Label.GetLocalizedLabelString().ToLower();

			if (FeatureCheckHelper.IsFeatureEnabled(FeatureNames.TelemetryFeatureUsage))
			{
				PortalFeatureTrace.TraceInstance.LogFeatureUsage(FeatureTraceCategory.Idea, this.HttpContext, "read_idea_forum", ideaForumViewModel.Ideas.Count(), ideaForum.ToEntityReference(), "read");
			}

			return View("IdeaForum", ideaForumViewModel);
		}

		private ActionResult GetIdeasViewOrRedirectToOnlyIdeaForum(OrganizationServiceContext context)
		{
			var websiteDataAdapter = new WebsiteDataAdapter();

			var ideaForums = websiteDataAdapter.SelectIdeaForums().ToArray();

			var ideaForumCount = websiteDataAdapter.SelectIdeaForumCount();

			foreach (var ideaForum in ideaForums)
			{
				ideaForum.Url = context.GetUrl(ideaForum.Entity);
			}

			if (ideaForums.Count() == 1)
			{
				return this.RedirectToAction("Ideas", new { ideaForumPartialUrl = ideaForums.First().Url });
			}

			var ideasViewModel = new IdeasViewModel
			{
				IdeaForums = ideaForums,
				IdeaForumCount = ideaForumCount
			};

			if (FeatureCheckHelper.IsFeatureEnabled(FeatureNames.TelemetryFeatureUsage))
			{
				PortalFeatureTrace.TraceInstance.LogFeatureUsage(FeatureTraceCategory.Idea, this.HttpContext, "read_ideas_forum", string.Empty, ideaForumCount, "adx_ideaforum", "read");
			}

			return View("Ideas", ideasViewModel);
		}

		private ActionResult GetIdeaView(Entity adxIdeaForum, Entity adxIdea, int? page)
		{
			var ideaDataAdapter = new IdeaDataAdapter(adxIdea) { ChronologicalComments = true };

			var idea = ideaDataAdapter.Select();
			var comments = FeatureCheckHelper.IsFeatureEnabled(FeatureNames.Feedback)
				? new PaginatedList<IComment>(page, ideaDataAdapter.SelectCommentCount(), ideaDataAdapter.SelectComments)
				: null;

			var currentIdeaForum = new IdeaForumDataAdapter(adxIdeaForum).Select();
			if (currentIdeaForum == null)
			{
				return RedirectToPageNotFound();
			}

			var ideaViewModel = new IdeaViewModel
			{
				IdeaForum = currentIdeaForum,
				Idea = idea,
				Comments = new IdeaCommentsViewModel { Comments = comments, Idea = idea }
			};

			if (FeatureCheckHelper.IsFeatureEnabled(FeatureNames.TelemetryFeatureUsage))
			{
				PortalFeatureTrace.TraceInstance.LogFeatureUsage(FeatureTraceCategory.Idea, this.HttpContext, "read_idea", 1, idea.Entity.ToEntityReference(), "read");
				PortalFeatureTrace.TraceInstance.LogFeatureUsage(FeatureTraceCategory.Idea, this.HttpContext, "read_comment_idea", idea.CommentCount, idea.Entity.ToEntityReference(), "read");
			}

			return View("Idea", ideaViewModel);
		}

		private ActionResult RedirectToPageNotFound()
		{
			var context = PortalCrmConfigurationManager.CreatePortalContext();

			var page = context.ServiceContext.GetPageBySiteMarkerName(context.Website, PageNotFoundSiteMarker);

			if (page == null)
			{
				throw new Exception("Please contact your system administrator. The required site marker {0} is missing.".FormatWith(PageNotFoundSiteMarker));
			}

			var path = OrganizationServiceContextExtensions.GetUrl(context.ServiceContext, page);

			if (path == null)
			{
				throw new Exception("Please contact your System Administrator. Unable to build URL for Site Marker {0}.".FormatWith(PageNotFoundSiteMarker));
			}

			return Redirect(path);
		}
	}
}
