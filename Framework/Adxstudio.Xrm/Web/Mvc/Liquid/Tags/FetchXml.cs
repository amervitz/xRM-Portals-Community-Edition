/*
  Copyright (c) Microsoft Corporation. All rights reserved.
  Licensed under the MIT License. See License.txt in the project root for license information.
*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Adxstudio.Xrm.Security;
using Adxstudio.Xrm.Services.Query;
using DotLiquid;
using DotLiquid.Exceptions;
using DotLiquid.Util;
using Adxstudio.Xrm.ContentAccess;

namespace Adxstudio.Xrm.Web.Mvc.Liquid.Tags
{
	/// <summary>
	/// Provides support for arbirary FetchXML queries through Liquid, with optional support for Entity Permissions.
	/// </summary>
	/// <example>
	/// <![CDATA[
	/// {% fetchxml query enable_entity_permissions: true, right: 'read' %}
	///	  <fetch mapping="logical" returntotalrecordcount="true">
	///		<entity name="{{ params.entityname | default: 'contact' | h }}">
	///		  <all-attributes />
	///		</entity>
	///	  </fetch>
	///	{% endfetchxml %}
	///
	///	<pre>{{ query.xml | h }}</pre>
	///
	///	<p>{{ query.results.total_record_count }}</p>
	///
	///	<ul>
	///	  {% for record in query.results.entities %}
	///		<li>{{ record.fullname | h }} ({{ record.id | h }})</li>
	///	  {% endfor %}
	///	</ul>
	/// ]]>
	/// </example>
	public class FetchXml : Block
	{
		private static readonly Regex Syntax = new Regex(@"(?<variable>\w+)");

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
				throw new SyntaxException("Syntax Error in '{0}' tag - Valid syntax: {0} [var] (right:[string])", tagName);
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

			using (TextWriter xml = new StringWriter())
			{
				base.Render(context, xml);

				var fetch = Fetch.Parse(xml.ToString());

				var right = GetRight(context);

				CrmEntityPermissionProvider.EntityPermissionRightResult permissionResult = new CrmEntityPermissionProvider()
					.TryApplyRecordLevelFiltersToFetch(portalLiquidContext.PortalViewContext.CreateServiceContext(), right, fetch);

                // Apply Content Access Level filtering
                var contentAccessLevelProvider = new ContentAccessLevelProvider();
                contentAccessLevelProvider.TryApplyRecordLevelFiltersToFetch(right, fetch);
                
                // Apply Product filtering
                var productAccessProvider = new ProductAccessProvider();
                productAccessProvider.TryApplyRecordLevelFiltersToFetch(CrmEntityPermissionRight.Read, fetch);

                context.Scopes.Last()[_variableName] = new FetchXmlQueryDrop(portalLiquidContext, fetch, permissionResult);
			}
		}

		private CrmEntityPermissionRight GetRight(Context context)
		{
			string right;
			CrmEntityPermissionRight parsedRight;

			return TryGetAttributeValue(context, "right", out right) && Enum.TryParse(right, true, out parsedRight)
				? parsedRight
				: CrmEntityPermissionRight.Read;
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
