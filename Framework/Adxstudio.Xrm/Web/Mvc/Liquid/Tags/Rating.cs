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
	using Adxstudio.Xrm.Web.Mvc.Html;
	using DotLiquid;
	using DotLiquid.Exceptions;
	using DotLiquid.Util;
	using Microsoft.Xrm.Sdk;

	/// <summary>
	/// The Liquid tag for rendering an entity rating. {% rating id: page.id, entity: page.logical_name, readonly: false, panel: true, snippet: "Rating Heading", step: "1", min: "0", max: "5", round: true %}
	/// </summary>
	public class Rating : Tag
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
				throw new SyntaxException("Syntax Error in '{0}' tag - Valid syntax: {0} [[var] =] (id:[string]) (entity:[string]) (readonly:[boolean]) (panel:[boolean]) (snippet:[string]) (step:[string]) (min:[string]) (max:[string]) (round:[boolean])", tagName);
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

			string id;
			Guid parsedId;

			if (!this.TryGetAttributeValue(context, "id", out id) || !Guid.TryParse(id, out parsedId))
			{
				throw new SyntaxException("Syntax Error in 'rating' tag. Missing required attribute 'id:[string]'");
			}

			string entity;

			if (!this.TryGetAttributeValue(context, "entity", out entity) || string.IsNullOrWhiteSpace(entity))
			{
				entity = "adx_webpage";
			}

			string readonlyString;
			var parsedReadonly = false;

			if (this.TryGetAttributeValue(context, "readonly", out readonlyString))
			{
				bool.TryParse(readonlyString, out parsedReadonly);
			}

			string panel;
			var parsedPanel = false;

			if (this.TryGetAttributeValue(context, "panel", out panel))
			{
				bool.TryParse(panel, out parsedPanel);
			}

			string panelSnippet;

			this.TryGetAttributeValue(context, "snippet", out panelSnippet);

			string step;

			this.TryGetAttributeValue(context, "step", out step);

			string min;

			this.TryGetAttributeValue(context, "min", out min);

			string max;

			this.TryGetAttributeValue(context, "max", out max);

			string round;
			var parsedRound = true;

			if (this.TryGetAttributeValue(context, "round", out round))
			{
				bool.TryParse(round, out parsedRound);
			}

			var entityReference = new EntityReference(entity, parsedId);

			var html = portalLiquidContext.Html.Rating(entityReference, panel: parsedPanel, panelTitleSnippetName: panelSnippet, isReadonly: parsedReadonly, step: step, min: min, max: max, roundNearestHalf: parsedRound);

			result.Write(html);
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
