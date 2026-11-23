namespace Adxstudio.Xrm.AspNet.Cms
{
	using Microsoft.Owin;
	using Microsoft.Owin.Host.SystemWeb;
	using Microsoft.Owin.Infrastructure;

	/// <summary>
	/// The <see cref="ICookieManager"/> for the portal's OWIN authentication middleware.
	/// </summary>
	/// <remarks>
	/// Cookies are written through the System.Web response cookie collection, so cookies that ASP.NET adds to the same
	/// response can't overwrite them, and large cookies are split into chunks. Katana marks its correlation and nonce
	/// cookies <c>SameSite=None</c> but only makes them <c>Secure</c> when the request reached the server over HTTPS.
	/// Browsers reject <c>SameSite=None</c> cookies that aren't <c>Secure</c>, which breaks external sign-in behind a
	/// TLS-terminating proxy and on <c>http://localhost</c>, so this manager always marks those cookies <c>Secure</c>.
	/// </remarks>
	public sealed class AuthenticationCookieManager : ICookieManager
	{
		private readonly ICookieManager inner = new SystemWebChunkingCookieManager();

		/// <summary>
		/// Reads a cookie from the request, reassembling it if it was chunked.
		/// </summary>
		public string GetRequestCookie(IOwinContext context, string key)
		{
			return this.inner.GetRequestCookie(context, key);
		}

		/// <summary>
		/// Adds a cookie to the response, chunking it if it's too large.
		/// </summary>
		public void AppendResponseCookie(IOwinContext context, string key, string value, CookieOptions options)
		{
			RequireSecureForSameSiteNone(options);
			this.inner.AppendResponseCookie(context, key, value, options);
		}

		/// <summary>
		/// Expires a cookie and any chunks of it that the request sent.
		/// </summary>
		public void DeleteCookie(IOwinContext context, string key, CookieOptions options)
		{
			RequireSecureForSameSiteNone(options);
			this.inner.DeleteCookie(context, key, options);
		}

		private static void RequireSecureForSameSiteNone(CookieOptions options)
		{
			if (options != null && options.SameSite == SameSiteMode.None)
			{
				options.Secure = true;
			}
		}
	}
}
