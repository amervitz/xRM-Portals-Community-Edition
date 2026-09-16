/*
  Copyright (c) Microsoft Corporation. All rights reserved.
  Licensed under the MIT License. See License.txt in the project root for license information.
*/

namespace Site.Areas.KnowledgeManagement.Controllers
{
	using System;
	using System.Web.Mvc;
	using System.Collections.Specialized;
	using System.Diagnostics;
	using System.Linq;
	using System.Web;
	using System.Web.Routing;

	using Adxstudio.Xrm;
	using Adxstudio.Xrm.AspNet.Cms;
	using Adxstudio.Xrm.Cms;
	using Adxstudio.Xrm.ContentAccess;
	using Adxstudio.Xrm.Core.Flighting;
	using Adxstudio.Xrm.Data;
	using Adxstudio.Xrm.KnowledgeArticles;
	using Adxstudio.Xrm.Resources;
	using Adxstudio.Xrm.Security;
	using Adxstudio.Xrm.Services.Query;
	using Adxstudio.Xrm.Text;
	using Adxstudio.Xrm.Web.Mvc;
	using Microsoft.Xrm.Client;
	using Microsoft.Xrm.Portal.Configuration;
	using Microsoft.Xrm.Sdk;
	using Microsoft.Xrm.Sdk.Client;
	using Site.Areas.KnowledgeManagement.ViewModels;
	using Adxstudio.Xrm.Services;
	using Adxstudio.Xrm.Web;
	using Microsoft.Xrm.Portal.Web;
	using Microsoft.Xrm.Sdk.Query;
	using OrganizationServiceContextExtensions = Adxstudio.Xrm.Cms.OrganizationServiceContextExtensions;

	[PortalView, PortalSecurity]
	public class ArticleController : Controller
	{
		private const string ArticlesFetchXmlFormat = @"
			<fetch mapping='logical'>
				<entity name='knowledgearticle'>
					<attribute name='articlepublicnumber' />
					<attribute name='knowledgearticleid' />
					<attribute name='title' />
					<attribute name='keywords' />
					<attribute name='createdon' />
					<attribute name='statecode' />
					<attribute name='statuscode' />
					<attribute name='isinternal' />
					<attribute name='isrootarticle' />
					<attribute name='knowledgearticleviews' />
					<attribute name='languagelocaleid' />
					<link-entity name='languagelocale' from='languagelocaleid' to='languagelocaleid' visible='false' link-type='outer'  alias='language_locale'>
						<attribute name='localeid' />
						<attribute name='code' />
						<attribute name='region' />
						<attribute name='name' />
						<attribute name='language' />
					</link-entity>
					<filter type='and'>
						<condition attribute='isrootarticle' operator='eq' value='0' />
						<condition attribute='statecode' operator='eq' value='{0}' />
						<condition attribute='isinternal' operator='eq' value='0' />
						<condition attribute='articlepublicnumber' operator='eq' value='{1}' />
						{2}
					</filter>
				</entity>
			</fetch>";

		/// <summary>The timespan to keep in cache.</summary>
		private static readonly TimeSpan DefaultDuration = TimeSpan.FromHours(1);

