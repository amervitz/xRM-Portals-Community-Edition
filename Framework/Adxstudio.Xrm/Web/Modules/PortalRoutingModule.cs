/*
  Copyright (c) Microsoft Corporation. All rights reserved.
  Licensed under the MIT License. See License.txt in the project root for license information.
*/

using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using Adxstudio.Xrm.Blogs;
using Adxstudio.Xrm.Configuration;
using Adxstudio.Xrm.Web.Handlers.ElFinder;
using Adxstudio.Xrm.Web.Mvc;
using Adxstudio.Xrm.Web.Mvc.Controllers;
using Adxstudio.Xrm.Web.Routing;
using Microsoft.Xrm.Client;
using Microsoft.Xrm.Portal.Web.Routing;

namespace Adxstudio.Xrm.Web.Modules
{
	/// <summary>
	/// Manages the URL routing for a standard portal website.
	/// </summary>
	/// <remarks>
	/// <example>
	/// Example configuration.
	/// <code>
	/// <![CDATA[
	/// <configuration>
	/// 
	///  <system.webServer>
	///   <modules runAllManagedModulesForAllRequests="true">
	///    <add name="PortalRouting" type="Adxstudio.Xrm.Web.Modules.PortalRoutingModule, Adxstudio.Xrm" preCondition="managedHandler"/>
	///   </modules>
	///  </system.webServer>
	///  
	/// </configuration>
	/// ]]>
	/// </code>
	/// </example>
	/// </remarks>
	public class PortalRoutingModule : Microsoft.Xrm.Portal.Web.Modules.PortalRoutingModule
	{
		private const string _prefix = "xrm-adx";

		// For an embedded resource file to be eligible for use in a server-side include, it must be listed here.
		private static readonly string[] _paths = new[]
		{
			"js/editable/editable.js",
			"js/editable/utilities.js",
			"js/editable/attribute.js",
			"js/editable/attributes/html.js",
			"js/editable/attributes/textarea.js",
			"js/editable/datetimepicker.js",
			"js/editable/entity.js",
			"js/editable/entity_form.js",
			"js/editable/entity_handler.js",
			"js/editable/entities/adx_blog.js",
			"js/editable/entities/adx_blogpost.js",
			"js/editable/entities/feedback.js",
			"js/editable/entities/adx_communityforum.js",
			"js/editable/entities/adx_communityforumpost.js",
			"js/editable/entities/adx_communityforumthread.js",
			"js/editable/entities/adx_event.js",
			"js/editable/entities/adx_eventschedule.js",
			"js/editable/entities/adx_shortcut.js",
			"js/editable/entities/adx_webfile.js",
			"js/editable/entities/adx_weblinkset.js",
			"js/editable/entities/adx_webpage.js",
			"js/editable/entities/sitemapchildren.js",
			"js/editable/cmsentityservice.js",
			"js/xrm-postload.js",
		};

		public override void Init(HttpApplication application)
		{
			RouteTable.Routes.UseWithWriteLock(routes =>
			{
				base.Init(application);
			});
		}

		protected virtual bool CdnEnabled
		{
			get { return AdxstudioCrmConfigurationManager.GetCrmSection().CdnEnabled; }
		}

		protected override void Register(
			RouteCollection routes,
			string prefix,
			IEmbeddedResourceRouteHandler embeddedResourceRouteHandler,
			IEmbeddedResourceRouteHandler scriptHandler)
		{
			if (UseEmbeddedResourceVirtualPathProvider)
			{
				// ignore the prefix to allow the EmbeddedResourceVirtualPathProvider to handle the request

				routes.Ignore(prefix + "/{*pathInfo}");
			}
			else
			{
				// add the combined script handler

				var contstraints = new RouteValueDictionary(new { prefix });

				routes.Add(
					"type={0},prefix={1}".FormatWith(scriptHandler, prefix),
					new Route(
						"{prefix}/js/xrm-combined.js/{*pathInfo}",
						null,
						contstraints,
						scriptHandler));

				// keep this route until the xrm-combined-js.aspx is removed from the files project

				routes.Add(
					"type={0},prefix={1},extension=aspx".FormatWith(scriptHandler, prefix),
					new Route(
						"{prefix}/js/xrm-combined-js.aspx/{*pathInfo}",
						null,
						contstraints,
						scriptHandler));

				// add the embedded resource handler

				routes.Add(
					"type={0},prefix={1}".FormatWith(embeddedResourceRouteHandler, prefix),
					new Route(
						"{prefix}/{*path}",
						null,
						contstraints,
						embeddedResourceRouteHandler));
			}

			if (CdnEnabled)
			{
				RegisterCdn(routes, prefix, embeddedResourceRouteHandler, scriptHandler);
			}
		}

