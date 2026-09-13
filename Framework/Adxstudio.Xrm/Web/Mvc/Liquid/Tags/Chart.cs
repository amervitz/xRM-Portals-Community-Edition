/*
  Copyright (c) Microsoft Corporation. All rights reserved.
  Licensed under the MIT License. See License.txt in the project root for license information.
*/

namespace Adxstudio.Xrm.Web.Mvc.Liquid.Tags
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Text.RegularExpressions;
	using System.Threading;
	using Adxstudio.Xrm.Web.Mvc.Html;
	using DotLiquid;
	using DotLiquid.Exceptions;
	using DotLiquid.Util;

	/// <summary>
	/// The Liquid tag for rendering a CRM chart. {% chart id:"527B944C-E948-4012-BB10-F00A538F9903" %}
	/// </summary>
	public class Chart : Tag
	{
		/// <summary>
		/// The expression used to match applicable markup for this particular Liquid tag.
		/// </summary>
		private static readonly Regex Syntax = new Regex(@"((?<variable>\w+)\s*=\s*)?(?<attributes>.*)");

		/// <summary>
		/// The attributes specified on the tag that match the <see cref="Syntax"/>.
		/// </summary>
		private IDictionary<string, string> attributes;

		/// <summary>
		/// Initialization of the liquid tag.
		/// </summary>
		/// <param name="tagName">The name of the tag.</param>
		/// <param name="markup">The liquid markup.</param>
		/// <param name="tokens">The list of tokens.</param>
		public override void Initialize(string tagName, string markup, List<string> tokens)
		{
			var syntaxMatch = Syntax.Match(markup);

			if (syntaxMatch.Success)
			{
				this.attributes = new Dictionary<string, string>(Template.NamingConvention.StringComparer);

				R.Scan(markup, TagSyntax.Attributes, (key, value) => this.attributes[key] = value);
			}
			else
			{
				throw new SyntaxException("Syntax Error in '{0}' tag - Valid syntax: {0} [[var] =] (id:[string]) (viewid:[string]) (serviceurl:[string])", tagName);
			}

			base.Initialize(tagName, markup, tokens);
		}

		/// <summary>
		/// Write the HTML to the page.
		/// </summary>
		/// <param name="context">The DotLiquid <see cref="Context"/></param>
		/// <param name="result">The <see cref="TextWriter"/> used to write out the HTML.</param>
		public override void Render(Context context, TextWriter result)
		{
			IPortalLiquidContext portalLiquidContext;

			if (!context.TryGetPortalLiquidContext(out portalLiquidContext))
			{
				return;
			}

			string chartId;
			Guid parsedChartId;

			if (!this.TryGetAttributeValue(context, "id", out chartId) || !Guid.TryParse(chartId, out parsedChartId))
			{
				throw new SyntaxException("Syntax Error in 'chart' tag. Missing required attribute 'id:[string]'");
			}

			string viewId;
			var parsedViewId = Guid.Empty;
			Guid? outViewId = null;
			var hasView = this.TryGetAttributeValue(context, "viewid", out viewId) && Guid.TryParse(viewId, out parsedViewId);

			if (hasView)
			{
				outViewId = parsedViewId;
			}

			string serviceUrl;

			if (!this.TryGetAttributeValue(context, "serviceurl", out serviceUrl))
			{
				serviceUrl = GetLazyServiceUrl(portalLiquidContext).Value;
			}

			var html = portalLiquidContext.Html.CrmChart(serviceUrl, parsedChartId, outViewId);

			result.Write(html);
		}

		/// <summary>
		/// Load the URL to the visualization service route to get the chart builder.
		/// </summary>
		/// <param name="portalLiquidContext">The current <see cref="IPortalLiquidContext"/>.</param>
		/// <returns>The URL to the service.</returns>
		private static Lazy<string> GetLazyServiceUrl(IPortalLiquidContext portalLiquidContext)
		{
			var url = portalLiquidContext.UrlHelper.RouteUrl("Visualizations_GetChartBuilder", new
			{
				__portalScopeId__ = portalLiquidContext.PortalViewContext.Website.EntityReference.Id
			});

			return new Lazy<string>(() => url, LazyThreadSafetyMode.None);
		}

		/// <summary>
		/// Attempts to get an attribute from the <see cref="Context"/>.
		/// </summary>
		/// <param name="context">The DotLiquid <see cref="Context"/></param>
		/// <param name="name">The name of the attribute.</param>
		/// <param name="value">The value of the attribute.</param>
		/// <returns>True if an attribute exists for the specified name, otherwise false. The attribute value will be assigned to the output parameter named value.</returns>
		private bool TryGetAttributeValue(Context context, string name, out string value)
		{
			value = null;

			string variable;

			if (!this.attributes.TryGetValue(name, out variable))
			{
				return false;
			}

			var raw = context[variable];

			if (raw != null)
			{
				value = raw.ToString();
			}

			return !string.IsNullOrWhiteSpace(value);
		}
	}
}
