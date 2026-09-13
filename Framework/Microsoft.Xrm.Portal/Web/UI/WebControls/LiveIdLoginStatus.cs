/*
  Copyright (c) Microsoft Corporation. All rights reserved.
  Licensed under the MIT License. See License.txt in the project root for license information.
*/

using System;
using System.ComponentModel;
using System.Configuration;
using System.Drawing.Design;
using System.Web.Configuration;
using System.Web.UI;
using Microsoft.Xrm.Client;
using Microsoft.Xrm.Portal.IdentityModel.Web.Handlers;
using Microsoft.Xrm.Portal.Web.Handlers;
using Microsoft.Xrm.Portal.Web.Security;
using Microsoft.Xrm.Portal.Web.Security.LiveId;
using Encoder = System.Web.Security.AntiXss.AntiXssEncoder;

namespace Microsoft.Xrm.Portal.Web.UI.WebControls
{
	/// <summary>
	/// The LiveIdLoginStatus control displays a login link for users who are not authenticated and a logout link for users who are authenticated.
	/// When anonymous, the link takes the user to Windows Live or optionally (using LoginNavigateUrl) to a specified landing page that lets the user know they're going to Windows Live.
	/// When authenticated, the logout link resets the current user's identity to be an anonymous user.
	/// </summary>
	/// <remarks>
	/// When a LoginNavigateUrl is not specified, the link takes the user directly to Windows Live.
	/// 
	/// If a landing page is desired, set the LoginNavigateUrl to be your landing page, and then place this control on your landing page with no LoginNavigateUrl.
	/// 
	/// The application ID and secret key from your Live ID site registration must be provided in the connection string the <see cref="LiveIdMembershipProvider"/> uses.
	/// See the example below for the connection string syntax.
	/// 
	/// If you wish to provide a logout page URL, it must be provided in the connection string the <see cref="LiveIdMembershipProvider"/> uses.
	/// See the example below for the connection string syntax.
	/// 
	/// When returning from sign-in, if the user is authenticated but has never registered, they can be sent to a specified RegistrationUrl.
	/// If no RegistrationUrl is given, the user can be automatically registered if the Auto Register connection string setting is true.
	/// If there is no RegistrationUrl, and automatic registration is disabled, the user will be signed out.
	/// 
	/// <example>
	/// <![CDATA[
	/// <connectionStrings>
	///		<add name="LiveId" connectionString="Application Id=???; Secret=???; Signed Out Url=???; Auto Register=true"/>
	///	</connectionStrings>
	/// ]]>
	/// The Signed Out Url and Auto Register settings are optional.
	/// </example>
	/// 
	/// The return URL for your Live ID site registration should be the url to the LiveIdWebAuthenticationHandler that must be specified in the web.config.
	/// </remarks>
	[ToolboxData("<{0}:LiveIdLoginStatus runat=server></{0}:LiveIdLoginStatus>")]
	public sealed class LiveIdLoginStatus : System.Web.UI.WebControls.WebControl
	{
		private System.Web.UI.WebControls.HyperLink _hyperLink;
		private string _loginText;
		private string _logoutText;

		/// <summary>
		/// When returning from sign-in, if the user is authenticated and registered, they will be sent to the specified URL.
		/// </summary>
		[UrlProperty]
		public string LoginDestinationUrl { get; set; }

		/// <summary>
		/// Gets or sets the URL of the image used for the login link.
		/// </summary>
		[DefaultValue(""), UrlProperty, Editor("System.Web.UI.Design.ImageUrlEditor, System.Design, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a", typeof(UITypeEditor))]
		public string LoginImageUrl { get; set; }
		
		/// <summary>
		/// Gets or sets the text used for the login link.
		/// </summary>
		[DefaultValue(typeof(string), "Sign In")]
		public string LoginText
		{
			get { return string.IsNullOrEmpty(_loginText) ? "Sign In" : _loginText; }
			set { _loginText = value; }
		}
		
		/// <summary>
		/// Gets or sets the URL of the image used for the logout button.
		/// </summary>
		[DefaultValue(""), UrlProperty, Editor("System.Web.UI.Design.ImageUrlEditor, System.Design, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a", typeof(UITypeEditor))]
		public string LogoutImageUrl { get; set; }
		
		/// <summary>
		/// Gets or sets the text used for the logout link.
		/// </summary>
		[DefaultValue(typeof(string), "Sign Out")]
		public string LogoutText
		{
			get { return string.IsNullOrEmpty(_logoutText) ? "Sign Out" : _logoutText; }
			set { _logoutText = value; }
		}

