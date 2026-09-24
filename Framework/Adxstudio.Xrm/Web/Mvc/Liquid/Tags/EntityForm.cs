/*
  Copyright (c) Microsoft Corporation. All rights reserved.
  Licensed under the MIT License. See License.txt in the project root for license information.
*/

using System;
using System.Collections.Generic;
using System.IO;
using System.ServiceModel;
using System.Text.RegularExpressions;
using Adxstudio.Xrm.Services;
using Adxstudio.Xrm.Services.Query;
using DotLiquid;
using DotLiquid.Exceptions;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace Adxstudio.Xrm.Web.Mvc.Liquid.Tags
{
	public class EntityForm : Tag
	{
		private static readonly Regex Syntax = new Regex(string.Format(@"(?<variable>{0})(\s+(?<key>{0})?)", DotLiquid.Liquid.QuotedFragment));

		private IDictionary<string, string> _attributes;

		public override void Initialize(string tagName, string markup, List<string> tokens)
		{
			var syntaxMatch = Syntax.Match(markup);

			if (syntaxMatch.Success)
			{
				_attributes = new Dictionary<string, string>(Template.NamingConvention.StringComparer);
				TagSyntax.ScanAttributes(markup, (key, value) => _attributes[key] = value);
			}
			else
			{
				throw new SyntaxException("Syntax Error in '{0}' tag - Valid syntax: {0} [[var] =] (name:[string] | id:[string] | key:[string]) (languagecode:[integer])", tagName);
			}

			base.Initialize(tagName, markup, tokens);
		}

		public override void Render(Context context, TextWriter result)
		{
			var key = GetKey(context);
			if (!string.IsNullOrEmpty(key))
			{
				result.Write("<!--[% entityform id:{0} %]-->", key);
			}
		}

		private string GetKey(Context context)
		{
			IPortalLiquidContext portalLiquidContext;
			
			if (!context.TryGetPortalLiquidContext(out portalLiquidContext))
			{
				return string.Empty;
			}

			var portalOrgService = portalLiquidContext.PortalOrganizationService;
			try
			{
				string idVariable;

				if (_attributes.TryGetValue("id", out idVariable))
				{
					var idValue = context[idVariable];

					return idValue == null ? string.Empty : idValue.ToString();
				}

				string nameVariable;

				if (_attributes.TryGetValue("name", out nameVariable))
				{
					return GetEntityFormIdByName(portalOrgService, context, nameVariable);
				}

				string keyVariable;

				if (_attributes.TryGetValue("key", out keyVariable))
				{
					var keyValue = context[keyVariable];

					Guid id;

					if (keyValue != null && Guid.TryParse(keyValue.ToString(), out id))
					{
						return id.ToString();
					}

					return GetEntityFormIdByName(portalOrgService, context, keyVariable);
				}
			}
			catch (FaultException<OrganizationServiceFault>)
			{
				return string.Empty;
			}

			return string.Empty;
		}
		
		private string GetEntityFormIdByName(IOrganizationService serviceContext, Context context, string nameVariable)
		{
			var nameValue = context[nameVariable];

			if (nameValue == null)
			{
				return string.Empty;
			}

			var name = nameValue.ToString();

			if (string.IsNullOrWhiteSpace(name))
			{
				return string.Empty;
			}

			var entityForm = serviceContext.RetrieveSingle(
				"adx_entityform",
				FetchAttribute.None,
				new[] {
					new Services.Query.Condition("adx_name", ConditionOperator.Equal, name),
					new Services.Query.Condition("statecode", ConditionOperator.Equal, 0)
				});

			return entityForm == null ? string.Empty : entityForm.Id.ToString();
		}
	}
}
