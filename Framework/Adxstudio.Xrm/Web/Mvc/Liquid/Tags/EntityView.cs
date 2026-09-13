/*
  Copyright (c) Microsoft Corporation. All rights reserved.
  Licensed under the MIT License. See License.txt in the project root for license information.
*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using Adxstudio.Xrm.Cms;
using Adxstudio.Xrm.Web.UI;
using Adxstudio.Xrm.Web.UI.CrmEntityListView;
using DotLiquid;
using DotLiquid.Exceptions;
using DotLiquid.Util;

namespace Adxstudio.Xrm.Web.Mvc.Liquid.Tags
{
	public class EntityView : Block
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

				R.Scan(markup, TagSyntax.Attributes, (key, value) => _attributes[key] = value);
			}
			else
			{
				throw new SyntaxException("Syntax Error in '{0}' tag - Valid syntax: {0} [[var] =] (name:[string] | id:[string]) (filter:[string]) (search:[string]) (metafilter:[string]) (order:[integer]) (page:[integer]) (pagesize:[integer]) (languagecode:[integer])", tagName);
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

			var portalViewContext = portalLiquidContext.PortalViewContext;
			var viewConfiguration = GetViewConfiguration(portalViewContext, context);

			if (viewConfiguration == null)
			{
				return;
			}

			string pageSize;
			int parsedPageSize;

			if ((TryGetAttributeValue(context, "pagesize", out pageSize) || TryGetAttributeValue(context, "page_size", out pageSize)) && int.TryParse(pageSize, out parsedPageSize))
			{
				viewConfiguration.Item1.PageSize = Math.Max(1, parsedPageSize);
			}

			var viewDataAdapter = new ViewDataAdapter(viewConfiguration.Item1, new PortalConfigurationDataAdapterDependencies(portalViewContext.PortalName));

			string filter;

			if (TryGetAttributeValue(context, "filter", out filter))
			{
				viewDataAdapter.Filter = filter;
			}

			string metaFilter;

			if (TryGetAttributeValue(context, "metafilter", out metaFilter))
			{
				viewDataAdapter.MetaFilter = metaFilter;
			}

			string order;

			if (TryGetAttributeValue(context, "order", out order))
			{
				viewDataAdapter.Order = order;
			}

			string page;
			int parsedPage;

			if (TryGetAttributeValue(context, "page", out page) && int.TryParse(page, out parsedPage))
			{
				viewDataAdapter.Page = Math.Max(1, parsedPage);
			}

			string search;

			if (TryGetAttributeValue(context, "search", out search))
			{
				viewDataAdapter.Search = search;
			}

			context.Stack(() =>
			{
				context[string.IsNullOrEmpty(_variableName) ? "entityview" : _variableName] = new EntityViewDrop(
					portalLiquidContext,
					viewConfiguration.Item1,
					viewDataAdapter,
					new Lazy<EntityListViewDrop>(() => new EntityListViewDrop(portalLiquidContext, viewConfiguration.Item1.GetEntityView(portalViewContext.CreateServiceContext(), viewConfiguration.Item2)), LazyThreadSafetyMode.None));

				RenderAll(NodeList, context, result);
			});
		}

		private Tuple<ViewConfiguration, int> GetViewConfiguration(IPortalViewContext portalViewContext, Context context)
		{
			var entityList = context[EntityList.ScopeVariableName] as EntityListDrop;

			return entityList == null
				? GetViewConfigurationFromAttributes(portalViewContext, context)
				: GetViewConfigurationFromEntityListContext(context, entityList);
		}

		private Tuple<ViewConfiguration, int> GetViewConfigurationFromAttributes(IPortalViewContext portalViewContext, Context context)
		{
			var view = GetViewFromAttributes(portalViewContext, context);

			if (view == null)
			{
				return null;
			}

			var configuration = new ViewConfiguration(view)
			{
				EnableEntityPermissions = true
			};

			configuration.Search.Enabled = true;

			string languageCode;
			int parsedLanguageCode;

			if ((TryGetAttributeValue(context, "languagecode", out languageCode) || TryGetAttributeValue(context, "language_code", out languageCode)) && int.TryParse(languageCode, out parsedLanguageCode))
			{
				return new Tuple<ViewConfiguration, int>(configuration, parsedLanguageCode);
			}

			return new Tuple<ViewConfiguration, int>(configuration, 0);
		}

		private Tuple<ViewConfiguration, int> GetViewConfigurationFromEntityListContext(Context context, EntityListDrop entityList)
		{
			if (!entityList.Views.Any())
			{
				return null;
			}

			if (!entityList.DefaultViewId.HasValue)
			{
				return null;
			}

			var configuration = new ViewConfiguration(
				entityList.EntityLogicalName,
				entityList.PrimaryKeyName,
				entityList.DefaultViewId.Value)
			{
				EnableEntityPermissions = entityList.EnableEntityPermissions
			};

			if (!string.IsNullOrWhiteSpace(entityList.FilterPortalUserAttributeName))
			{
				configuration.FilterPortalUserAttributeName = entityList.FilterPortalUserAttributeName;
			}

			if (!string.IsNullOrWhiteSpace(entityList.FilterAccountAttributeName))
			{
				configuration.FilterAccountAttributeName = entityList.FilterAccountAttributeName;
			}

			if (!string.IsNullOrWhiteSpace(entityList.FilterWebsiteAttributeName))
			{
				configuration.FilterWebsiteAttributeName = entityList.FilterWebsiteAttributeName;
			}

			configuration.FilterSettings.Enabled = entityList.FilterEnabled;

			if (!string.IsNullOrWhiteSpace(entityList.FilterDefinition))
			{
				configuration.FilterSettings.Definition = entityList.FilterDefinition;
			}

			configuration.Search.Enabled = entityList.SearchEnabled;

			if (entityList.PageSize.HasValue)
			{
				configuration.PageSize = Math.Max(1, entityList.PageSize.Value);
			}

			string selectedViewId;
			Guid parsedSelectedViewId;

			if (TryGetAttributeValue(context, "id", out selectedViewId) && Guid.TryParse(selectedViewId, out parsedSelectedViewId))
			{
				configuration.ViewId = parsedSelectedViewId;
			}

			string languageCode;
			int parsedLanguageCode;

			if ((TryGetAttributeValue(context, "languagecode", out languageCode) || TryGetAttributeValue(context, "language_code", out languageCode)) && int.TryParse(languageCode, out parsedLanguageCode))
			{
				return new Tuple<ViewConfiguration, int>(configuration, parsedLanguageCode);
			}

			return new Tuple<ViewConfiguration, int>(configuration, entityList.LanguageCode);
		}

		private SavedQueryView GetViewFromAttributes(IPortalViewContext portalViewContext, Context context)
		{
			string viewId;
			Guid parsedViewId;

			if (TryGetAttributeValue(context, "id", out viewId) && Guid.TryParse(viewId, out parsedViewId))
			{
				return new SavedQueryView(portalViewContext.CreateServiceContext(), parsedViewId);
			}

			string entityLogicalName;
			string viewName;

			if ((TryGetAttributeValue(context, "logicalname", out entityLogicalName) || TryGetAttributeValue(context, "logical_name", out entityLogicalName)) && TryGetAttributeValue(context, "name", out viewName))
			{
				return new SavedQueryView(portalViewContext.CreateServiceContext(), entityLogicalName, viewName);
			}

			return null;
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
