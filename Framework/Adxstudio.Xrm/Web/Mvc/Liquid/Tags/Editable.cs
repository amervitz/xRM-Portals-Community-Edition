/*
  Copyright (c) Microsoft Corporation. All rights reserved.
  Licensed under the MIT License. See License.txt in the project root for license information.
*/

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using DotLiquid;
using DotLiquid.Exceptions;
using DotLiquid.Util;

namespace Adxstudio.Xrm.Web.Mvc.Liquid.Tags
{
	public class Editable : Tag
	{
		private static readonly Regex Syntax = new Regex(string.Format(@"(?<editable>{0})(\s+(?<key>{0})?)", DotLiquid.Liquid.QuotedFragment));

		private IDictionary<string, string> _attributes;
		private string _editable;
		private string _key;

		public override void Initialize(string tagName, string markup, List<string> tokens)
		{
			var syntaxMatch = Syntax.Match(markup);

			if (syntaxMatch.Success)
			{
				_editable = syntaxMatch.Groups["editable"].Value;
				_key = syntaxMatch.Groups["key"].Value;

				_attributes = new Dictionary<string, string>(Template.NamingConvention.StringComparer);

				R.Scan(markup, TagSyntax.Attributes, (key, value) => _attributes[key] = value);
			}
			else
			{
				throw new SyntaxException("Syntax Error in '{0}' tag - Valid syntax: {0} [editable] ([key]) (type:[type])", tagName);
			}

			base.Initialize(tagName, markup, tokens);
		}

		public override void Render(Context context, TextWriter result)
		{
			var attributes = _attributes.ToDictionary(e => e.Key, e => context[e.Value]);

			var key = context[_key] as string;

			if (string.IsNullOrEmpty(key))
			{
				var editable = context[_editable] as IEditable;

				if (editable == null)
				{
					return;
				}

				result.Write(editable.GetEditable(context, new EditableOptions(attributes)) ?? string.Empty);
			}
			else
			{
				var editable = context[_editable] as IEditableCollection;

				if (editable == null)
				{
					return;
				}

				result.Write(editable.GetEditable(context, key, new EditableOptions(attributes)) ?? string.Empty);
			}
		}
	}
}