		/// <summary>
		/// When a LoginNavigateUrl is not specified, the link takes the user directly to Windows Live.
		/// 
		/// If a landing page is desired, set the LoginNavigateUrl to be your landing page, and then place this control on your landing page with no LoginNavigateUrl.
		/// </summary>
		[UrlProperty]
		public string LoginNavigateUrl { get; set; }

		/// <summary>
		/// When returning from sign-in, if the user is authenticated but has never registered, they will be sent to the specified URL.
		/// The URL must be one that exists that the server can POST to. It cannot be a custom URL or one that will cause a 404 redirect.
		/// </summary>
		/// <remarks>If no registration URL is given, the user can be automatically registered if one sets the AutoRegister connection string setting to true.</remarks>
		[UrlProperty]
		public string RegistrationUrl { get; set; }

		protected override HtmlTextWriterTag TagKey
		{
			get { return HtmlTextWriterTag.A; }
		}

		protected override void CreateChildControls()
		{
			Controls.Clear();
			
			_hyperLink = new System.Web.UI.WebControls.HyperLink { EnableViewState = false, EnableTheming = false };

			Controls.Add(_hyperLink);
		}

		protected override void Render(HtmlTextWriter writer)
		{
			RenderContents(writer);
		}

		protected override void RenderContents(HtmlTextWriter writer)
		{
			AssertConfiguration();

			SetChildProperties();

			if (!string.IsNullOrEmpty(ID))
			{
				writer.AddAttribute(HtmlTextWriterAttribute.Id, ClientID);
			}
			
			base.RenderContents(writer);
		}

		private void AssertConfiguration()
		{
			var missingHandlerException = new ConfigurationErrorsException("The LiveIdLoginStatus control requires a '{0}' or '{1}' to be configured.".FormatWith(typeof(LiveIdMembershipProvider), typeof(LiveIdAccountTransferHandler)));

			if (LiveIdMembershipProvider.Current == null) throw missingHandlerException;

			if (!LiveIdWebAuthenticationHandlerExists()) throw missingHandlerException;

			var windowsLive = new WindowsLiveLogin(true);

			if (string.IsNullOrEmpty(windowsLive.AppId)) throw new ConfigurationErrorsException("The LiveIdLoginStatus control requires the application ID that you obtained when you registered your site to be specified in the LiveIdMembershipProvider connection string.");
		}

		private bool LiveIdWebAuthenticationHandlerExists()
		{
			if (Context.Request.ServerVariables.Get("MANAGED_PIPELINE_MODE") == "Integrated") return true; 

			var handlersSection = (HttpHandlersSection)WebConfigurationManager.GetSection("system.web/httpHandlers");

			foreach (HttpHandlerAction handler in handlersSection.Handlers)
			{
				if (Type.GetType(handler.Type) == typeof(LiveIdWebAuthenticationHandler)
					|| Type.GetType(handler.Type) == typeof(LiveIdAccountTransferHandler))
				{
					return true;
				}
			}
			
			return false;
		}

		private void SetChildProperties()
		{
			EnsureChildControls();

			if (Context.Request.IsAuthenticated)
			{
				_hyperLink.ImageUrl = LogoutImageUrl;
				_hyperLink.Text = LogoutText;

				var live = LiveIdMembershipProvider.Current;

				_hyperLink.NavigateUrl = "http://login.live.com/logout.srf?appid={0}".FormatWith(Encoder.UrlEncode(live.AppId));
			}
			else
			{
				_hyperLink.ImageUrl = LoginImageUrl;
				_hyperLink.Text = LoginText;

				var loginDestinationUrl = LoginDestinationUrl
					?? Context.Request.QueryString["ReturnUrl"]
						?? Context.Request.QueryString["URL"]
							?? Context.Request.RawUrl;

				if (!string.IsNullOrEmpty(LoginNavigateUrl))
				{
					var url = new UrlBuilder(LoginNavigateUrl);
					
					url.QueryString.Set("ReturnUrl", loginDestinationUrl);
					
					_hyperLink.NavigateUrl = url.PathWithQueryString;
				}
				else
				{
					// the user did not supply a LoginNavigateSiteMarker, set the link to go to Windows Live.
					_hyperLink.NavigateUrl = "http://login.live.com/wlogin.srf?appid={0}&alg=wsignin1.0&appctx=loginpath{1}$registrationpath{2}".FormatWith(
						Encoder.UrlEncode(LiveIdMembershipProvider.Current.AppId),
						Encoder.UrlEncode(loginDestinationUrl),
						Encoder.UrlEncode(RegistrationUrl));
				}
			}

			_hyperLink.CopyBaseAttributes(this);
			_hyperLink.ApplyStyle(ControlStyle);
		}
	}
}


