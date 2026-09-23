/*
  Copyright (c) Microsoft Corporation. All rights reserved.
  Licensed under the MIT License. See License.txt in the project root for license information.
*/

namespace Adxstudio.Xrm.Web.Routing
{
	using System;
	using System.Net;
	using System.Web;
	using System.Linq;
	using System.Web.Hosting;
	using System.Web.Routing;
	using System.Web.WebPages;
	using Adxstudio.Xrm.AspNet.Cms;
	using Microsoft.Xrm.Client;
	using Adxstudio.Xrm.Cms;
	using Microsoft.Xrm.Portal;
	using Microsoft.Xrm.Portal.Web;
	using Microsoft.Xrm.Portal.Configuration;
	using Adxstudio.Xrm.Web;

    /// <summary>
    /// Handles requests to portal <see cref="Entity"/> objects.
    /// </summary>
    /// <seealso cref="PortalRoutingModule"/>
    public class PortalRouteHandler : Microsoft.Xrm.Portal.Web.Routing.PortalRouteHandler
	{
		private const string DefaultWebTemplate = "~/Pages/WebTemplate.aspx";
		private const string WebTemplateWithoutValidation = "~/Pages/WebTemplateNoValidation.aspx";
		private const string DisableValidationSiteSetting = "DisableValidationWebTemplate";

		private readonly string[] _redirectedEntities = {
			"adx_webpage",
			"adx_communityforum",
			"adx_communityforumthread",
			"adx_blog",
			"adx_blogpost"
		};

		/// <summary>
		/// Initialize class
		/// </summary>
		/// <param name="portalName"></param>
		public PortalRouteHandler(string portalName)
			: base(portalName)
		{
		}

		/// <summary>
		/// Provides the object that processes the request.
		/// </summary>
		/// <param name="requestContext">An object that encapsulates information about the request.</param>
		/// <returns></returns>
		public override IHttpHandler GetHttpHandler(RequestContext requestContext)
		{
			string routeUrl = string.Empty;
			try
			{
				Route route = (System.Web.Routing.Route)requestContext.RouteData.Route;
				routeUrl = route.Url;
			}
			catch { }
			ADXTrace.Instance.TraceInfo(TraceCategory.Monitoring, string.Format("GetHttpHandler route=[{0}] ", routeUrl));

			// apply the current SiteMapNode to the OWIN environment
			var node = GetNode(requestContext);

			if (node != null)
			{
				requestContext.Set(node);
			}

			// Note: prior to this next call, PortalContext is null.
			var portal = PortalCrmConfigurationManager.CreatePortalContext(PortalName, requestContext);

			portal = OnPortalLoaded(requestContext, portal);

			if (portal == null) return null;

			var isInvalidNode = portal.Entity == null || portal.Path == null;

			// there's nothing else we can really do--we'll exit with a bare-bones 404.
			if (isInvalidNode)
			{
				ADXTrace.Instance.TraceInfo(TraceCategory.Application, "FindSiteMapNode failed to find a non-null CrmSiteMapNode. Responding with a basic 404.");

				RenderNotFound(requestContext);

				return null;
			}

			if (portal.StatusCode == HttpStatusCode.NotFound)
			{
				var response = requestContext.HttpContext.Response;
				response.StatusCode = (int)HttpStatusCode.NotFound;
			}
			else if (portal.StatusCode == HttpStatusCode.Forbidden)
			{
				var response = requestContext.HttpContext.Response;
				response.StatusCode = (int)HttpStatusCode.Forbidden;
			}

			IHttpHandler handler;

			if (TryCreateHandler(portal, out handler))
			{
				return handler;
			}

			if (node?.Entity?.Attributes != null && node.Entity.Attributes.ContainsKey("adx_alloworigin"))
			{
				var allowOrigin = node.Entity["adx_alloworigin"] as string;

				Web.Extensions.SetAccessControlAllowOriginHeader(requestContext.HttpContext, allowOrigin);
			}
			// remove the querystring

			var rewritePath = portal.Path;
			var index = rewritePath.IndexOf("?");
			var path = index > -1 ? rewritePath.Substring(0, index) : rewritePath;

			DisplayInfo displayInfo;

			if (string.Equals(path, DefaultWebTemplate, StringComparison.OrdinalIgnoreCase) && requestContext.HttpContext.GetSiteSetting<bool>(DisableValidationSiteSetting))
			{
				path = WebTemplateWithoutValidation;
			}

			ADXTrace.Instance.TraceInfo(TraceCategory.Application, string.Format("Current path: {0}", path));

			if (TryGetDisplayModeInfoForPath(path, requestContext.HttpContext, out displayInfo))
			{
				ADXTrace.Instance.TraceInfo(TraceCategory.Application, string.Format("Found displayModeInfo={0} for path", displayInfo.DisplayMode.DisplayModeId));

				return displayInfo.FilePath == null ? null : CreateHandlerFromVirtualPath(displayInfo.FilePath);
			}

			ADXTrace.Instance.TraceInfo(TraceCategory.Application, "Unable to get DisplayModeInfo for adx_pagetemplate with rewrite path");

			return path == null ? null : CreateHandlerFromVirtualPath(path);
		}

