/*
  Copyright (c) Microsoft Corporation. All rights reserved.
  Licensed under the MIT License. See License.txt in the project root for license information.
*/

namespace Adxstudio.Xrm.Web.Mvc.Liquid.Tags
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using DotLiquid;
	using Microsoft.Xrm.Sdk;
	using DotLiquid.Exceptions;
	using Adxstudio.Xrm.Services;
	using Adxstudio.Xrm.Services.Query;
	using Microsoft.Xrm.Sdk.Client;
	using Microsoft.Xrm.Sdk.Query;
	using OrganizationServiceContextExtensions = Cms.OrganizationServiceContextExtensions;

	/// <summary>
	/// Tag used for redirecting to specified sitemarker location
	/// </summary>
	public class Redirect : Tag
	{
		/// <summary>
		/// Attribute Dictionary
		/// </summary>
		private IDictionary<string, string> attributes;

		/// <summary>
		/// Initializes Tag attributes
		/// </summary>
		/// <param name="tagName">Tag Name</param>
		/// <param name="markup">Markup String</param>
		/// <param name="tokens">Token Collection</param>
		public override void Initialize(string tagName, string markup, List<string> tokens)
		{
			this.attributes = new Dictionary<string, string>(Template.NamingConvention.StringComparer);

			TagSyntax.ScanAttributes(markup, (key, value) => this.attributes[key] = value);

			base.Initialize(tagName, markup, tokens);
		}

		/// <summary>
		/// Executes the logic to perform the redirect based on passed sitemarker variable
		/// </summary>
		/// <param name="context">DotLiquid Context</param>
		/// <param name="result">TextWriter Result</param>
		public override void Render(Context context, TextWriter result)
		{
			string siteMarkerVariable;

			if (!this.attributes.TryGetValue("sitemarker", out siteMarkerVariable))
			{
				throw new SyntaxException("Syntax Error in 'redirect' tag. Missing required attribute 'sitemarker:[string]'");
			}

			IPortalLiquidContext portalLiquidContext;
			if (!context.TryGetPortalLiquidContext(out portalLiquidContext))
			{
				return;
			}

			siteMarkerVariable = siteMarkerVariable.Replace("\"", string.Empty);

			EntityReference website = portalLiquidContext.PortalViewContext.Website.EntityReference;
			Entity page;
			var serviceContext = portalLiquidContext.PortalViewContext.CreateServiceContext();

			if (!TryGetPageBySiteMarkerName(serviceContext, website, siteMarkerVariable, out page))
			{
				throw new SyntaxException("The sitemarker is unavailable");
			}

			var sitemarkerPath = OrganizationServiceContextExtensions.GetUrl(serviceContext, page);

			if (sitemarkerPath == null)
			{
				throw new SyntaxException("The sitemarker path is unavailable");
			}

			// Redirect to Sitemarker's URL path
			this.RedirectToUrl(context, sitemarkerPath);
		}

		/// <summary>
		/// Retrieves page for specified Sitemarker Name
		/// </summary>
		/// <param name="serviceContext">Organization Service Context</param>
		/// <param name="website">Website EntityReference</param>
		/// <param name="siteMarkerName">Sitemarker Name</param>
		/// <param name="page">Sitemarker Page</param>
		/// <returns>Sitemarker found</returns>
		private static bool TryGetPageBySiteMarkerName(OrganizationServiceContext serviceContext, EntityReference website, string siteMarkerName, out Entity page)
		{
			var fetch = new Fetch
			{
				Entity = new FetchEntity("adx_webpage")
				{
					Filters = new[]
					{
						new Filter
						{
							Conditions = new[]
							{
								new Services.Query.Condition("adx_websiteid", ConditionOperator.Equal, website.Id)
							}
						}
					},
					Links = new[]
					{
						new Link
						{
							Name = "adx_sitemarker",
							ToAttribute = "adx_webpageid",
							FromAttribute = "adx_pageid",
							Filters = new[]
							{
								new Filter
								{
									Conditions = new[]
									{
										new Services.Query.Condition("adx_pageid", ConditionOperator.NotNull),
										new Services.Query.Condition("adx_name", ConditionOperator.Equal, siteMarkerName.Replace("['", string.Empty).Replace("']", string.Empty)),
										new Services.Query.Condition("adx_websiteid", ConditionOperator.Equal, website.Id)
									}
								}
							}
						}
					}
				}
			};

			page = serviceContext.RetrieveSingle(fetch);

			return page != null;
		}

		/// <summary>
		/// Redirects page to the specified sitemarker path
		/// </summary>
		/// <param name="context">DotLiquid Context</param>
		/// <param name="sitemarkerPath">Sitemarker Path</param>
		private void RedirectToUrl(Context context, string sitemarkerPath)
		{
			IPortalLiquidContext portalLiquidContext;

			if (!context.TryGetPortalLiquidContext(out portalLiquidContext))
			{
				return;
			}

			var html = portalLiquidContext.Html;

			if (html.ViewContext == null || html.ViewContext.HttpContext == null || html.ViewContext.HttpContext.Response == null)
			{
				return;
			}

			html.ViewContext.HttpContext.Response.Redirect(sitemarkerPath);
		}

		
	}
}
