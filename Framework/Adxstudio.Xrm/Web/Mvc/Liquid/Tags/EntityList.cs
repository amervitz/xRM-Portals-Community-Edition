/*
  Copyright (c) Microsoft Corporation. All rights reserved.
  Licensed under the MIT License. See License.txt in the project root for license information.
*/

namespace Adxstudio.Xrm.Web.Mvc.Liquid.Tags
{
	using System;
	using System.Collections.Generic;
	using System.Globalization;
	using System.IO;
	using System.Linq;
	using System.ServiceModel;
	using System.Text.RegularExpressions;
	using System.Threading;
	using DotLiquid;
	using DotLiquid.Exceptions;
	using DotLiquid.Util;
	using Microsoft.Xrm.Sdk;
	using Microsoft.Xrm.Sdk.Metadata;
	using Microsoft.Xrm.Sdk.Query;
	using Adxstudio.Xrm.Core;
	using Adxstudio.Xrm.Services;
	using Adxstudio.Xrm.Services.Query;
	using Condition = Adxstudio.Xrm.Services.Query.Condition;

	public class EntityList : Block
	{
		public const string ScopeVariableName = "__entitylist__";

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
				throw new SyntaxException("Syntax Error in '{0}' tag - Valid syntax: {0} [[var] =] (name:[string] | id:[string] | key:[string]) (languagecode:[integer])", tagName);
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

			var drop = GetEntityListDrop(portalLiquidContext, context);

			if (drop == null)
			{
				return;
			}

			context.Stack(() =>
			{
				context[string.IsNullOrEmpty(_variableName) ? "entitylist" : _variableName] = drop;
				context[ScopeVariableName] = drop;

				RenderAll(NodeList, context, result);
			});
		}

		private EntityListDrop GetEntityListDrop(IPortalLiquidContext portalLiquidContext, Context context)
		{
			using (var serviceContext = portalLiquidContext.PortalViewContext.CreateServiceContext())
			{
				try
				{
					string idVariable;

					if (_attributes.TryGetValue("id", out idVariable))
					{
						return GetEntityListById(portalLiquidContext, context, idVariable);
					}

					string nameVariable;

					if (_attributes.TryGetValue("name", out nameVariable))
					{
						return GetEntityListByNameOrKey(portalLiquidContext, context, nameVariable);
					}

					string keyVariable;

					if (_attributes.TryGetValue("key", out keyVariable))
					{
						var keyValue = context[keyVariable];

						Guid id;

						if (keyValue != null && Guid.TryParse(keyValue.ToString(), out id))
						{
							return GetEntityListById(portalLiquidContext, context, keyVariable);
						}

						return GetEntityListByNameOrKey(portalLiquidContext, context, keyVariable);
					}
				}
				catch (FaultException<OrganizationServiceFault>)
				{
					return null;
				}
			}

			return null;
		}

		private EntityListDrop GetEntityListById(IPortalLiquidContext portalLiquidContext, Context context, string idVariable)
		{
			var idValue = context[idVariable];

			if (idValue == null)
			{
				return null;
			}

			Guid id;

			if (!Guid.TryParse(idValue.ToString(), out id))
			{
				return null;
			}

			var portalOrgService = portalLiquidContext.PortalOrganizationService;

			var entityList = portalOrgService.RetrieveSingle(
				"adx_entitylist",
				FetchAttribute.All,
				new[] {
					new Condition("adx_entitylistid", ConditionOperator.Equal, id),
					new Condition("statecode", ConditionOperator.Equal, 0)
				});

			return GetEntityListDrop(portalLiquidContext, context, entityList);
		}