		protected override bool TryCreateHandler(IPortalContext portal, out IHttpHandler handler)
		{
			var routeHandlerProvider = PortalCrmConfigurationManager.CreateDependencyProvider(PortalName).GetDependency<IPortalRouteHandlerProvider>();
			return routeHandlerProvider.TryCreateHandler(portal, out handler);
		}

		protected override IPortalContext OnPortalLoaded(RequestContext requestContext, IPortalContext portal)
		{
			if (portal == null) throw new ArgumentNullException("portal");

			if (portal.Entity != null && _redirectedEntities.Contains(portal.Entity.LogicalName))
			{
				// Check if we need to follow any rules with regards to Language Code prefix in URL (only applies if multi-language is enabled)
				var contextLanguageInfo = requestContext.HttpContext.GetContextLanguageInfo();
				if (contextLanguageInfo.IsCrmMultiLanguageEnabled)
				{
					bool needRedirect = requestContext.HttpContext.Request.HttpMethod == WebRequestMethods.Http.Get
											&& ((ContextLanguageInfo.DisplayLanguageCodeInUrl != contextLanguageInfo.RequestUrlHasLanguageCode)
											|| contextLanguageInfo.ContextLanguage.UsedAsFallback);
					if (needRedirect)
					{
						string redirectPath = contextLanguageInfo.FormatUrlWithLanguage();
						ADXTrace.Instance.TraceInfo(TraceCategory.Monitoring, "OnPortalLoaded redirecting(1)");
						requestContext.HttpContext.Response.StatusCode = (int)HttpStatusCode.Redirect;
						requestContext.HttpContext.Response.RedirectLocation = redirectPath;
						requestContext.HttpContext.Response.End();
						return null;
					}
				}
			}

			var isInvalidNode = (portal.Entity == null || portal.Path == null);

			// If the node is null, isn't a CrmSiteMapNode, has no rewrite path, or is a 404, try other options.
			if (isInvalidNode || portal.StatusCode == HttpStatusCode.NotFound)
			{
				var redirectProvider = PortalCrmConfigurationManager.CreateDependencyProvider(PortalName).GetDependency<IRedirectProvider>();
				var context = requestContext.HttpContext;
				var clientUrl = new UrlBuilder(context.Request.Url);

				// Try matching user-defined redirects, and URL history--in that order.
				var redirectMatch = redirectProvider.Match(portal.Website.Id, clientUrl);

				// If we have a successful match, redirect.
				if (redirectMatch.Success)
				{
					ADXTrace.Instance.TraceInfo(TraceCategory.Monitoring, "OnPortalLoaded redirecting(2)");
					context.Trace.Write(GetType().FullName, @"Redirecting path ""{0}"" to ""{1}""".FormatWith(clientUrl.Path, redirectMatch.Location));

					context.Response.StatusCode = (int)redirectMatch.StatusCode;
					context.Response.RedirectLocation = redirectMatch.Location;
					context.Response.End();

					return null;
				}
			}

			return base.OnPortalLoaded(requestContext, portal);
		}

		protected virtual bool TryGetDisplayModeInfoForPath(string path, HttpContextBase httpContext, out DisplayInfo displayInfo)
		{
			displayInfo = null;

			var displayModeProvider = DisplayModeProvider.Instance;

			if (displayModeProvider == null)
			{
				return false;
			}

			var displayModes = displayModeProvider.GetAvailableDisplayModesForContext(httpContext, null);

			foreach (var displayMode in displayModes)
			{
				displayInfo = displayMode.GetDisplayInfo(httpContext, path, VirtualPathExists);

				if (displayInfo != null)
				{
					return true;
				}
			}

			return false;
		}

		private bool VirtualPathExists(string virtualPath)
		{
			try
			{
				return HostingEnvironment.VirtualPathProvider.FileExists(virtualPath);
			}
			catch (Exception e)
			{
				ADXTrace.Instance.TraceError(TraceCategory.Application, string.Format(@"Exception while checking existence of virtual path: {0}", e.ToString()));

				return false;
			}
		}

		internal static CrmSiteMapNode GetNode(RequestContext request)
		{
			if (request == null) return null;

			var path = request.RouteData.Values["path"] as string;
			path = FormatPath(path);

			ADXTrace.Instance.TraceInfo(TraceCategory.Monitoring, string.Format("path={0}", path));

			var node = SiteMap.Provider.FindSiteMapNode(path) as CrmSiteMapNode;
			return node;
		}

		/// <summary>
		/// Formats a route path value so that it can be processed by the site map.
		/// Rules are: 1) if path is null or white space, then make it to be just a forward-slash.
		/// 2) else if path doesn't have a leading forward-slash, then insert leading forward-slash.
		/// 3) else just return path as is.
		/// </summary>
		/// <param name="rawPath">Raw route path value to format.</param>
		/// <returns>Route path value formatted so it can be processed by the site map.</returns>
		private static string FormatPath(string rawPath)
		{
			var forwardSlash = System.IO.Path.AltDirectorySeparatorChar.ToString();
			return !string.IsNullOrWhiteSpace(rawPath) ? rawPath.StartsWith(forwardSlash) ? rawPath : forwardSlash + rawPath : forwardSlash;
		}
	}
}
