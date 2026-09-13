/*
  Copyright (c) Microsoft Corporation. All rights reserved.
  Licensed under the MIT License. See License.txt in the project root for license information.
*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.ServiceModel;
using System.Text.RegularExpressions;
using DotLiquid;
using DotLiquid.Exceptions;
using DotLiquid.Util;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Client;

namespace Adxstudio.Xrm.Web.Mvc.Liquid.Tags
{
	public class WebForm : Tag
	{
		private static readonly Regex Syntax = new Regex(string.Format(@"(?<variable>{0})(\s+(?<key>{0})?)", DotLiquid.Liquid.QuotedFragment));

		private IDictionary<string, string> _attributes;

		public override void Initialize(string tagName, string markup, List<string> tokens)
		{
			var syntaxMatch = Syntax.Match(markup);

			if (syntaxMatch.Success)
			{
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
			var key = GetKey(context);
			if (!string.IsNullOrEmpty(key))
			{
				result.Write("<!--[% webform id:{0} %]-->", key);
			}
		}

		private string GetKey(Context context)
		{
			IPortalLiquidContext portalLiquidContext;
			
			if (!context.TryGetPortalLiquidContext(out portalLiquidContext))
			{
				return string.Empty;
			}

			using (var serviceContext = portalLiquidContext.PortalViewContext.CreateServiceContext())
			{
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
						return GetWebFormIdByName(serviceContext, context, nameVariable);
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

						return GetWebFormIdByName(serviceContext, context, keyVariable);
					}
				}
				catch (FaultException<OrganizationServiceFault>)
				{
					return string.Empty;
				}
			}

			return string.Empty;
		}
		
		private string GetWebFormIdByName(OrganizationServiceContext serviceContext, Context context, string nameVariable)
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

			var webForm = serviceContext.CreateQuery("adx_webform")
				.FirstOrDefault(e => e.GetAttributeValue<string>("adx_name") == name
					&& e.GetAttributeValue<int?>("statecode") == 0);

			return webForm == null ? string.Empty : webForm.Id.ToString();
		}
	}
}