		protected virtual void RegisterCdn(
			RouteCollection routes,
			string prefix,
			IEmbeddedResourceRouteHandler embeddedResourceRouteHandler,
			IEmbeddedResourceRouteHandler scriptHandler)
		{
			// add the combined script handler

			var contstraints = new RouteValueDictionary(new { prefix, cdn = "cdn" });

			routes.Add(
				"type={0},prefix={1},cdn".FormatWith(scriptHandler, prefix),
				new Route(
					"{cdn}/{prefix}/js/xrm-combined.js/{*pathInfo}",
					null,
					contstraints,
					scriptHandler));

			// keep this route until the xrm-combined-js.aspx is removed from the files project

			routes.Add(
				"type={0},prefix={1},extension=aspx,cdn".FormatWith(scriptHandler, prefix),
				new Route(
					"{cdn}/{prefix}/js/xrm-combined-js.aspx/{*pathInfo}",
					null,
					contstraints,
					scriptHandler));

			// add the embedded resource handler

			routes.Add(
				"type={0},prefix={1},cdn".FormatWith(embeddedResourceRouteHandler, prefix),
				new Route(
					"{cdn}/{prefix}/{*path}",
					null,
					contstraints,
					embeddedResourceRouteHandler));
		}

		protected override void RegisterEmbeddedResourceRoutes(
			RouteCollection routes,
			IRouteHandler portalRouteHandler,
			IEmbeddedResourceRouteHandler embeddedResourceRouteHandler,
			IEmbeddedResourceRouteHandler scriptHandler)
		{
			base.RegisterEmbeddedResourceRoutes(routes, portalRouteHandler, embeddedResourceRouteHandler, scriptHandler);

			// register custom embedded resource handler routes

			var paths = GetPaths(_prefix, _paths).ToArray();
			var customScriptHandler = new CompositeEmbeddedResourceRouteHandler(scriptHandler.Mappings, paths);

			// register embedded resource handler routes

			Register(routes, _prefix, embeddedResourceRouteHandler, customScriptHandler);
		}

