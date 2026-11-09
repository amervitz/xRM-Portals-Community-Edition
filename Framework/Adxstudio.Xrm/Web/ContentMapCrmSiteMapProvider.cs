/*
  Copyright (c) Microsoft Corporation. All rights reserved.
  Licensed under the MIT License. See License.txt in the project root for license information.
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Web;
using System.Web.Routing;
using Adxstudio.Xrm.AspNet.Cms;
using Adxstudio.Xrm.Cms;
using Adxstudio.Xrm.Configuration;
using Adxstudio.Xrm.Performance;
using Adxstudio.Xrm.Resources;
using Adxstudio.Xrm.Web.Mvc;
using Adxstudio.Xrm.Web.Providers;
using Microsoft.Xrm.Client;
using Microsoft.Xrm.Client.Metadata;
using Microsoft.Xrm.Portal.Configuration;
using Microsoft.Xrm.Portal.Web;
using Microsoft.Xrm.Portal.Web.Providers;
using Microsoft.Xrm.Sdk;

namespace Adxstudio.Xrm.Web
{
	public class ContentMapCrmSiteMapProvider : CrmSiteMapProvider
	{
		public const string AccessDeniedPageSiteMarkerName = "Access Denied";
		public const string NotFoundPageSiteMarkerName = "Page Not Found";

		public override SiteMapNode FindSiteMapNode(string rawUrl)
		{
			var rawUrlWithoutLanguage = GetRawUrlWithoutLanguage(rawUrl);

			ADXTrace.Instance.TraceInfo(TraceCategory.Monitoring, "FindSiteMapNode: (0)ENTER");

			return CachePerRequest("FindSiteMapNode", rawUrlWithoutLanguage, () => GetContentMapProvider().Using(map => FindSiteMapNode(rawUrlWithoutLanguage, map)));
		}

		public virtual SiteMapNode FindSiteMapNodeWithoutSecurityValidation(string rawUrl)
		{
			ADXTrace.Instance.TraceInfo(TraceCategory.Monitoring, "FindSiteMapNode: (0)ENTER");

			return GetContentMapProvider().Using(map => FindSiteMapNode(GetRawUrlWithoutLanguage(rawUrl), map, excludeFromSecurityValidation: true));
		}

		private string GetRawUrlWithoutLanguage(string rawUrl)
		{
			var rawUrlWithoutLanguage = rawUrl;

			// Strip out language code from the path url because partial urls in the system don't have language code in them.
			var contextLanguageInfo = HttpContext.Current.GetContextLanguageInfo();
			if (contextLanguageInfo.IsCrmMultiLanguageEnabled && ContextLanguageInfo.DisplayLanguageCodeInUrl)
			{
				rawUrlWithoutLanguage = contextLanguageInfo.StripLanguageCodeFromAbsolutePath(rawUrl);
			}

			return rawUrlWithoutLanguage;
		}

		private SiteMapNode FindSiteMapNode(string rawUrl, ContentMap map, int counter = 0, bool excludeFromSecurityValidation = false)
		{
			using (PerformanceProfiler.Instance.StartMarker(PerformanceMarkerName.SiteMapProvider, PerformanceMarkerArea.Cms, PerformanceMarkerTagName.FindSiteMapNode))
			{
				counter++;
				WebsiteNode site;
				IContentMapEntityUrlProvider urlProvider;

				if (!TryGetWebsite(map, out site, out urlProvider))
				{
					ADXTrace.Instance.TraceInfo(TraceCategory.Monitoring, "FindSiteMapNode: (1)cannot find WebsiteNode");
					return base.FindSiteMapNode(rawUrl);
				}

				var httpContext = HttpContext.Current;
				string currentNodeUrl;

				// Allow override of current site map node using special route param.
				if (httpContext != null
					&& httpContext.Request.RawUrl == rawUrl
					&& TryGetCurrentNodeUrlFromRouteData(httpContext, out currentNodeUrl)
					&& currentNodeUrl != rawUrl && counter < 5000)
				{
					ADXTrace.Instance.TraceInfo(TraceCategory.Monitoring, "FindSiteMapNode: (2)override url");
					return FindSiteMapNode(currentNodeUrl, map, counter, excludeFromSecurityValidation);
				}

				var clientUrl = ExtractClientUrlFromRawUrl(rawUrl);
				
				// Find any possible SiteMarkerRoutes (or other IPortalContextRoutes) that match this path.
				string routeMatch = RouteTable.Routes.GetPortalContextPath(map, site, clientUrl.Path);
				var contextPath = routeMatch ?? clientUrl.Path;
				if (routeMatch != null)
				{
					ADXTrace.Instance.TraceInfo(TraceCategory.Monitoring, "FindSiteMapNode: (3)FOUND route");
				}

				var languageContext = httpContext.GetContextLanguageInfo();
				// If the URL matches a web page, try to look up that page and return a node.
				var mappingResult = ContentMapUrlMapping.LookupPageByUrlPath(site, contextPath, ContentMapUrlMapping.WebPageLookupOptions.LanguageContentOnly, languageContext);

				if (mappingResult.Node != null && mappingResult.IsUnique)
				{
					ADXTrace.Instance.TraceInfo(TraceCategory.Monitoring, "FindSiteMapNode: (4)FOUND PAGE");
					return GetAccessibleNodeOrAccessDeniedNode(map, site, mappingResult.Node, urlProvider, excludeFromSecurityValidation);
				}
				else if (!mappingResult.IsUnique)
				{
					return GetNotFoundNode(map, site, urlProvider);
				}

				// If the URL matches a web file, try to look up that file and return a node.
				var file = ContentMapUrlMapping.LookupFileByUrlPath(site, clientUrl.Path, languageContext);

				if (file != null)
				{
					ADXTrace.Instance.TraceInfo(TraceCategory.Monitoring, "FindSiteMapNode: (5)FOUND FILE");
					return GetAccessibleNodeOrAccessDeniedNode(map, site, file, urlProvider, excludeFromSecurityValidation);
				}

				// If there is a pageid Guid on the querystring, try to look up a web page by
				// that ID and return a node.
				Guid pageid;

				if (TryParseGuid(clientUrl.QueryString["pageid"], out pageid))
				{
					WebPageNode pageById;

					if (map.TryGetValue(new EntityReference("adx_webpage", pageid), out pageById))
					{
						ADXTrace.Instance.TraceInfo(TraceCategory.Monitoring, "FindSiteMapNode: (6)FOUND pageID");
						return GetAccessibleNodeOrAccessDeniedNode(map, site, pageById, urlProvider, excludeFromSecurityValidation);
					}
				}

				// If the above lookups failed, try find a node in any other site map providers.
				foreach (SiteMapProvider subProvider in SiteMap.Providers)
				{
					// Skip this provider if it is the same as this one.
					if (subProvider.Name == Name) continue;

					// Check if the provider has solution dependencies
					var solutionDependent = subProvider as ISolutionDependent;

					if (solutionDependent != null)
					{
						if (map.Solution.Solutions.Intersect(solutionDependent.RequiredSolutions).Count() != solutionDependent.RequiredSolutions.Count())
						{
							continue;
						}
					}

					var node = subProvider.FindSiteMapNode(clientUrl.PathWithQueryString);

					if (node != null)
					{
						ADXTrace.Instance.TraceInfo(TraceCategory.Monitoring, "FindSiteMapNode: (7)FOUND other provider");
						return node;
					}
				}

				ADXTrace.Instance.TraceInfo(TraceCategory.Monitoring, "FindSiteMapNode: (8)NOT FOUND");
				return GetNotFoundNode(map, site, urlProvider);
			}
		}

		public override SiteMapNode GetParentNode(SiteMapNode node)
		{
			var parent = GetParentNodeInternal(node);

			if (parent != null)
			{
				ADXTrace.Instance.TraceInfo(TraceCategory.Application, "ParentNode");

				if ((node.Key == parent.Key) || (node.Url == "/"))
				{
					throw new InvalidOperationException("Web page parent cannot be set to self.");
				}
			}

			return parent;
		}

		private SiteMapNode GetParentNodeInternal(SiteMapNode node)
		{
			return CachePerRequest("GetParentNode", node.Key, () => GetContentMapProvider().Using(map => GetParentNode(node, map)));
		}

		private SiteMapNode GetParentNode(SiteMapNode node, ContentMap map)
		{
			WebsiteNode site;
			IContentMapEntityUrlProvider urlProvider;
			using (PerformanceProfiler.Instance.StartMarker(PerformanceMarkerName.SiteMapProvider, PerformanceMarkerArea.Cms, PerformanceMarkerTagName.GetParentNodes))
			{
				if (!TryGetWebsite(map, out site, out urlProvider))
				{
					return base.GetParentNode(node);
				}

				ADXTrace.Instance.TraceInfo(TraceCategory.Application, string.Format("key={0}", node.Key));

				// When searching for parent page, it doesn't matter if we look for root or content page, both will have the same parent page.
				var languageContext = HttpContext.Current.GetContextLanguageInfo();
				var mappingResult = ContentMapUrlMapping.LookupPageByUrlPath(site, node.Url, ContentMapUrlMapping.WebPageLookupOptions.Any, languageContext);
				
				// Ignore IsUnique to find any avaiable parent node
				if (mappingResult.Node == null || mappingResult.Node.Parent == null || mappingResult.Node.Parent.IsReference)
				{
					return null;
				}

				return ReturnNodeIfAccessible(GetNode(map, mappingResult.Node.Parent, urlProvider), () => null);
			}
		}

		public override SiteMapNodeCollection GetChildNodes(SiteMapNode node)
		{
			var children = GetChildNodesInternal(node);

			if (children != null)
			{
				var value = string.Join(",", children.OfType<SiteMapNode>().Select(n => n.Url));
				ADXTrace.Instance.TraceInfo(TraceCategory.Application, "ChildNodes");
			}

			return children;
		}

		private SiteMapNodeCollection GetChildNodesInternal(SiteMapNode node)
		{
			return CachePerRequest("GetChildNodes", node.Key, () => GetContentMapProvider().Using(map => GetChildNodes(node, map)));
		}

		private SiteMapNodeCollection GetChildNodes(SiteMapNode node, ContentMap map)
		{
			WebsiteNode site;
			IContentMapEntityUrlProvider urlProvider;

			using (PerformanceProfiler.Instance.StartMarker(PerformanceMarkerName.SiteMapProvider, PerformanceMarkerArea.Cms, PerformanceMarkerTagName.GetChildNodes))
			{
				if (!TryGetWebsite(map, out site, out urlProvider))
				{
					return base.GetChildNodes(node);
				}

				var children = new List<SiteMapNode>();

				// Shorcuts do not have children, may have the same Url as a web page.
				if (IsShortcutNode(node))
				{
					return new SiteMapNodeCollection();
				}

				// SiteMap is not language-aware, so the node.Url will always be URL of the root WebPage, so look for root.
				var langContext = HttpContext.Current.GetContextLanguageInfo();
				var filterResult = ContentMapUrlMapping.LookupPageByUrlPath(site, node.Url, ContentMapUrlMapping.WebPageLookupOptions.RootOnly, langContext);

				if (filterResult.Node != null && filterResult.IsUnique)
				{
					var portal = PortalContext;
					var context = portal.ServiceContext;

					foreach (var child in filterResult.Node.WebPages)
					{
						// Only get children pages who match the current active language.
						var childNode = IsValidLanguageContentPage(child, langContext) ? GetNode(map, child, HttpStatusCode.OK, urlProvider) : null;

						if (childNode == null)
						{
							continue;
						}

						if (ChildNodeValidator.Validate(context, childNode))
						{
							children.Add(childNode);
						}
					}

					foreach (var file in filterResult.Node.WebFiles)
					{
						var childNode = GetNode(map, file, urlProvider);

						if (ChildNodeValidator.Validate(context, childNode))
						{
							children.Add(childNode);
						}
					}

					foreach (var shortcut in filterResult.Node.Shortcuts)
					{
						var childNode = GetNode(map, shortcut, urlProvider);

						if (childNode != null && ChildNodeValidator.Validate(context, childNode))
						{
							children.Add(childNode);
						}
					}
				}

				// Append values from other site map providers.
				foreach (SiteMapProvider subProvider in SiteMap.Providers)
				{
					// Skip this provider if it is the same as this one.
					if (subProvider.Name == Name) continue;

					// Check if the provider has solution dependencies
					var solutionDependent = subProvider as ISolutionDependent;

					if (solutionDependent != null)
					{
						if (map.Solution.Solutions.Intersect(solutionDependent.RequiredSolutions).Count() != solutionDependent.RequiredSolutions.Count())
						{
							continue;
						}
					}

					var subProviderChildNodes = subProvider.GetChildNodes(node);

					if (subProviderChildNodes == null) continue;

					foreach (SiteMapNode childNode in subProviderChildNodes)
					{
						children.Add(childNode);
					}
				}

				children.Sort(new SiteMapNodeDisplayOrderComparer());

				return new SiteMapNodeCollection(children.ToArray());
			}
		}

		protected override SiteMapNode GetRootNodeCore()
		{
			return CachePerRequest("GetRootNodeCore", () =>
			{
				var urlProvider = PortalCrmConfigurationManager.CreateDependencyProvider(PortalName).GetDependency<IEntityUrlProvider>() as ContentMapEntityUrlProvider;

				if (urlProvider == null)
				{
					return base.GetRootNodeCore();
				}

				var portal = PortalContext;
				var website = portal.Website;

				var contentMapProvider = GetContentMapProvider();

				return contentMapProvider.Using(map =>
				{
					WebsiteNode site;

					if (map.TryGetValue(website, out site))
					{
						var homeSiteMarker = site.SiteMarkers.FirstOrDefault(sm => sm.Name == "Home");
						var homePage = homeSiteMarker != null ? homeSiteMarker.WebPage : null;

						if (homePage == null)
						{
							CmsEventSource.Log.HomeSiteMarkerNotFound(site);

							throw new InvalidOperationException("Website {0} must have a web page with the site marker Home.".FormatWith(site.Name));
						}

						return GetAccessibleNodeOrAccessDeniedNode(map, site, homePage, urlProvider);
					}

					return base.GetRootNodeCore();
				});
			});
		}

		protected CrmSiteMapNode GetAccessibleNodeOrAccessDeniedNode(ContentMap map, WebsiteNode site, WebPageNode page, IContentMapEntityUrlProvider provider, bool excludeFromSecurityValidation = false)
		{
			if (excludeFromSecurityValidation)
			{
				return GetNode(map, page, provider) ?? GetAccessDeniedNodeInternal(map, site, provider);
			}

			return ReturnNodeIfAccessible(GetNode(map, page, provider), () => GetAccessDeniedNodeInternal(map, site, provider));
		}

		protected CrmSiteMapNode GetAccessDeniedNodeInternal(ContentMap map, WebsiteNode site, IContentMapEntityUrlProvider provider)
		{
			var page = site.SiteMarkers.FirstOrDefault(sm => sm.Name == AccessDeniedPageSiteMarkerName)?.WebPage;

			return page != null && !page.IsReference
				? GetNode(map, page, HttpStatusCode.Forbidden, provider, includeReturnUrl: true)
				: GetNotFoundNode();
		}


		protected CrmSiteMapNode GetNotFoundNode(ContentMap map, WebsiteNode site,
			IContentMapEntityUrlProvider urlProvider)
		{
			var notFoundPage = site.SiteMarkers.FirstOrDefault(sm => sm.Name == NotFoundPageSiteMarkerName);

			if (notFoundPage == null || notFoundPage.WebPage == null || notFoundPage.WebPage.IsReference)
			{
				return null;
			}

			var languageInfo = HttpContext.Current.GetContextLanguageInfo();
			var path = HttpContext.Current.Request.Path;
			var notFoundNode = languageInfo.FindLanguageSpecificWebPageNode(notFoundPage.WebPage, true);
			var isLanguageEnabled = languageInfo.IsCrmMultiLanguageEnabled;

			if (isLanguageEnabled)
			{
				var isPublished = languageInfo.ContextLanguage.IsPublished;
				var language = languageInfo.ContextLanguage.WebsiteLanguageNode.Name;

				if (isPublished && notFoundNode == null)
				{
					var root = this.RootNode as CrmSiteMapNode;

					if (null != root)
					{
						ADXTrace.Instance.TraceWarning(TraceCategory.Application, "Cannot find language specific web page for url");

						var id = root.Entity.GetAttributeValue("adx_webpageid");
						throw new HttpException((int)HttpStatusCode.NotFound, "Error ID - {0}.  The Webpage you are looking for at {1} is not found in the {2} language. To display Page Not Found page localize it in {2} language.".FormatWith(id, path, language));
					}
				}
				else if (!isPublished && notFoundNode != null)
				{
					ADXTrace.Instance.TraceInfo(TraceCategory.Application, string.Format("Language not available for: {0}", language));

					var id = notFoundNode.Id;
					throw new HttpException((int)HttpStatusCode.NotFound, "Error ID – {0} . {1} language is not available. Please ensure it is in published status.".FormatWith(id, language));
				}
				else if (!isPublished)
				{
					throw new HttpException((int)HttpStatusCode.NotFound, "Not Found.");
				}
			}

			return GetNode(map, notFoundNode, HttpStatusCode.NotFound, urlProvider);
		}

		protected virtual CrmSiteMapNode GetNode(ContentMap map, WebPageNode page, IContentMapEntityUrlProvider provider)
		{
			return GetNode(map, page, HttpStatusCode.OK, provider);
		}

		protected virtual CrmSiteMapNode GetNode(ContentMap map, WebPageNode webPageNode, HttpStatusCode statusCode, IContentMapEntityUrlProvider provider, bool includeReturnUrl = false)
		{
			var contextLanguageInfo = HttpContext.Current.GetContextLanguageInfo();

			WebPageNode GetLanguageNode()
			{
				ADXTrace.Instance.TraceInfo(TraceCategory.Monitoring, string.Format("SiteMapProvider.GetNode Lang:{0} ", webPageNode.IsRoot != false ? "root" : webPageNode.WebPageLanguage.PortalLanguage.Code));
				var languageNode = webPageNode.LanguageContentPages.FirstOrDefault(p => p.WebPageLanguage.PortalLanguage.Code == contextLanguageInfo.ContextLanguage.Code);
				return languageNode ?? webPageNode;
			}

			var page = contextLanguageInfo.IsCrmMultiLanguageEnabled ? GetLanguageNode() : webPageNode;

			var template = page.PageTemplate;

			if (template == null)
			{
				ADXTrace.Instance.TraceInfo(TraceCategory.Application, string.Format("Web Page with id '{0}' does not have the required Page Template.", page.Id));

				return null;
			}

			var returnUrl = includeReturnUrl && HttpContext.Current  != null
				? "&ReturnUrl={0}".FormatWith(System.Web.Security.AntiXss.AntiXssEncoder.UrlEncode(HttpContext.Current.Request.Url.PathAndQuery))
				: string.Empty;

			var rewriteUrl = template.Type == (int)PageTemplateNode.TemplateType.WebTemplate && template.WebTemplateId != null
				? template.UseWebsiteHeaderAndFooter.GetValueOrDefault(true) ? "~/Pages/WebTemplate.aspx" : "~/Pages/WebTemplateNoMaster.aspx"
				: template.RewriteUrl;

			var entity = page.ToEntity(GetEntityType("adx_webpage"));
			var url = provider.GetUrl(map, page);

			var node = new CrmSiteMapNode(
				this,
				url,
				url,
				!string.IsNullOrWhiteSpace(page.Title) ? page.Title : page.Name,
				page.Summary,
				"{0}?pageid={1}{2}".FormatWith(rewriteUrl, page.Id, returnUrl),
				page.ModifiedOn.GetValueOrDefault(DateTime.UtcNow),
				entity,
				statusCode);

			if (template.WebTemplateId != null)
			{
				node["adx_webtemplateid"] = template.WebTemplateId.Id.ToString();
			}

			return node;
		}

		protected CrmSiteMapNode GetAccessibleNodeOrAccessDeniedNode(ContentMap map, WebsiteNode site, WebFileNode file, IContentMapEntityUrlProvider provider, bool excludeFromSecurityValidation = false)
		{
			if (excludeFromSecurityValidation)
			{
				return GetNode(map, file, provider) ?? GetAccessDeniedNodeInternal(map, site, provider);
			}

			return ReturnNodeIfAccessible(GetNode(map, file, provider), () => GetAccessDeniedNodeInternal(map, site, provider));
		}

		protected virtual CrmSiteMapNode GetNode(ContentMap map, WebFileNode file, IContentMapEntityUrlProvider provider)
		{
			return GetNode(map, file, HttpStatusCode.OK, provider);
		}

		protected virtual CrmSiteMapNode GetNode(ContentMap map, WebFileNode file, HttpStatusCode statusCode, IContentMapEntityUrlProvider provider)
		{
			var entity = file.ToEntity(GetEntityType("adx_webfile"));
			var url = provider.GetUrl(map, file);

			return new CrmSiteMapNode(
				this,
				url,
				url,
				file.Name,
				file.Summary,
				string.Empty,
				file.ModifiedOn.GetValueOrDefault(DateTime.UtcNow),
				entity,
				statusCode);
		}

		protected virtual CrmSiteMapNode GetNode(ContentMap map, ShortcutNode shortcut, IContentMapEntityUrlProvider provider)
		{
			return GetNode(map, shortcut, HttpStatusCode.OK, provider);
		}

		protected virtual CrmSiteMapNode GetNode(ContentMap map, ShortcutNode shortcut, HttpStatusCode statusCode, IContentMapEntityUrlProvider provider)
		{
			var entity = shortcut.ToEntity(GetEntityType("adx_shortcut"));

			var url = !string.IsNullOrWhiteSpace(shortcut.ExternalUrl) ? shortcut.ExternalUrl : provider.GetUrl(map, shortcut);

			if (url == null)
			{
				return null;
			}

			var description = GetShortcutDescription(shortcut);

			return new CrmSiteMapNode(
				this,
				url,
				url,
				!string.IsNullOrWhiteSpace(shortcut.Title) ? shortcut.Title : shortcut.Name,
				description,
				null,
				shortcut.ModifiedOn.GetValueOrDefault(DateTime.UtcNow),
				entity,
				statusCode);
		}

		private T CachePerRequest<T>(string method, Func<T> get) where T : class
		{
			return CachePerRequest(method, string.Empty, get);
		}

		private T CachePerRequest<T>(string method, string key, Func<T> get) where T : class
		{
			var httpContext = HttpContext.Current;

			if (httpContext == null)
			{
				return get();
			}

			var cacheKey = "{0}:{1}:{2}:{3}".FormatWith(GetType().FullName, Name, method, key);
			var cached = httpContext.Items[cacheKey] as T;

			if (cached != null)
			{
				ADXTrace.Instance.TraceInfo(TraceCategory.Monitoring, string.Format("CachePerRequest Cache:{0}", key));
				return cached;
			}

			var value = get();

			httpContext.Items[cacheKey] = value;

			ADXTrace.Instance.TraceInfo(TraceCategory.Monitoring, string.Format("CachePerRequest Get:{0}", key));
			return value;
		}

		private IContentMapProvider GetContentMapProvider()
		{
			return AdxstudioCrmConfigurationManager.CreateContentMapProvider(PortalName);
		}

		private static string GetShortcutDescription(ShortcutNode shortcut)
		{
			if (!string.IsNullOrWhiteSpace(shortcut.Description)) return shortcut.Description;
			if (shortcut.WebPage != null && !shortcut.WebPage.IsReference) return shortcut.WebPage.Summary;
			if (shortcut.WebFile != null && !shortcut.WebFile.IsReference) return shortcut.WebFile.Summary;

			return null;
		}

		private Type GetEntityType(string entityLogicalName)
		{
			var portal = PortalContext;
			var context = portal.ServiceContext;

			EntitySetInfo info;

			if (OrganizationServiceContextInfo.TryGet(context.GetType(), entityLogicalName, out info))
			{
				return info.Entity.EntityType;
			}

			return null;
		}

		private bool TryGetCurrentNodeUrlFromRouteData(HttpContext httpContext, out string url)
		{
			url = null;

			if (httpContext == null)
			{
				return false;
			}

			var encodedUrl = httpContext.Request.RequestContext.RouteData.Values["__currentSiteMapNodeUrl__"] as string;

			if (string.IsNullOrWhiteSpace(encodedUrl))
			{
				return false;
			}

			try
			{
				url = Encoding.UTF8.GetString(Convert.FromBase64String(encodedUrl));

				return !string.IsNullOrWhiteSpace(url);
			}
			catch
			{
				return false;
			}
		}

		private bool TryGetWebsite(ContentMap map, out WebsiteNode site, out IContentMapEntityUrlProvider urlProvider)
		{
			urlProvider = PortalCrmConfigurationManager.CreateDependencyProvider(PortalName).GetDependency<IContentMapEntityUrlProvider>();

			if (urlProvider == null)
			{
				site = null;
				return false;
			}

			var portal = PortalContext;
			var website = portal.Website;

			if (!map.TryGetValue(website, out site))
			{
				return false;
			}

			return true;
		}

		/// <summary>
		/// Figures out whether the given web page is a translated content WebPage and its language matches the current active portal language. 
		/// If multi-language is not enabled, then true will always be returned.
		/// </summary>
		/// <param name="page">WebPageNode to check.</param>
		/// <param name="langContext">The language context.</param>
		/// <returns>Whether the given web page matches the current active portal language.</returns>
		private bool IsValidLanguageContentPage(WebPageNode page, ContextLanguageInfo langContext)
		{
			if (langContext.IsCrmMultiLanguageEnabled)
			{
				return page.IsRoot != true && page.WebPageLanguage != null && page.WebPageLanguage.Id == langContext.ContextLanguage.EntityReference.Id;
			}
			return true;
		}
	}
}