		[HttpGet]
		public ActionResult Article(string number, string lang, int? page)
		{
			var serviceContext = PortalCrmConfigurationManager.CreateServiceContext();

			// If the article is specifically being requested (via URL) in a language, multi-language is enabled, and the requested language
			// is different than the context language, then update the context language to respect the language of the article being viewed.
			// remove the old lang parameter and update url if necessary
			var contextLanguageInfo = this.HttpContext.GetContextLanguageInfo();
			if (contextLanguageInfo.IsCrmMultiLanguageEnabled)
			{
				var needsRedirect = ContextLanguageInfo.DisplayLanguageCodeInUrl != contextLanguageInfo.RequestUrlHasLanguageCode
									|| contextLanguageInfo.ContextLanguage.UsedAsFallback || !string.IsNullOrWhiteSpace(lang);

				var llcc = !string.IsNullOrWhiteSpace(lang) ? lang : contextLanguageInfo.ContextLanguage.Code;
				var activeLangauges = contextLanguageInfo.ActiveWebsiteLanguages;
				if (!string.IsNullOrWhiteSpace(lang) && !activeLangauges.Any(l => l.Code.Equals(lang, StringComparison.InvariantCultureIgnoreCase)))
				{
					IWebsiteLanguage language;
					if (ContextLanguageInfo.TryGetLanguageFromMapping(contextLanguageInfo.ActiveWebsiteLanguages, lang, out language))
					{
						llcc = language.UsedAsFallback && contextLanguageInfo.ContextLanguage.CrmLcid == language.CrmLcid
									? contextLanguageInfo.ContextLanguage.Code
									: language.Code;
					}
				}

				var articleUrl = ContextLanguageInfo.DisplayLanguageCodeInUrl
									? contextLanguageInfo.FormatUrlWithLanguage(overrideLanguageCode: llcc)
									: contextLanguageInfo.AbsolutePathWithoutLanguageCode;


				var queryParameters = new NameValueCollection(this.Request.QueryString);
				articleUrl = articleUrl.Replace(queryParameters.Count == 1 ?
					string.Format("?lang={0}", lang) : string.Format("lang={0}", lang),
					string.Empty);

				if (needsRedirect && articleUrl != Request.Url.PathAndQuery)
				{
					return Redirect(articleUrl);
				}
			}

			string langCode;
			var article = GetArticle(serviceContext, number, this.HttpContext.GetWebsite(), lang, out langCode);

			if (article == null)
			{
				return View("ArticleUnavailable");
			}

			if (!Authorized(serviceContext, article))
			{
				return RedirectToAccessDeniedPage();
			}

			return GetArticleView(article, page, langCode);
		}

		[HttpPost, ValidateInput(false)]
		[ValidateAntiForgeryToken]
		public ActionResult CommentCreate(Guid id, string authorName, string authorEmail, string copy)
		{
			if (!FeatureCheckHelper.IsFeatureEnabled(FeatureNames.Feedback))
			{
				return new EmptyResult();
			}

			var context = PortalCrmConfigurationManager.CreateServiceContext();

			var article = context.RetrieveSingle("knowledgearticle",
				FetchAttribute.All,
				new Condition("knowledgearticleid", ConditionOperator.Equal, id),
				false,
				false,
				RequestFlag.AllowStaleData);

			if (article == null || !Authorized(context, article))
			{
				return new EmptyResult();
			}

			var articleDataAdapter = new KnowledgeArticleDataAdapter(article) { ChronologicalComments = true };

			var sanitizedCopy = SafeHtml.SafeHtmSanitizer.GetSafeHtml(copy ?? string.Empty);

			TryAddComment(articleDataAdapter, authorName, authorEmail, sanitizedCopy);

			var commentsViewModel = new ArticleCommentsViewModel()
			{
				Comments =
					new PaginatedList<IComment>(PaginatedList.Page.Last, articleDataAdapter.SelectCommentCount(),
						articleDataAdapter.SelectComments),
				KnowledgeArticle = articleDataAdapter.Select()
			};
			RouteData.Values["action"] = "Article";
			RouteData.Values["id"] = Guid.Empty;
			return PartialView("Comments", commentsViewModel);
		}

		[HttpPost, ValidateInput(false)]
		[AjaxValidateAntiForgeryToken]
		public ActionResult RatingCreate(Guid id, int rating, int maxRating, int minRating)
		{
			if (!FeatureCheckHelper.IsFeatureEnabled(FeatureNames.Feedback))
			{
				return new EmptyResult();
			}

			var context = PortalCrmConfigurationManager.CreateServiceContext();

			var article = context.RetrieveSingle("knowledgearticle",
				FetchAttribute.All,
				new Condition("knowledgearticleid", ConditionOperator.Equal, id),
				false,
				false,
				RequestFlag.AllowStaleData);

			if (article == null || !Authorized(context, article))
			{
				return new EmptyResult();
			}

			var articleDataAdapter = new KnowledgeArticleDataAdapter(article);

			TryAddUpdateRating(articleDataAdapter, rating, maxRating, minRating);

			var commentsViewModel = new ArticleCommentsViewModel()
			{
				KnowledgeArticle = articleDataAdapter.Select()
			};

			return PartialView("Rating", commentsViewModel.KnowledgeArticle);
		}

