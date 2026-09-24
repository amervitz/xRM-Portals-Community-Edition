/*
  Copyright (c) Microsoft Corporation. All rights reserved.
  Licensed under the MIT License. See License.txt in the project root for license information.
*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Web;
using DotLiquid;

namespace Adxstudio.Xrm.Web.Mvc.Liquid.Tags
{
	public class OutputCache : Tag
	{
		private IDictionary<string, string> _attributes;

		public override void Initialize(string tagName, string markup, List<string> tokens)
		{
			_attributes = new Dictionary<string, string>(Template.NamingConvention.StringComparer);

			TagSyntax.ScanAttributes(markup, (key, value) => _attributes[key] = value);

			base.Initialize(tagName, markup, tokens);
		}

		public override void Render(Context context, TextWriter result)
		{
			HttpCachePolicyBase cache;

			if (!TryGetResponseCachePolicy(context, out cache))
			{
				return;
			}

			object value;
			HttpCacheability cacheability;

			if (TryGetAttributeValue(context, "cacheability", out value) && Enum.TryParse(value.ToString(), out cacheability))
			{
				cache.SetCacheability(cacheability);
			}
		}

		protected bool TryGetResponseCachePolicy(Context context, out HttpCachePolicyBase cache)
		{
			cache = null;

			IPortalLiquidContext portalLiquidContext;

			if (!context.TryGetPortalLiquidContext(out portalLiquidContext))
			{
				return false;
			}

			var html = portalLiquidContext.Html;

			if (html.ViewContext == null || html.ViewContext.HttpContext == null || html.ViewContext.HttpContext.Response == null)
			{
				return false;
			}

			cache = html.ViewContext.HttpContext.Response.Cache;

			return cache != null;
		}

		private bool TryGetAttributeValue(Context context, string name, out object value)
		{
			value = null;

			string variable;

			if (!_attributes.TryGetValue(name, out variable))
			{
				return false;
			}

			value = context[variable];

			return value != null;
		}
	}
}
