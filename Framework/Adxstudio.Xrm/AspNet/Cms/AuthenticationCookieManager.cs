/*
  Copyright (c) Microsoft Corporation. All rights reserved.
  Licensed under the MIT License. See License.txt in the project root for license information.
*/

namespace Adxstudio.Xrm.AspNet.Cms
{
	using Microsoft.Owin;
	using Microsoft.Owin.Host.SystemWeb;
	using Microsoft.Owin.Infrastructure;

	/// <summary>
	/// Preserves authentication cookies alongside ASP.NET cookies and enforces the
	/// Secure attribute required by browsers for SameSite=None cookies.
	/// </summary>
	public sealed class AuthenticationCookieManager : ICookieManager
	{
		private readonly ICookieManager _inner = new SystemWebChunkingCookieManager();

		public string GetRequestCookie(IOwinContext context, string key)
		{
			return _inner.GetRequestCookie(context, key);
		}

		public void AppendResponseCookie(IOwinContext context, string key, string value, CookieOptions options)
		{
			EnsureSecureSameSiteNone(options);
			_inner.AppendResponseCookie(context, key, value, options);
		}

		public void DeleteCookie(IOwinContext context, string key, CookieOptions options)
		{
			EnsureSecureSameSiteNone(options);
			_inner.DeleteCookie(context, key, options);
		}

		private static void EnsureSecureSameSiteNone(CookieOptions options)
		{
			// Katana derives Secure from the request scheme, even for SameSite=None.
			// Browsers reject that combination on HTTP localhost too. Secure cookies
			// work on Chromium's trusted localhost origin; deployed sites require HTTPS.
			if (options.SameSite == SameSiteMode.None) options.Secure = true;
		}
	}
}
