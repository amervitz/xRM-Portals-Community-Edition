/*
  Copyright (c) Microsoft Corporation. All rights reserved.
  Licensed under the MIT License. See License.txt in the project root for license information.
*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web;
using Adxstudio.Xrm.Resources;
using Adxstudio.Xrm.Search;
using DotLiquid;
using DotLiquid.Exceptions;
using Microsoft.Xrm.Client;
using Microsoft.Xrm.Portal.Configuration;
using Adxstudio.Xrm.AspNet.Cms;

namespace Adxstudio.Xrm.Web.Mvc.Liquid.Tags
{
	public class SearchIndex : Block
	{
		private static readonly Regex Syntax = new Regex(@"((?<variable>\w+)\s*=\s*)?(?<attributes>.*)");

		private IDictionary<string, string> _attributes;
		private string _variableName;

		public override void Initialize(string tagName, string markup, List<string> tokens)
		{
			var syntaxMatch = Syntax.Match(markup);

			if (syntaxMatch.Success)
			{
				_variableName = syntaxMatch.Groups["variable"].Value.Trim();
				_attributes = new Dictionary<string, string>(Template.NamingConvention.StringComparer);

				TagSyntax.ScanAttributes(markup, (key, value) => _attributes[key] = value);
			}
			else
			{
				throw new SyntaxException(
					ResourceManager.GetString("Syntax_Error_In_Tag_And_Valid_Syntax_Is") + "[[var] =] query:[string] (filter:[string]) (logical_names:[string]|[array]) (page:[integer]) (pagesize:[integer]) (provider:[string])",
					tagName);
			}

			base.Initialize(tagName, markup, tokens);
		}

		public override void Render(Context context, TextWriter result)
		{
			IPortalLiquidContext portalLiquidContext;

			if (!context.TryGetPortalLiquidContext(out portalLiquidContext))
			{
				return;
			}

			var searchProvider = GetSearchProvider(context);

			// Allow empty query text, to allow return of top-ranking results based only on filter.
			string queryText = TryGetAttributeValue(context, "query", out queryText)
				? queryText
				: string.Empty;

			string filter;

			TryGetAttributeValue(context, "filter", out filter);

			string pageValue;

			int page = TryGetAttributeValue(context, "page", out pageValue)
				&& int.TryParse(pageValue, out page)
				? page
				: 1;

			string pageSizeValue;
			
			int pageSize = (TryGetAttributeValue(context, "page_size", out pageSizeValue) || TryGetAttributeValue(context, "pagesize", out pageSizeValue))
				&& int.TryParse(pageSizeValue, out pageSize)
				? pageSize
				: 10;

			string logicalNamesValue;

			var logicalNames = (TryGetAttributeValue(context, "logical_names", out logicalNamesValue) || TryGetAttributeValue(context, "logicalnames", out logicalNamesValue))
				? logicalNamesValue.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries)
				: Enumerable.Empty<string>();

			// TODO check this here for context when this is called to make sure this still works
			var contextLanguage = HttpContext.Current.GetContextLanguageInfo();

			var query = new CrmEntityQuery(queryText, page, pageSize, logicalNames, contextLanguage.ContextLanguage, contextLanguage.IsCrmMultiLanguageEnabled, filter);

			context.Stack(() =>
			{
				context[string.IsNullOrEmpty(_variableName) ? "searchindex" : _variableName] = new SearchIndexQueryDrop(portalLiquidContext, searchProvider, query);

				RenderAll(NodeList, context, result);
			});
		}

		private SearchProvider GetSearchProvider(Context context)
		{
			if (!SearchManager.Enabled)
			{
				throw new InvalidOperationException("Search isn't enabled for the current application.");
			}

			string providerAttribute;

			if (_attributes.TryGetValue("provider", out providerAttribute))
			{
				var providerName = context[providerAttribute] as string;

				if (providerName == null)
				{
					throw new InvalidOperationException("Search provider name can't be null.");
				}

				var namedProvider = SearchManager.GetProvider(providerName);

				if (namedProvider == null)
				{
					throw new InvalidOperationException("Failed to retrieve search provider {0}.".FormatWith(providerName));
				}

				return namedProvider;
			}

			var defaultProvider = SearchManager.Provider;

			if (defaultProvider == null)
			{
				throw new InvalidOperationException("Failed to retrieve default search provider for the current application.");
			}

			return defaultProvider;
		}

		private bool TryGetAttributeValue(Context context, string name, out string value)
		{
			value = null;

			string variable;

			if (!_attributes.TryGetValue(name, out variable))
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