		[HttpGet]
		public int GetArticleViewCount(Guid id)
		{

			var service = System.Web.HttpContext.Current.GetOrganizationService();
			var entity = service.RetrieveSingle(
				new EntityReference("knowledgearticle", id), 
				new ColumnSet("knowledgearticleviews"),
				RequestFlag.AllowStaleData | RequestFlag.SkipDependencyCalculation,
				DefaultDuration);

			return entity.Attributes.Contains("knowledgearticleviews") ? entity.Attributes["knowledgearticleviews"] as int? ?? 0 : 0;
		}

		[HttpGet]
		public decimal GetArticleRating(Guid id)
		{
			var service = System.Web.HttpContext.Current.GetOrganizationService();
			var entity = service.RetrieveSingle(
				new EntityReference("knowledgearticle", id),
				new ColumnSet("rating"),
				RequestFlag.AllowStaleData | RequestFlag.SkipDependencyCalculation,
				DefaultDuration);

			return entity.Attributes.Contains("rating") ? entity.Attributes["rating"] as decimal? ?? 0 : 0;
		}

		[HttpPost, ValidateInput(false)]
		[AjaxValidateAntiForgeryToken]
		public void CaseDeflectionCreate(Guid id, bool isRatingEnabled, string searchText)
		{
			var context = PortalCrmConfigurationManager.CreateServiceContext();

			var article = context.RetrieveSingle("knowledgearticle",
				FetchAttribute.All,
				new Condition("knowledgearticleid", ConditionOperator.Equal, id),
				false,
				false,
				RequestFlag.AllowStaleData);

			var articleDataAdapter = new KnowledgeArticleDataAdapter(article);
			articleDataAdapter.CreateUpdateCaseDeflection(article.Attributes["title"].ToString(), searchText, isRatingEnabled, context);
		}

		[HttpPost, ValidateInput(false)]
		[AjaxValidateAntiForgeryToken]
		public void IncrementViewCount(Guid id, Uri urlReferrer)
		{
			var articleEntity = new Entity("knowledgearticle", id);
			var articleDataAdapter = new KnowledgeArticleDataAdapter(articleEntity) { ChronologicalComments = true };
			articleDataAdapter.IncrementKnowledgeArticleViewCount(urlReferrer);
		}

		public static string GetPortalUri(string siteMarkerName)
		{
			var portalContext = PortalCrmConfigurationManager.CreatePortalContext();
			var page = portalContext.ServiceContext.GetPageBySiteMarkerName(portalContext.Website, siteMarkerName);
			return page == null ? string.Empty : new UrlBuilder(portalContext.ServiceContext.GetUrl(page)).Path;
		}

		private ActionResult RedirectToAccessDeniedPage()
		{
			var serviceContext = PortalCrmConfigurationManager.CreatePortalContext();

			var page = serviceContext.ServiceContext.GetPageBySiteMarkerName(serviceContext.Website, AccessDeniedSiteMarker);

			if (page == null)
			{
				throw new Exception(
					ResourceManager.GetString("Contact_System_Administrator_Required_Site_Marker_Missing_Exception")
						.FormatWith(PageNotFoundSiteMarker));
			}

			var path = OrganizationServiceContextExtensions.GetUrl(serviceContext.ServiceContext, page);

			if (path == null)
			{
				throw new Exception("Please contact your System Administrator. Unable to build URL for Site Marker {0}.".FormatWith(PageNotFoundSiteMarker));
			}

			return Redirect(path);
		}

		private ActionResult GetArticleView(Entity articleEntity, int? page, string code)
		{
			var articleViewModel = new ArticleViewModel(articleEntity, page, code);

			if (FeatureCheckHelper.IsFeatureEnabled(FeatureNames.TelemetryFeatureUsage))
			{
				PortalFeatureTrace.TraceInstance.LogFeatureUsage(FeatureTraceCategory.KnowledgeArticle, this.HttpContext, "read_article", 1, articleEntity.ToEntityReference(), "read");
			}

			return View("Article", articleViewModel);
		}

