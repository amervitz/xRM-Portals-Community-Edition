/*
  Copyright (c) Microsoft Corporation. All rights reserved.
  Licensed under the MIT License. See License.txt in the project root for license information.
*/

using System;
using System.Collections.Specialized;
using System.Net;
using System.Web;
using Microsoft.Xrm.Client.Diagnostics;
using Microsoft.Xrm.Portal.Cms;
using Microsoft.Xrm.Portal.Configuration;
using Microsoft.Xrm.Portal.Runtime;
using Microsoft.Xrm.Portal.Web.Providers;
using Encoder = System.Web.Security.AntiXss.AntiXssEncoder;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Client;
using Microsoft.Xrm.Sdk.Client;

namespace Microsoft.Xrm.Portal.Web
{
	/// <summary>
	/// Abstract base class for XRM <see cref="SiteMapProvider"/>s, containing shared configuration and
	/// security validation logic.
	/// </summary>
	public abstract class CrmSiteMapProviderBase : SiteMapProvider
	{
		public string PortalName { get; private set; }

		protected IPortalContext PortalContext
		{
			get { return PortalCrmConfigurationManager.CreatePortalContext(PortalName); }
		}

		protected virtual ICrmSiteMapNodeValidator ChildNodeValidator { get; private set; }

		protected virtual ICrmSiteMapNodeValidator NodeValidator { get; private set; }

		protected virtual ICrmSiteMapNodeValidator SecurityTrimmingValidator { get; private set; }

		public override void Initialize(string name, NameValueCollection attributes)
		{
			TraceInfo(@"Initialize(""{0}"", attributes)", name);

			// Enable security trimming by default.
			attributes["securityTrimmingEnabled"] = attributes["securityTrimmingEnabled"] ?? "true";

			foreach (var key in attributes.AllKeys)
			{
				TraceInfo(@"{0}=""{1}""", key, attributes[key]);
			}

			base.Initialize(name, attributes);

			var nodeValidatorProvider = GetNodeValidatorProvider();
			NodeValidator =  nodeValidatorProvider.GetValidator(this);

			var securityTrimmingValidatorProvider = GetSecurityTrimmingValidatorProvider();
			SecurityTrimmingValidator = securityTrimmingValidatorProvider.GetValidator(this);

			var childNodeValidatorProvider = GetChildNodeValidatorProvider();
			ChildNodeValidator = childNodeValidatorProvider.GetValidator(this);

			PortalName = attributes["portalName"];
		}

		public override bool IsAccessibleToUser(HttpContext context, SiteMapNode node)
		{
			node.ThrowOnNull("node");
			context.ThrowOnNull("context");

			if (!SecurityTrimmingEnabled)
			{
				return true;
			}
			
			var crmNode = node as CrmSiteMapNode;

			if (crmNode == null)
			{
				return base.IsAccessibleToUser(context, node);
			}

			return SecurityTrimmingValidator.Validate(PortalContext.ServiceContext, crmNode);
		}

		protected void TraceInfo(string messageFormat, params object[] args)
		{
			Tracing.FrameworkInformation(GetType().Name, string.Empty, messageFormat, args);
		}

		protected static bool TryParseGuid(string value, out Guid guid)
		{
			guid = default(Guid);

			try
			{
				if (string.IsNullOrEmpty(value))
				{
					return false;
				}

				guid = new Guid(value);

				return true;
			}
			catch (FormatException)
			{
				return false;
			}
		}

		protected CrmSiteMapNode ReturnNodeIfAccessible(CrmSiteMapNode node, Func<CrmSiteMapNode> getFallbackNode)
		{
			return node != null && node.IsAccessibleToUser(HttpContext.Current) ? node : getFallbackNode();
		}

		protected abstract CrmSiteMapNode GetNode(OrganizationServiceContext context, Entity entity);
		
		protected CrmSiteMapNode GetAccessibleNodeOrAccessDeniedNode(OrganizationServiceContext context, Entity entity)
		{
			entity.ThrowOnNull("entity");

			return ReturnNodeIfAccessible(GetNode(context, entity), GetAccessDeniedNode);
		}

