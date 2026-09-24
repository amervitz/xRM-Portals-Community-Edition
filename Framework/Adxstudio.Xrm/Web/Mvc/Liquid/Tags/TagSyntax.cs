namespace Adxstudio.Xrm.Web.Mvc.Liquid.Tags
{
	using System;
	using System.Text.RegularExpressions;

	/// <summary>
	/// Parsing shared by the portal's Liquid tags.
	/// </summary>
	internal static class TagSyntax
	{
		/// <summary>
		/// Matches a <c>key: value</c> tag attribute. DotLiquid 2.0 exposed this as <c>Liquid.TagAttributes</c>,
		/// which later versions removed.
		/// </summary>
		private static readonly Regex Attributes = new Regex(string.Format(@"(\w+)\s*:\s*({0})", DotLiquid.Liquid.QuotedFragment), RegexOptions.Compiled);

		/// <summary>
		/// Invokes <paramref name="attribute"/> with the key and value of each attribute in the tag markup.
		/// </summary>
		/// <param name="markup">The tag markup.</param>
		/// <param name="attribute">Receives each attribute's key and value.</param>
		internal static void ScanAttributes(string markup, Action<string, string> attribute)
		{
			foreach (Match match in Attributes.Matches(markup))
			{
				attribute(match.Groups[1].Value, match.Groups[2].Value);
			}
		}
	}
}