		private EntityListDrop GetEntityListByNameOrKey(IPortalLiquidContext portalLiquidContext, Context context, string nameVariable)
		{
			var nameValue = context[nameVariable];

			if (nameValue == null)
			{
				return null;
			}

			var name = nameValue.ToString();

			if (string.IsNullOrWhiteSpace(name))
			{
				return null;
			}

			Entity entityList;

			var portalOrgService = portalLiquidContext.PortalOrganizationService;
			var entityMetadata = portalOrgService.GetEntityMetadata("adx_entitylist", EntityFilters.Attributes);

			// Must check if new attribute exists to maintain compatability with previous schema versions and prevent runtime 
			// exceptions when portal code updates are pushed to web apps where new solutions have not yet been applied.
			if (entityMetadata != null && entityMetadata.Attributes != null &&
				entityMetadata.Attributes.Select(a => a.LogicalName).Contains("adx_key"))
			{
				var fetch = new Fetch
				{
					Entity = new FetchEntity("adx_entitylist")
					{
						Attributes = FetchAttribute.All,
						Filters = new[]
						{
							new Filter
							{
								Type = LogicalOperator.And,
								Conditions = new[] { new Condition("statecode", ConditionOperator.Equal, 0) },
								Filters = new List<Filter>
								{
									new Filter
									{
										Type = LogicalOperator.Or,
										Conditions = new List<Condition>
										{
											new Condition("adx_name", ConditionOperator.Equal, name),
											new Condition("adx_key", ConditionOperator.Equal, name)
										}
									}
								}
							}
						}
					}
				};

				entityList = portalOrgService.RetrieveSingle(fetch);
			}
			else
			{
				entityList = portalOrgService.RetrieveSingle(
					"adx_entitylist",
					FetchAttribute.All,
					new[] {
						new Condition("adx_name", ConditionOperator.Equal, name),
						new Condition("statecode", ConditionOperator.Equal, 0)
					});
			}

			return GetEntityListDrop(portalLiquidContext, context, entityList);
		}

		private EntityListDrop GetEntityListDrop(IPortalLiquidContext portalLiquidContext, Context context, Entity entityList)
		{
			if (entityList == null)
			{
				return null;
			}
			
			return new EntityListDrop(
				portalLiquidContext,
				entityList,
				GetLazyGridDataUrl(portalLiquidContext),
				GetLazyModalFormTemplateUrl(portalLiquidContext),
				GetLazyWebPageUrl(portalLiquidContext.PortalViewContext, entityList.GetAttributeValue<EntityReference>("adx_webpageforcreate")),
				GetLazyWebPageUrl(portalLiquidContext.PortalViewContext, entityList.GetAttributeValue<EntityReference>("adx_webpagefordetailsview")),
				GetLazyLanguageCode(portalLiquidContext, context));
		}

		private Lazy<int> GetLazyLanguageCode(IPortalLiquidContext portalLiquidContext, Context context)
		{
			string languageCodeVariable;
			int? languageCode = null;

			if (_attributes.TryGetValue("language_code", out languageCodeVariable) || _attributes.TryGetValue("languagecode", out languageCodeVariable))
			{
				languageCode = context[languageCodeVariable] as int?;
			}

			// Note: entity list only supports CRM languages, so get the CrmLcid rather than the potentially custom language Lcid.
			return languageCode.HasValue && languageCode.Value > 0
				? new Lazy<int>(() => languageCode.Value, LazyThreadSafetyMode.None)
				: new Lazy<int>(() => portalLiquidContext.ContextLanguageInfo?.GetCrmLcid() ?? CultureInfo.CurrentCulture.LCID, LazyThreadSafetyMode.None);
		}

		private Lazy<string> GetLazyWebPageUrl(IPortalViewContext portalViewContext, EntityReference webPage)
		{
			if (portalViewContext == null)
			{
				return new Lazy<string>(() => null, LazyThreadSafetyMode.None);
			}

			if (webPage == null)
			{
				return new Lazy<string>(() => null, LazyThreadSafetyMode.None);
			}

			return new Lazy<string>(() =>
			{
				var urlProvider = portalViewContext.UrlProvider;

				if (urlProvider == null)
				{
					return null;
				}

				using (var serviceContext = portalViewContext.CreateServiceContext())
				{
					var entity = serviceContext.CreateQuery("adx_webpage")
						.FirstOrDefault(e => e.GetAttributeValue<Guid>("adx_webpageid") == webPage.Id
							&& e.GetAttributeValue<int?>("statecode") == 0);

					return entity == null ? null : urlProvider.GetUrl(serviceContext, entity);
				}
			}, LazyThreadSafetyMode.None);
		}

		private static Lazy<string> GetLazyGridDataUrl(IPortalLiquidContext portalLiquidContext)
		{
			var url = portalLiquidContext.UrlHelper.RouteUrl("PortalGetGridData", new
			{
				__portalScopeId__ = portalLiquidContext.PortalViewContext.Website.EntityReference.Id
			});

			return new Lazy<string>(() => url, LazyThreadSafetyMode.None);
		}

		private static Lazy<string> GetLazyModalFormTemplateUrl(IPortalLiquidContext portalLiquidContext)
		{
			var url = portalLiquidContext.UrlHelper.RouteUrl("PortalModalFormTemplatePath", new
			{
				__portalScopeId__ = portalLiquidContext.PortalViewContext.Website.EntityReference.Id
			});
			
			return new Lazy<string>(() => url, LazyThreadSafetyMode.None);
		}
	}
}
