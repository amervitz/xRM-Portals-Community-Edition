/*
  Copyright (c) Microsoft Corporation. All rights reserved.
  Licensed under the MIT License. See License.txt in the project root for license information.
*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Web;
using System.IdentityModel.Services;
using Microsoft.Xrm.Client;
using Microsoft.Xrm.Client.Diagnostics;
using Microsoft.Xrm.Portal.IdentityModel.Configuration;
using Microsoft.Xrm.Portal.IdentityModel.Web.Modules;
using Microsoft.Xrm.Portal.Web;
using Microsoft.Xrm.Portal.Web.Security;
using Microsoft.Xrm.Portal.Web.Security.LiveId;

namespace Microsoft.Xrm.Portal.IdentityModel.Web.Handlers
{
	public class LiveIdAccountTransferHandler : IHttpHandler
	{
		private class Message : WSFederationMessage
		{
			public Message(Uri baseUrl) : base(baseUrl, "wsignin1.0") { Parameters.Clear(); }
			public override void Write(TextWriter writer) { }
		}

		#region Constructors

		public LiveIdAccountTransferHandler()
			: this(FederationCrmConfigurationManager.GetUserRegistrationSettings())
		{
		}

		public LiveIdAccountTransferHandler(IUserRegistrationSettings registrationSettings)
		{
			RegistrationSettings = registrationSettings;
		}

		#endregion

		private static readonly string _className = typeof(LiveIdAccountTransferHandler).FullName;
		private static readonly WindowsLiveLogin _windowsLiveLogin = new WindowsLiveLogin(true);

		public virtual IUserRegistrationSettings RegistrationSettings { get; private set; }

		protected string LiveIdTokenKey
		{
			get { return SelectRegistrationSetting(setting => setting.LiveIdTokenKey, "live-id-token"); }
		}

		protected string ReturnUrlKey
		{
			get { return SelectRegistrationSetting(setting => setting.ReturnUrlKey, "returnurl"); }
		}

		protected string ResultCodeKey
		{
			get { return SelectRegistrationSetting(setting => setting.ResultCodeKey, "result-code"); }
		}

		protected string DefaultReturnPath
		{
			get { return SelectRegistrationSetting(setting => setting.DefaultReturnPath, "~/"); }
		}

		public virtual string AccountTransferPath
		{
			get { return SelectRegistrationSetting(setting => setting.AccountTransferPath, DefaultReturnPath); }
		}

		public virtual string UnregisteredUserPath
		{
			get { return SelectRegistrationSetting(setting => setting.UnregisteredUserPath, DefaultReturnPath); }
		}

		public virtual string RegistrationPath
		{
			get { return SelectRegistrationSetting(setting => setting.RegistrationPath, DefaultReturnPath); }
		}

		protected string ErrorPath
		{
			get { return SelectRegistrationSetting(setting => setting.ErrorPath, DefaultReturnPath); }
		}

		private string SelectRegistrationSetting(Func<IUserRegistrationSettings, string> selector, string defaultValue)
		{
			return SelectSetting(RegistrationSettings, selector, defaultValue);
		}

		private static string SelectSetting<T>(T setting, Func<T, string> selector, string defaultValue) where T : class
		{
			return setting != null ? selector(setting) ?? defaultValue : defaultValue;
		}

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
			try
			{
				TryHandleSignInResponse(context);
			}
			catch (Exception exception)
			{
				if (!TryHandleException(context, exception))
				{
					throw new FederationAuthenticationException("Federated sign-in error.", exception);
				}
			}
		}

		/// <summary>
		/// Set the Live ID cookie and redirect to the login destination or the registration destination.
		/// </summary>
		protected virtual bool TryHandleSignInResponse(HttpContext context)
		{
			var user = _windowsLiveLogin.ProcessLogin(context.Request.Form);

			TraceInformation("TryHandleSignInResponse", "user.Id={0}", user.Id);

			if (LiveIdMembershipProvider.Current.ValidateUser(user.Id, user.Id))
			{
				if (RegistrationSettings != null && !string.IsNullOrEmpty(RegistrationSettings.AccountTransferPath))
				{
					// go to the account transfer page

					TraceInformation("TryHandleSignInResponse", "accountTransferPath={0}", AccountTransferPath);

					return TryHandleAccountTransferPageRedirect(context);
				}
				
				// go straight to ACS

				return TryHandleImmediateAcsRedirect(context);
			}

			// invalid user account

			return TryHandleUnregisteredUser(context);
		}

		public virtual bool TryHandleAccountTransferPageRedirect(HttpContext context)
		{
			var transferUri = GetReturnUri(context, AccountTransferPath);

			TraceInformation("TryHandleAccountTransferPageRedirect", "transferUri={0}", transferUri);

			var message = new Message(transferUri);
			message.Parameters.Add(LiveIdTokenKey, context.Request["stoken"]);
			message.Parameters.Add(ReturnUrlKey, context.Request[ReturnUrlKey]);

			var post = message.WriteFormPost();

			context.Response.Write(post);

			return true;
		}

		public virtual bool TryHandleImmediateAcsRedirect(HttpContext context)
		{
			var signInContext = new Dictionary<string, string>
			{
				{ LiveIdTokenKey, context.Request["stoken"] },
				{ ReturnUrlKey, context.Request[ReturnUrlKey] },
			};

			var fam = new CrmFederationAuthenticationModule(context);
			var signInUrl = fam.GetSignInRequestUrl(signInContext);

			TraceInformation("TryHandleImmediateAcsRedirect", "signInUrl={0}", signInUrl);

			context.RedirectAndEndResponse(signInUrl);

			return true;
		}

		public virtual bool TryHandleUnregisteredUser(HttpContext context)
		{
			// redirect to the unregistered page

			var returnPath = RegistrationSettings != null && !string.IsNullOrEmpty(RegistrationSettings.UnregisteredUserPath)
				? UnregisteredUserPath
				: "{0}{1}{2}={3}".FormatWith(RegistrationPath, RegistrationPath.Contains("?") ? "&" : "?", ResultCodeKey, "unregistered");

			TraceInformation("TryHandleException", "returnPath={0}", returnPath);

			context.RedirectAndEndResponse(returnPath);

			return true;
		}

		public virtual bool TryHandleException(HttpContext context, Exception exception)
		{
			// redirect to the error page if it is specified

			if (RegistrationSettings != null && !string.IsNullOrEmpty(RegistrationSettings.ErrorPath))
			{
				var returnPath = ErrorPath;

				TraceInformation("TryHandleException", "returnPath={0}", returnPath);

				context.RedirectAndEndResponse(returnPath);

				return true;
			}

			return false;
		}

		protected static Uri GetReturnUri(HttpContext context, string path)
		{
			var baseUri = context.Request.Url.GetLeftPart(UriPartial.Authority);
			var returnPath = VirtualPathUtility.ToAbsolute(path);

			return new Uri(baseUri + returnPath);
		}

		private static void TraceInformation(string memberName, string format, params object[] args)
		{
			Tracing.FrameworkInformation(_className, memberName, format, args);
		}
	}
}