		protected override void RegisterCustomRoutes(
			RouteCollection routes,
			IRouteHandler portalRouteHandler,
			IEmbeddedResourceRouteHandler embeddedResourceRouteHandler,
			IEmbeddedResourceRouteHandler scriptHandler)
		{
			var entityRouteHandler = new Adxstudio.Xrm.Web.Routing.EntityRouteHandler(PortalName);

			routes.Add(
				entityRouteHandler.GetType().FullName,
				new Route(
					"{prefix}/{logicalName}/{id}",
					null,
					new RouteValueDictionary(new { prefix = "_entity" }),
					entityRouteHandler));

			routes.Add(
				entityRouteHandler.GetType().FullName + "PortalScoped",
				new Route(
					"{prefix}/{logicalName}/{id}/{__portalScopeId__}",
					null,
					new RouteValueDictionary(new { prefix = "_entity" }),
					entityRouteHandler));
			
			if (CdnEnabled)
			{
				routes.Add(
					entityRouteHandler.GetType().FullName + ",cdn",
					new Route(
						"{cdn}/{prefix}/{logicalName}/{id}",
						null,
						new RouteValueDictionary(new { prefix = "_entity", cdn = "cdn" }),
						entityRouteHandler));
			}

			var webResourceRouteHandler = new WebResourceRouteHandler(PortalName);

			routes.Add(
				webResourceRouteHandler.GetType().FullName,
				new Route(
					"{prefix}/{name}",
					null,
					new RouteValueDictionary(new { prefix = "_webresource" }),
					webResourceRouteHandler));

			if (CdnEnabled)
			{
				routes.Add(
					webResourceRouteHandler.GetType().FullName + ",cdn",
					new Route(
						"{cdn}/{prefix}/{name}",
						null,
						new RouteValueDictionary(new { prefix = "_webresource", cdn = "cdn" }),
						webResourceRouteHandler));
			}

			var servicesConstraints = new RouteValueDictionary(new { area = "_services" });

			routes.Add(typeof(ElFinderRouteHandler).FullName, new Route(ElFinderRouteHandler.RoutePath, new ElFinderRouteHandler(PortalName)));

			routes.MapRoute("CmsTemplate_GetAll",
				"_services/portal/{__portalScopeId__}/{entityLogicalname}/{id}/__templates/all/{currentSiteMapNodeUrl}",
				new { controller = "CmsTemplate", action = "GetAll" },
				new[] { "Adxstudio.Xrm.Web.Mvc.Controllers" });

			routes.MapRoute("CmsTemplate_Get",
				"_services/portal/{__portalScopeId__}/{entityLogicalname}/{id}/__templates/source/{encodedName}",
				new { controller = "CmsTemplate", action = "Get" },
				new[] { "Adxstudio.Xrm.Web.Mvc.Controllers" });

			routes.MapRoute("CmsTemplate_GetPreview",
				"_services/portal/{__portalScopeId__}/{entityLogicalname}/{id}/__templates/preview/{encodedName}/{__currentSiteMapNodeUrl__}",
				new { controller = "CmsTemplate", action = "GetPreview" },
				new[] { "Adxstudio.Xrm.Web.Mvc.Controllers" });

			routes.MapRoute("CmsTemplate_GetLivePreview",
				"_services/portal/{__portalScopeId__}/{entityLogicalname}/{id}/__templates/live-preview/{__currentSiteMapNodeUrl__}",
				new { controller = "CmsTemplate", action = "GetLivePreview" },
				new[] { "Adxstudio.Xrm.Web.Mvc.Controllers" });

			routes.MapRoute("CmsParent_GetParentOptions",
				"_services/portal/{__portalScopeId__}/__parents",
				new { controller = "CmsParent", action = "GetParentOptions" },
				new[] { "Adxstudio.Xrm.Web.Mvc.Controllers" });

			routes.MapRoute("CmsParent_GetParentOptionsForEntity",
				CmsParentController.GetParentOptionsForEntityRoutePath,
				new { controller = "CmsParent", action = "GetParentOptionsForEntity" },
				new[] { "Adxstudio.Xrm.Web.Mvc.Controllers" });

			routes.Add(typeof(CmsEntityChildrenRouteHandler).FullName, new Route(CmsEntityChildrenRouteHandler.RoutePath, new CmsEntityChildrenRouteHandler(PortalName)));
			routes.Add(typeof(CmsEntityDeleteRouteHandler).FullName, new Route(CmsEntityDeleteRouteHandler.RoutePath, new CmsEntityDeleteRouteHandler(PortalName)));
			routes.Add(typeof(CmsEntityFileAttachmentRouteHandler).FullName, new Route(CmsEntityFileAttachmentRouteHandler.RoutePath, new CmsEntityFileAttachmentRouteHandler(PortalName)));
			routes.Add(typeof(CmsEntityUrlRouteHandler).FullName, new Route(CmsEntityUrlRouteHandler.RoutePath, new CmsEntityUrlRouteHandler(PortalName)));
			routes.Add(typeof(CmsEntityAttributeRouteHandler).FullName, new Route(CmsEntityAttributeRouteHandler.RoutePath, new CmsEntityAttributeRouteHandler(PortalName)));
			routes.Add(typeof(CmsEntityRelationshipRouteHandler).FullName, new Route(CmsEntityRelationshipRouteHandler.RoutePath, new CmsEntityRelationshipRouteHandler(PortalName)));
			routes.Add(typeof(CmsEntityRouteHandler).FullName, new Route(CmsEntityRouteHandler.RoutePath, new CmsEntityRouteHandler(PortalName)));
			routes.Add(typeof(CmsEntitySetRouteHandler).FullName, new Route(CmsEntitySetRouteHandler.RoutePath, new CmsEntitySetRouteHandler(PortalName)));
			routes.Add(typeof(OrganizationServiceCachePluginMessageRouteHandler).FullName, new Route(OrganizationServiceCachePluginMessageRouteHandler.RoutePath, null, servicesConstraints, new OrganizationServiceCachePluginMessageRouteHandler(PortalName)));
			routes.Add(typeof(BlogFeedRouteHandler).FullName, new Route(BlogFeedRouteHandler.RoutePath, new BlogFeedRouteHandler(PortalName)));
			routes.Add(typeof(WebsiteBlogAggregationFeedRouteHandler).FullName, new Route(WebsiteBlogAggregationFeedRouteHandler.RoutePath, new WebsiteBlogAggregationFeedRouteHandler(PortalName)));
		}

		protected override void RegisterDefaultRoutes(
			RouteCollection routes,
			IRouteHandler portalRouteHandler,
			IEmbeddedResourceRouteHandler embeddedResourceRouteHandler,
			IEmbeddedResourceRouteHandler scriptHandler)
		{
			// swap out the PortalRouteHandler with a custom dependency (keeping the base script handler)

			var portalRouteHandlerAdx = new Adxstudio.Xrm.Web.Routing.PortalRouteHandler(PortalName);

			if (CdnEnabled)
			{
				var route = new Route("{cdn}/{*path}", null, new RouteValueDictionary(new { cdn = "cdn" }), portalRouteHandlerAdx);
				routes.Add(portalRouteHandler.GetType().FullName + ",cdn", route);
			}

			base.RegisterDefaultRoutes(routes, portalRouteHandlerAdx, embeddedResourceRouteHandler, scriptHandler);
		}
	}
}
