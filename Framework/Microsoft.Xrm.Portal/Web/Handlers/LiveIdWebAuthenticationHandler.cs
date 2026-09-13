/*
  Copyright (c) Microsoft Corporation. All rights reserved.
  Licensed under the MIT License. See License.txt in the project root for license information.
*/

using System.Web;
using System.Web.Security;
using Microsoft.Xrm.Client;
using Microsoft.Xrm.Client.Diagnostics;
using Microsoft.Xrm.Portal.Web.Security;
using Microsoft.Xrm.Portal.Web.Security.LiveId;
using Encoder = System.Web.Security.AntiXss.AntiXssEncoder;

namespace Microsoft.Xrm.Portal.Web.Handlers
{
	public sealed class LiveIdWebAuthenticationHandler : IHttpHandler
	{
		private static readonly string ClassName = typeof(LiveIdWebAuthenticationHandler).FullName;
		private static readonly WindowsLiveLogin WindowsLiveLogin = new WindowsLiveLogin(true);

		/// <summary>
		/// Gets a value indicating whether another request can use the <see cref="T:System.Web.IHttpHandler" /> instance.
		/// </summary>
		/// <returns>
		/// true if the <see cref="T:System.Web.IHttpHandler" /> instance is reusable; otherwise, false.
		/// </returns>
		public bool IsReusable
		{
			get { return false; }
		}

		/// <summary>
		/// Enables processing of HTTP Web requests by a custom HttpHandler that implements the <see cref="T:System.Web.IHttpHandler" /> interface.
		/// </summary>
		/// <param name="context">An <see cref="T:System.Web.HttpContext" /> object that provides references to the intrinsic server objects (for example, Request, Response, Session, and Server) used to service HTTP requests. </param>
		public void ProcessRequest(HttpContext context)
		{
			switch (context.Request["action"])
			{
				case "logout":
					SignOutAndRedirectToSignedOutUrl(context);
					break;
				case "clearcookie":
					SignOutAndReturnSuccessResponse(context);
					break;
				default:
					ProcessLogin(context);
					break;
			}
		}

		private static string BuildSafeUrlForRegistrationDestinationWithLoginUrlOnQueryString(string registrationDestinationPath, string loginDestinationPath)
		{
			var safeUrl = new UrlBuilder(SafeUrl(registrationDestinationPath));

			safeUrl.QueryString.Set("LoginURL", SafeUrl(loginDestinationPath));

			return safeUrl.PathWithQueryString;
		}

		/// <summary>
		/// Set the Live ID cookie and redirect to the login destination or the registration destination.
		/// </summary>
		private static void ProcessLogin(HttpContext context)
		{
			WindowsLiveLogin.User user = WindowsLiveLogin.ProcessLogin(context.Request.Form);

			if (user == null)
			{
				SignOutAndRedirectToSignedOutUrl(context);
				return;
			}

			if (LiveIdMembershipProvider.Current.ValidateUser(user.Id, user.Id))
			{
				Tracing.FrameworkInformation(ClassName, "ProcessLogin", "User is registered -- redirecting to login destination");

				FormsAuthentication.SetAuthCookie(user.Id, user.UsePersistentCookie);

				context.Response.Redirect(SafeUrl(user.Context.LoginDestinationPath));
			}
			else
			{
				if (string.IsNullOrEmpty(user.Context.RegistrationDestinationPath) && !WindowsLiveLogin.AutoRegister)
				{
					Tracing.FrameworkInformation(ClassName, "ProcessLogin", "User not registered -- registration destination not provided -- automatic registration not permitted -- signing out");

					SignOutAndRedirectToSignedOutUrl(context);
					
					return;
				}
				
				if (string.IsNullOrEmpty(user.Context.RegistrationDestinationPath) && WindowsLiveLogin.AutoRegister)
				{
					Tracing.FrameworkInformation(ClassName, "ProcessLogin", "User not registered -- registration destination not provided -- automatically registering and redirecting");

					MembershipCreateStatus status;

					var membershipUser = LiveIdMembershipProvider.Current.CreateUser(user.Id, user.Id, null, null, null, true, null, out status);

					if (membershipUser == null) throw new MembershipCreateUserException(status);

					FormsAuthentication.SetAuthCookie(user.Id, user.UsePersistentCookie);

					context.Response.Redirect(SafeUrl(user.Context.LoginDestinationPath));
				}
				else
				{
					Tracing.FrameworkInformation(ClassName, "ProcessLogin", "User not registered -- posting to registration destination");

					var autoPostFormToRegistrationDestinationHtml = @"
						<html>
						<head>
							<title></title>
							<script type=""text/javascript"">
								function OnBack() {{ }}
								function DoSubmit() {{
									var submitted = false;
									if (!submitted) {{ submitted = true; document.fmHF.submit(); }}
								}}
							</script>
						</head>
						<body onload=""javascript:DoSubmit();"">
						<form name=""fmHF"" id=""fmHF"" action=""{0}"" method=""post"" target=""_top"">
							<input type=""hidden"" name=""live-id-token"" id=""live-id-token"" value=""{1}"">
							<input type=""hidden"" name=""live-id-context"" id=""live-id-context"" value=""{2}"">
							<input type=""hidden"" name=""live-id-action"" id=""live-id-action"" value=""register"">
						</form>
						</body>
						</html>".FormatWith(
							BuildSafeUrlForRegistrationDestinationWithLoginUrlOnQueryString(user.Context.RegistrationDestinationPath, user.Context.LoginDestinationPath),
							Encoder.XmlAttributeEncode(context.Request["stoken"]),
							Encoder.XmlAttributeEncode(context.Request["appctx"]));

					context.Response.Write(autoPostFormToRegistrationDestinationHtml);
				}
			}
		}

		private static string SafeUrl(string url)
		{
			try
			{
				return new UrlBuilder(url).PathWithQueryString;
			}
			catch
			{
				return "/";
			}
		}

		private static void SignOutAndRedirectToSignedOutUrl(HttpContext context)
		{
			FormsAuthentication.SignOut();

			context.Response.Redirect(WindowsLiveLogin.SignedOutUrl);
		}

		private static void SignOutAndReturnSuccessResponse(HttpContext context)
		{
			FormsAuthentication.SignOut();

			string type;
			byte[] content;

			WindowsLiveLogin.GetClearCookieResponse(out type, out content);

			context.Response.ContentType = type;
			context.Response.OutputStream.Write(content, 0, content.Length);
			context.Response.End();
		}
	}
}
