/*
  Copyright (c) Microsoft Corporation. All rights reserved.
  Licensed under the MIT License. See License.txt in the project root for license information.
*/

namespace Site
{
	using System;
	using System.Web;
	using System.Web.Mvc;
	using Adxstudio.Xrm.AspNet.Cms;
	using Adxstudio.Xrm.AspNet.Identity;
	using Microsoft.AspNet.Identity;
	using Microsoft.Owin;
	using Microsoft.Owin.Security;
	using Microsoft.Owin.Security.Cookies;
	using Owin;
	using Site.Areas.Account.Models;

	public partial class Startup
	{
		// For more information on configuring authentication, please visit http://go.microsoft.com/fwlink/?LinkId=301864
		public void ConfigureAuth(IAppBuilder app, CrmWebsite website)
		{
			var url = new UrlHelper(HttpContext.Current.Request.RequestContext);
			var defaultAuthenticationType = website.Settings.Get<string>("Authentication/Registration/LoginButtonAuthenticationType");
			var loginPath = string.IsNullOrWhiteSpace(defaultAuthenticationType)
				? new PathString(url.Action("Login", "Login", new { area = "Account" }))
				: new PathString(url.Action("ExternalLogin", "Login", new { area = "Account", provider = defaultAuthenticationType }));
			var externalLoginCallbackPath = new PathString(url.Action("ExternalLoginCallback", "Login", new { area = "Account" }));
			var externalAuthenticationFailedPath = new PathString(url.Action("ExternalAuthenticationFailed", "Login", new { area = "Account" }));
			var externalPasswordResetPath = new PathString(url.Action("ExternalPasswordReset", "Login", new { area = "Account" }));
			var settingsManager = new ApplicationStartupSettingsManager(website, 
				(manager, user) => user.GenerateUserIdentityAsync(manager), loginPath, externalLoginCallbackPath, externalAuthenticationFailedPath, externalPasswordResetPath);

			// Configure user manager and role manager to use a single instance per request
			app.CreatePerOwinContext(() => settingsManager);
			app.CreatePerOwinContext<ApplicationInvitationManager>(ApplicationInvitationManager.Create);
			app.CreatePerOwinContext<ApplicationUserManager>(ApplicationUserManager.Create);
			app.CreatePerOwinContext<ApplicationSignInManager>(ApplicationSignInManager.Create);


			app.UseSiteMapAuthentication(settingsManager.ApplicationCookie);

			// Enable the application to use a cookie to store information for the signed in user
			// and to use a cookie to temporarily store information about a user logging in with a third party login provider
			// Configure the sign in cookie
			app.UseCookieAuthentication(settingsManager.ApplicationCookie);
			app.SetDefaultSignInAsAuthenticationType(DefaultAuthenticationTypes.ExternalCookie);
			app.UseCookieAuthentication(PassiveCookieOptions(DefaultAuthenticationTypes.ExternalCookie, TimeSpan.FromMinutes(5)));

			app.CreatePerOwinContext<CrmUser>(ApplicationUser.Create);

			// Enables the application to temporarily store user information when they are verifying the second factor in the two-factor authentication process.
			app.UseCookieAuthentication(PassiveCookieOptions(DefaultAuthenticationTypes.TwoFactorCookie, settingsManager.TwoFactorCookie.ExpireTimeSpan));

			// Enables the application to remember the second login verification factor such as phone or email.
			// Once you check this option, your second step of verification during the login process will be remembered on the device where you logged in from.
			// This is similar to the RememberMe option when you log in.
			app.UseCookieAuthentication(PassiveCookieOptions(DefaultAuthenticationTypes.TwoFactorRememberBrowserCookie, null));

			app.UsePortalsAuthentication(settingsManager);
		}

		/// <summary>
		/// The options that ASP.NET Identity's UseExternalSignInCookie, UseTwoFactorSignInCookie and
		/// UseTwoFactorRememberBrowserCookie use, with the portal's authentication cookie manager.
		/// </summary>
		private static CookieAuthenticationOptions PassiveCookieOptions(string authenticationType, TimeSpan? expireTimeSpan)
		{
			var options = new CookieAuthenticationOptions
			{
				AuthenticationType = authenticationType,
				AuthenticationMode = AuthenticationMode.Passive,
				CookieName = ".AspNet." + authenticationType,
				CookieManager = new AuthenticationCookieManager(),
			};

			if (expireTimeSpan != null) options.ExpireTimeSpan = expireTimeSpan.Value;

			return options;
		}
	}
}
