using DotLiquid.Util;

namespace Adxstudio.Xrm.Web.Mvc.Liquid.Tags
{
	internal static class TagSyntax
	{
		internal static readonly string Attributes = R.Q(string.Format(@"(\w+)\s*:\s*({0})", DotLiquid.Liquid.QuotedFragment));
	}
}