		protected CrmSiteMapNode GetAccessDeniedNode()
		{
			var portal = PortalContext;
			var context = portal.ServiceContext;
			var website = portal.Website;

			if (HttpContext.Current.User == null || HttpContext.Current.User.Identity == null || !HttpContext.Current.User.Identity.IsAuthenticated)
			{
				var loginPage = context.GetPageBySiteMarkerName(website, "Login");

				if (loginPage != null)
				{
					return ReturnNodeIfAccessible(GetWebPageNodeWithReturnUrl(context, loginPage, HttpStatusCode.Forbidden), GetNotFoundNode);
				}
			}

			var accessDeniedPage = context.GetPageBySiteMarkerName(website, "Access Denied");

			if (accessDeniedPage != null)
			{
				return ReturnNodeIfAccessible(GetWebPageNodeWithReturnUrl(context, accessDeniedPage, HttpStatusCode.Forbidden), GetNotFoundNode);
			}

			return GetNotFoundNode();
		}

		protected CrmSiteMapNode GetNotFoundNode()
		{
			var portal = PortalContext;
			var context = portal.ServiceContext;
			var website = portal.Website;

			var notFoundPage = context.GetPageBySiteMarkerName(website, "Page Not Found");

			if (notFoundPage == null)
			{
				return null;
			}

			var notFoundNode = GetWebPageNode(context, notFoundPage, HttpStatusCode.NotFound);

			return ReturnNodeIfAccessible(notFoundNode, () => null);
		}

		protected CrmSiteMapNode GetWebPageNode(OrganizationServiceContext context, Entity page, HttpStatusCode statusCode)
		{
			return GetWebPageNode(context, page, statusCode, p =>
			{
				var pageTemplate = p.GetRelatedEntity(context, "adx_pagetemplate_webpage");
				var webPageID = p.GetAttributeValue<Guid>("adx_webpageid");

				if (pageTemplate == null)
				{
					return null;
				}

				// MSBug #120133: Can't URL encode--used for URL rewrite.
				return "{0}?pageid={1}".FormatWith(pageTemplate.GetAttributeValue<string>("adx_rewriteurl"), webPageID);
			});
		}

		protected CrmSiteMapNode GetWebPageNode(OrganizationServiceContext context, Entity page, HttpStatusCode statusCode, Func<Entity, string> getRewriteUrl)
		{
			var contentFormatter = PortalCrmConfigurationManager.CreateDependencyProvider(PortalName).GetDependency<ICrmEntityContentFormatter>(GetType().FullName) ?? new PassthroughCrmEntityContentFormatter();

			var rewriteUrl = getRewriteUrl(page);

			var url = context.GetUrl(page);

			var title = page.GetAttributeValue<string>("adx_title");

			// apply a detached clone of the entity since the SiteMapNode is out of the scope of the current OrganizationServiceContext

			var pageClone = page.Clone(false);

			return new CrmSiteMapNode(
				this,
				url,
				url,
				contentFormatter.Format(string.IsNullOrEmpty(title) ? page.GetAttributeValue<string>("adx_name") : title, page, this),
				contentFormatter.Format(page.GetAttributeValue<string>("adx_summary"), page, this),
				rewriteUrl,
				page.GetAttributeValue<DateTime?>("modifiedon").GetValueOrDefault(DateTime.UtcNow),
				pageClone,
				statusCode);
		}

		protected CrmSiteMapNode GetWebPageNodeWithReturnUrl(OrganizationServiceContext context, Entity page, HttpStatusCode statusCode)
		{
			return GetWebPageNode(context, page, statusCode, p =>
			{
				var pageTemplate = p.GetRelatedEntity(context, "adx_pagetemplate_webpage");
				var webPageID = p.GetAttributeValue<Guid>("adx_webpageid");

				if (pageTemplate == null)
				{
					return null;
				}

				return "{0}?pageid={1}&ReturnUrl={2}".FormatWith(
					pageTemplate.GetAttributeValue<string>("adx_rewriteurl"),
					webPageID,
					Encoder.UrlEncode(HttpContext.Current.Request.Url.PathAndQuery));
			});
		}

		private INodeValidatorProvider GetNodeValidatorProvider()
		{
			return PortalCrmConfigurationManager.CreateDependencyProvider(PortalName).GetDependency<INodeValidatorProvider>("node");
		}

		private INodeValidatorProvider GetSecurityTrimmingValidatorProvider()
		{
			return PortalCrmConfigurationManager.CreateDependencyProvider(PortalName).GetDependency<INodeValidatorProvider>("securityTrimming");
		}

		private INodeValidatorProvider GetChildNodeValidatorProvider()
		{
			return PortalCrmConfigurationManager.CreateDependencyProvider(PortalName).GetDependency<INodeValidatorProvider>("childNode");
		}
	}
}