		private bool TryAddComment(IKnowledgeArticleDataAdapter dataAdapter, string authorName, string authorEmail, string content)
		{
			if (!Request.IsAuthenticated)
			{
				if (string.IsNullOrWhiteSpace(authorName))
				{
					ModelState.AddModelError("authorName", ResourceManager.GetString("Name_Required_Error"));
				}

				if (string.IsNullOrWhiteSpace(authorEmail))
				{
					ModelState.AddModelError("authorEmail", ResourceManager.GetString("Email_Required_Error"));
				}
			}

			if (string.IsNullOrWhiteSpace(content) || string.IsNullOrWhiteSpace(StringHelper.GetCommentTitleFromContent(content)))
			{
				ModelState.AddModelError("content", ResourceManager.GetString("Comment_Required_Error"));
			}

			if (!ModelState.IsValid)
			{
				return false;
			}

			dataAdapter.CreateComment(content, authorName, authorEmail);

			return true;
		}

		private bool TryAddUpdateRating(IKnowledgeArticleDataAdapter dataAdapter, int rating, int maxRating, int minRating)
		{
			if (!ModelState.IsValid)
			{
				return false;
			}

			dataAdapter.CreateUpdateRating(rating, maxRating, minRating, HttpContext.Profile.UserName);

			return true;
		}

		private static bool Authorized(OrganizationServiceContext serviceContext, Entity entity)
		{
			var entityPermissionProvider = new CrmEntityPermissionProvider();

			return entityPermissionProvider.TryAssert(serviceContext, CrmEntityPermissionRight.Read, entity);
		}

		private static Entity GetArticle(OrganizationServiceContext serviceContext, string number, CrmWebsite website, string lang, out string languageLocaleCode)
		{
			const int published = 3;
			var portalContext = PortalCrmConfigurationManager.CreatePortalContext();
			languageLocaleCode = lang;

			// If language locale code is NOT provided and multi-language is enabled, then use the context website language.
			var contextLanguageInfo = System.Web.HttpContext.Current.GetContextLanguageInfo();
			if (contextLanguageInfo.IsCrmMultiLanguageEnabled && string.IsNullOrWhiteSpace(languageLocaleCode))
			{
				languageLocaleCode = contextLanguageInfo.ContextLanguage.Code;
			}
			// If language locale code is NOT provided and we're not using multi-language, fall back to site setting.
			else if (string.IsNullOrWhiteSpace(languageLocaleCode))
			{
				languageLocaleCode = portalContext.ServiceContext.GetSiteSettingValueByName(portalContext.Website,
					"KnowledgeManagement/Article/Language");
			}

			var optionalLanguageCondition = string.IsNullOrWhiteSpace(languageLocaleCode) ? string.Empty : string.Format("<condition entityname='language_locale' attribute='code' operator='eq' value = '{0}' />", languageLocaleCode);
			var articlesFetchXml = string.Format(ArticlesFetchXmlFormat, published, number, optionalLanguageCondition);
			var fetchArticles = Fetch.Parse(articlesFetchXml);

			var settings = website.Settings;
			var productFilteringOn = settings.Get<bool>(ProductFilteringSiteSettingName);
			var calFilteringOn = settings.Get<bool>(CalEnabledSiteSettingName);

			if (calFilteringOn)
			{
				// Apply CAL filtering
				var contentAccessLevelProvider = new ContentAccessLevelProvider();
				contentAccessLevelProvider.TryApplyRecordLevelFiltersToFetch(CrmEntityPermissionRight.Read, fetchArticles);
			}

			if (productFilteringOn)
			{
				// Apply Product filtering
				var productAccessProvider = new ProductAccessProvider();
				productAccessProvider.TryApplyRecordLevelFiltersToFetch(CrmEntityPermissionRight.Read, fetchArticles);
			}

			var article = serviceContext.RetrieveSingle(fetchArticles, false, false, RequestFlag.AllowStaleData);

			return article;
		}

		private const string PageNotFoundSiteMarker = "Page Not Found";

		private const string AccessDeniedSiteMarker = "Access Denied";

		private const string ProductFilteringSiteSettingName = "ProductFiltering/Enabled";

		private const string CalEnabledSiteSettingName = "KnowledgeManagement/ContentAccessLevel/Enabled";
	}
}
