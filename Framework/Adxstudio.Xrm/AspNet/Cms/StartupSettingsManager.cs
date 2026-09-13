/*
  Copyright (c) Microsoft Corporation. All rights reserved.
  Licensed under the MIT License. See License.txt in the project root for license information.
*/

namespace Adxstudio.Xrm.AspNet.Cms
{
	using System;
	using System.Collections.Generic;
	using Microsoft.IdentityModel.Tokens;
	using System.Linq;
	using System.Net.Http;
	using System.Security.Claims;
	using System.Security.Cryptography.X509Certificates;
	using System.Text.RegularExpressions;
	using System.Threading;
	using System.Threading.Tasks;
	using Adxstudio.Xrm.AspNet.Identity;
	using Adxstudio.Xrm.Owin.Security.Saml2;
	using ITfoxtec.Identity.Saml2.Util;
	using Microsoft.AspNet.Identity;
	using Microsoft.AspNet.Identity.Owin;
	using Microsoft.IdentityModel.Protocols;
	using Microsoft.IdentityModel.Protocols.OpenIdConnect;
	using Microsoft.IdentityModel.Protocols.WsFederation;
	using Microsoft.Owin;
	using Microsoft.Owin.Security;
	using Microsoft.Owin.Security.Cookies;
	using Microsoft.Owin.Security.Facebook;
	using Microsoft.Owin.Security.Google;
	using Microsoft.Owin.Security.MicrosoftAccount;
	using Microsoft.Owin.Security.Notifications;
	using Microsoft.Owin.Security.OpenIdConnect;
	using Microsoft.Owin.Security.Twitter;
	using Microsoft.Owin.Security.WsFederation;
	using Microsoft.Xrm.Client;
	using Microsoft.Xrm.Portal.Web;
	using global::Owin.Security.Providers.LinkedIn;
	using global::Owin.Security.Providers.Yahoo;

	public interface IAuthenticationOptionsExtended
	{
		string AuthenticationType { get; set; }
		bool ExternalLogoutEnabled { get; set; }
		bool RegistrationEnabled { get; set; }
		string RegistrationClaimsMapping { get; set; }
		string LoginClaimsMapping { get; set; }
		string ProfileEditPolicyId { get; set; }
		Task<string> ToIssuer(CancellationToken cancellationToken);
	}

	/// <summary>
	/// Manages middleware settings that are applied at application startup.
	/// </summary>
	/// <typeparam name="TUser">The user type.</typeparam>
	public abstract class StartupSettingsManager<TUser>
		where TUser : CrmUser
	{
		#region options classes

		private class CommonAuthenticationOptions : AuthenticationOptions
		{
			public string Id { get; set; }
			public string Secret { get; set; }
			public string Caption { get; set; }
			public TimeSpan? BackchannelTimeout { get; set; }
			public PathString? CallbackPath { get; set; }
			public string SignInAsAuthenticationType { get; set; }
			public new AuthenticationMode? AuthenticationMode { get; set; }
			public IList<string> Scope { get; set; }
			public IList<string> CertificateSubjectKeyIdentifierValidator { get; set; }
			public IList<string> CertificateThumbprintValidator { get; set; }
			public bool ExternalLogoutEnabled { get; set; }
			public bool RegistrationEnabled { get; set; }
			public string RegistrationClaimsMapping { get; set; }
			public string LoginClaimsMapping { get; set; }
			public bool AllowContactMappingWithEmail { get; set; }

			public CommonAuthenticationOptions(string authenticationType)
				: base(authenticationType)
			{
			}
		}

		public class OpenIdConnectAuthenticationOptionsExtended : OpenIdConnectAuthenticationOptions, IAuthenticationOptionsExtended
		{
			private string issuer;
			public bool ExternalLogoutEnabled { get; set; }
			public bool RegistrationEnabled { get; set; }
			public string RegistrationClaimsMapping { get; set; }
			public string LoginClaimsMapping { get; set; }
			public bool AllowContactMappingWithEmail { get; set; }
			public string PasswordResetPolicyId { get; set; }
			public string ProfileEditPolicyId { get; set; }
			public string DefaultPolicyId { get; set; }

			public OpenIdConnectAuthenticationOptionsExtended()
			{
			}

			public OpenIdConnectAuthenticationOptionsExtended(string authenticationType)
				: base(authenticationType)
			{
			}

			public async Task<string> ToIssuer(CancellationToken cancellationToken)
			{
				if (this.issuer == null)
				{
					var configuration = await this.ConfigurationManager.GetConfigurationAsync(cancellationToken);
					this.issuer = configuration.Issuer;
				}

				return this.issuer;
			}
		}

		public class Saml2AuthenticationOptionsExtended : Saml2AuthenticationOptions, IAuthenticationOptionsExtended
		{
			private string issuer;
			public bool ExternalLogoutEnabled { get; set; }
			public bool RegistrationEnabled { get; set; }
			public string RegistrationClaimsMapping { get; set; }
			public string LoginClaimsMapping { get; set; }
			public bool AllowContactMappingWithEmail { get; set; }
			public string ProfileEditPolicyId { get; set; }

			public Saml2AuthenticationOptionsExtended()
			{
			}

			public Saml2AuthenticationOptionsExtended(bool externalLogoutEnabled)
			{
				ExternalLogoutEnabled = externalLogoutEnabled;
			}

			public async Task<string> ToIssuer(CancellationToken cancellationToken)
			{
				if (this.issuer == null)
				{
					var configuration = await this.ConfigurationManager.GetConfigurationAsync(cancellationToken);
					this.issuer = configuration.Issuer;
				}

				return this.issuer;
			}
		}

		public class WsFederationAuthenticationOptionsExtended : WsFederationAuthenticationOptions, IAuthenticationOptionsExtended
		{
			private string issuer;
			public bool ExternalLogoutEnabled { get; set; }
			public bool RegistrationEnabled { get; set; }
			public string RegistrationClaimsMapping { get; set; }
			public string LoginClaimsMapping { get; set; }
			public bool AllowContactMappingWithEmail { get; set; }
			public string ProfileEditPolicyId { get; set; }

			public WsFederationAuthenticationOptionsExtended()
			{
			}

			public WsFederationAuthenticationOptionsExtended(bool externalLogoutEnabled)
			{
				ExternalLogoutEnabled = externalLogoutEnabled;
			}

			public WsFederationAuthenticationOptionsExtended(string authenticationType)
				: base(authenticationType)
			{
			}

			public WsFederationAuthenticationOptionsExtended(bool externalLogoutEnabled, string authenticationType)
				: base(authenticationType)
			{
				ExternalLogoutEnabled = externalLogoutEnabled;
			}

			public async Task<string> ToIssuer(CancellationToken cancellationToken)
			{
				if (this.issuer == null)
				{
					var configuration = await this.ConfigurationManager.GetConfigurationAsync(cancellationToken);
					this.issuer = configuration.Issuer;
				}

				return this.issuer;
			}
		}

		public class MicrosoftAccountAuthenticationOptionsExtended : MicrosoftAccountAuthenticationOptions, IAuthenticationOptionsExtended
		{
			public bool ExternalLogoutEnabled { get; set; }
			public bool RegistrationEnabled { get; set; }
			public string RegistrationClaimsMapping { get; set; }
			public string LoginClaimsMapping { get; set; }
			public bool AllowContactMappingWithEmail { get; set; }
			public string ProfileEditPolicyId { get; set; }

			public MicrosoftAccountAuthenticationOptionsExtended()
			{
			}

			public MicrosoftAccountAuthenticationOptionsExtended(bool externalLogoutEnabled)
			{
				ExternalLogoutEnabled = externalLogoutEnabled;
			}

			public Task<string> ToIssuer(CancellationToken cancellationToken)
			{
				return Task.FromResult(this.AuthenticationType);
			}
		}

		public class TwitterAuthenticationOptionsExtended : TwitterAuthenticationOptions, IAuthenticationOptionsExtended
		{
			public bool ExternalLogoutEnabled { get; set; }
			public bool RegistrationEnabled { get; set; }
			public string RegistrationClaimsMapping { get; set; }
			public string LoginClaimsMapping { get; set; }
			public bool AllowContactMappingWithEmail { get; set; }
			public string ProfileEditPolicyId { get; set; }

			public TwitterAuthenticationOptionsExtended()
			{
			}

			public TwitterAuthenticationOptionsExtended(bool externalLogoutEnabled)
			{
				ExternalLogoutEnabled = externalLogoutEnabled;
			}

			public Task<string> ToIssuer(CancellationToken cancellationToken)
			{
				return Task.FromResult(this.AuthenticationType);
			}
		}

		public class FacebookAuthenticationOptionsExtended : FacebookAuthenticationOptions, IAuthenticationOptionsExtended
		{
			public bool ExternalLogoutEnabled { get; set; }
			public bool RegistrationEnabled { get; set; }
			public string RegistrationClaimsMapping { get; set; }
			public string LoginClaimsMapping { get; set; }
			public bool AllowContactMappingWithEmail { get; set; }
			public string ProfileEditPolicyId { get; set; }

			public FacebookAuthenticationOptionsExtended()
			{
			}

			public FacebookAuthenticationOptionsExtended(bool externalLogoutEnabled)
			{
				ExternalLogoutEnabled = externalLogoutEnabled;
			}

			public Task<string> ToIssuer(CancellationToken cancellationToken)
			{
				return Task.FromResult(this.AuthenticationType);
			}
		}

		public class GoogleOAuth2AuthenticationOptionsExtended : GoogleOAuth2AuthenticationOptions, IAuthenticationOptionsExtended
		{
			public bool ExternalLogoutEnabled { get; set; }
			public bool RegistrationEnabled { get; set; }
			public string RegistrationClaimsMapping { get; set; }
			public string LoginClaimsMapping { get; set; }
			public bool AllowContactMappingWithEmail { get; set; }
			public string ProfileEditPolicyId { get; set; }

			public GoogleOAuth2AuthenticationOptionsExtended()
			{
			}

			public GoogleOAuth2AuthenticationOptionsExtended(bool externalLogoutEnabled)
			{
				ExternalLogoutEnabled = externalLogoutEnabled;
			}

			public Task<string> ToIssuer(CancellationToken cancellationToken)
			{
				return Task.FromResult(this.AuthenticationType);
			}
		}

		public class LinkedInAuthenticationOptionsExtended : LinkedInAuthenticationOptions, IAuthenticationOptionsExtended
		{
			public bool ExternalLogoutEnabled { get; set; }
			public bool RegistrationEnabled { get; set; }
			public string RegistrationClaimsMapping { get; set; }
			public string LoginClaimsMapping { get; set; }
			public bool AllowContactMappingWithEmail { get; set; }
			public string ProfileEditPolicyId { get; set; }

			public LinkedInAuthenticationOptionsExtended()
			{
			}

			public LinkedInAuthenticationOptionsExtended(bool externalLogoutEnabled)
			{
				ExternalLogoutEnabled = externalLogoutEnabled;
			}

			public Task<string> ToIssuer(CancellationToken cancellationToken)
			{
				return Task.FromResult(this.AuthenticationType);
			}
		}

		public class YahooAuthenticationOptionsExtended : YahooAuthenticationOptions, IAuthenticationOptionsExtended
		{
			public bool ExternalLogoutEnabled { get; set; }
			public bool RegistrationEnabled { get; set; }
			public string RegistrationClaimsMapping { get; set; }
			public string LoginClaimsMapping { get; set; }
			public bool AllowContactMappingWithEmail { get; set; }
			public string ProfileEditPolicyId { get; set; }

			public YahooAuthenticationOptionsExtended()
			{
			}

			public YahooAuthenticationOptionsExtended(bool externalLogoutEnabled)
			{
				ExternalLogoutEnabled = externalLogoutEnabled;
			}

			public Task<string> ToIssuer(CancellationToken cancellationToken)
			{
				return Task.FromResult(this.AuthenticationType);
			}
		}

		#endregion

		private readonly Func<UserManager<TUser, string>, TUser, Task<ClaimsIdentity>> _regenerateIdentityCallback;
		private readonly PathString _loginPath;
		private readonly PathString _externalLoginCallbackPath;
		private readonly PathString _externalAuthenticationFailedPath;
		private readonly PathString _externalPasswordResetPath;
		private const bool DefaultRegistrationEnabled = true;
		private const bool DefaultAzureADObjectIdentifierAsNameIdentifierClaimEnabled = true;
		private const bool DefaultObjectIdentifierAsNameIdentifierClaimEnabled = false;
		private const bool DefaultExternalLogoutEnabled = false;
		private const bool DefaultAllowContactMappingWithEmail = false;
		private const string OwinAuthenticationFailedAccessDeniedMsg = "access_denied";
		private const string MessageQueryStringParameter = "message";
		private const string PasswordResetPolicyQueryStringParameter = "passwordResetPolicyId";
		private const string AzureADB2CPasswordResetPolicyErrorCode = "AADB2C90118";
		private const string AzureADB2CUserCancelledErrorCode = "AADB2C90091";

		public CookieAuthenticationOptions ApplicationCookie { get; private set; }
		public string DefaultAuthenticationType { get; private set; }
		public bool SingleSignOn { get; private set; }
		public CookieAuthenticationOptions TwoFactorCookie { get; private set; }
		public bool ExternalRegistrationEnabled { get; private set; }

		public MicrosoftAccountAuthenticationOptionsExtended MicrosoftAccount { get; private set; }
		public TwitterAuthenticationOptionsExtended Twitter { get; private set; }
		public FacebookAuthenticationOptionsExtended Facebook { get; private set; }
		public GoogleOAuth2AuthenticationOptionsExtended Google { get; private set; }

		public LinkedInAuthenticationOptionsExtended LinkedIn { get; private set; }
		public YahooAuthenticationOptionsExtended Yahoo { get; private set; }

		public IEnumerable<WsFederationAuthenticationOptionsExtended> WsFederationOptions { get; private set; }
		public IEnumerable<Saml2AuthenticationOptionsExtended> Saml2Options { get; private set; }
		public IEnumerable<OpenIdConnectAuthenticationOptionsExtended> OpenIdConnectOptions { get; private set; }
		public OpenIdConnectAuthenticationOptionsExtended AzureAdOptions { get; private set; }

		protected StartupSettingsManager(
			CrmWebsite website,
			Func<UserManager<TUser, string>, TUser, Task<ClaimsIdentity>> regenerateIdentityCallback,
			PathString loginPath,
			PathString externalLoginCallbackPath,
			PathString externalAuthenticationFailedPath,
			PathString externalPasswordResetPath)
		{
			_regenerateIdentityCallback = regenerateIdentityCallback;
			_loginPath = loginPath;
			_externalLoginCallbackPath = externalLoginCallbackPath;
			_externalAuthenticationFailedPath = externalAuthenticationFailedPath;
			_externalPasswordResetPath = externalPasswordResetPath;
			Initialize(website);
		}

		protected abstract UserManager<TUser, string> GetUserManager(IOwinContext context);

		protected virtual void Initialize(CrmWebsite website)
		{
			ApplicationCookie = ToCookieOptions(website);
			TwoFactorCookie = ToCookieTwoFactorOptions(website);
			
			DefaultAuthenticationType = website.Settings.Get<string>("Authentication/Registration/LoginButtonAuthenticationType");

			SingleSignOn = !string.IsNullOrWhiteSpace(DefaultAuthenticationType);

			var AzureADLoginEnabled = website.Settings
				.Get<bool?>("Authentication/Registration/AzureADLoginEnabled").GetValueOrDefault(true);
			if (AzureADLoginEnabled || Configuration.PortalSettings.Instance.Ess.IsEss)
			{
				AzureAdOptions = ToAzureAdOptions(website);
			}

			if (!Configuration.PortalSettings.Instance.Ess.IsEss && ToExternalLoginEnabled(website))
			{
				MicrosoftAccount = ToMicrosoft(website);
				Twitter = ToTwitter(website);
				Facebook = ToFacebook(website);
				Google = ToGoogle(website);

				LinkedIn = ToLinkedIn(website);
				Yahoo = ToYahoo(website);

				WsFederationOptions = ToGroupedOptions(website.Settings, @"Authentication\/WsFederation\/(?<provider>.+?)\/(?<name>.+)", ToWsFederationOptions).ToArray();
				Saml2Options = ToGroupedOptions(website.Settings, @"Authentication\/SAML2\/(?<provider>.+?)\/(?<name>.+)", ToSaml2Options).ToArray();
				OpenIdConnectOptions = ToGroupedOptions(website.Settings, @"Authentication\/OpenIdConnect\/(?<provider>.+?)\/(?<name>.+)", ToOpenIdConnectOptions).ToArray();
			}

			ExternalRegistrationEnabled = GetAllAuthenticationOptions().Select(option => option.RegistrationEnabled).Any();
		}

		protected virtual bool ToExternalLoginEnabled(CrmWebsite website)
		{
			return website.Settings.Get<bool?>("Authentication/Registration/ExternalLoginEnabled").GetValueOrDefault(true);
		}

		protected virtual OpenIdConnectAuthenticationOptionsExtended ToAzureAdOptions(CrmWebsite website)
		{
			var clientId = Configuration.PortalSettings.Instance.Authentication.ClientId;
			var rootUrl = Configuration.PortalSettings.Instance.Authentication.RootUrl;
			var tenantId = Configuration.PortalSettings.Instance.Authentication.TenantId;
			var redirectUri = Configuration.PortalSettings.Instance.Authentication.RedirectUri;
			var caption = website.Settings.Get<string>("Authentication/OpenIdConnect/AzureAD/Caption")
				?? Configuration.PortalSettings.Instance.Authentication.Caption
				?? "Azure AD";
			var registrationClaimsMapping = website.Settings.Get<string>("Authentication/OpenIdConnect/AzureAD/RegistrationClaimsMapping");
			var loginClaimsMapping = website.Settings.Get<string>("Authentication/OpenIdConnect/AzureAD/LoginClaimsMapping");
			var oIDAsNameIDClaimEnabled = website.Settings.Get<bool?>("Authentication/OpenIdConnect/AzureAD/ObjectIdentifierAsNameIdentifierClaimEnabled").GetValueOrDefault(DefaultAzureADObjectIdentifierAsNameIdentifierClaimEnabled);
			var logoutEnabled = website.Settings.Get<bool?>("Authentication/OpenIdConnect/AzureAD/ExternalLogoutEnabled").GetValueOrDefault(DefaultExternalLogoutEnabled);
			var registrationEnabled = website.Settings.Get<bool?>("Authentication/OpenIdConnect/AzureAD/RegistrationEnabled").GetValueOrDefault(DefaultRegistrationEnabled);
			var allowContactMappingWithEmail = website.Settings.Get<bool?>("Authentication/OpenIdConnect/AzureAD/AllowContactMappingWithEmail").GetValueOrDefault(DefaultAllowContactMappingWithEmail);

			if (!string.IsNullOrWhiteSpace(clientId) && !string.IsNullOrWhiteSpace(rootUrl)
				&& !string.IsNullOrWhiteSpace(tenantId) && !string.IsNullOrWhiteSpace(redirectUri))
			{
				var authority = new Uri(new Uri(rootUrl), tenantId + "/");
				var postLogoutRedirectUri = new Uri(redirectUri).GetLeftPart(UriPartial.Authority);

				return new OpenIdConnectAuthenticationOptionsExtended
				{
					CookieManager = new AuthenticationCookieManager(),
					AuthenticationType = authority.AbsoluteUri,
					AuthenticationMode = AuthenticationMode.Passive,
					Authority = authority.AbsoluteUri,
					ClientId = clientId,
					RedirectUri = redirectUri,
					Caption = caption,
					PostLogoutRedirectUri = postLogoutRedirectUri,
					Notifications = new OpenIdConnectAuthenticationNotifications
					{
						RedirectToIdentityProvider = OnRedirectToIdentityProvider,
						SecurityTokenValidated = notification => OnOpenIdConnectSecurityTokenValidated(notification, oIDAsNameIDClaimEnabled),
						AuthenticationFailed = OnAuthenticationFailed,
					},
					ExternalLogoutEnabled = logoutEnabled,
					RegistrationEnabled = registrationEnabled,
					RegistrationClaimsMapping = registrationClaimsMapping,
					LoginClaimsMapping = loginClaimsMapping,
					AllowContactMappingWithEmail = allowContactMappingWithEmail
				};
			}

			return null;
		}

		#region Cookie

		protected virtual CookieAuthenticationOptions ToCookieOptions(CrmWebsite website)
		{
			var authenticationType = website.Settings.Get<string>("Authentication/ApplicationCookie/AuthenticationType");
			var cookieName = website.Settings.Get<string>("Authentication/ApplicationCookie/CookieName");
			var cookieDomain = website.Settings.Get<string>("Authentication/ApplicationCookie/CookieDomain");
			var cookiePath = website.Settings.Get<string>("Authentication/ApplicationCookie/CookiePath");
			var cookieHttpOnly = website.Settings.Get<bool?>("Authentication/ApplicationCookie/CookieHttpOnly");
			var cookieSecure = website.Settings.GetEnum<CookieSecureOption>("Authentication/ApplicationCookie/CookieSecure");
			var expireTimeSpan = website.Settings.Get<TimeSpan?>("Authentication/ApplicationCookie/ExpireTimeSpan");
			var slidingExpiration = website.Settings.Get<bool?>("Authentication/ApplicationCookie/SlidingExpiration");
			var loginPath = website.Settings.Get<PathString?>("Authentication/ApplicationCookie/LoginPath");
			var logoutPath = website.Settings.Get<PathString?>("Authentication/ApplicationCookie/LogoutPath");
			var returnUrlParameter = website.Settings.Get<string>("Authentication/ApplicationCookie/ReturnUrlParameter");

			var options = new CookieAuthenticationOptions
			{
				CookieManager = new AuthenticationCookieManager(),
				AuthenticationType = authenticationType ?? DefaultAuthenticationTypes.ApplicationCookie,
				LoginPath = loginPath ?? _loginPath,
				Provider = new CookieAuthenticationProvider
				{
					// Enables the application to validate the security stamp when the user logs in.
					// This is a security feature which is used when you change a password or add an external login to your account.  
					OnValidateIdentity = GetOnValidateIdentity(website),
				}
			};

			if (!string.IsNullOrWhiteSpace(cookieName)) options.CookieName = cookieName;
			if (!string.IsNullOrWhiteSpace(cookieDomain)) options.CookieDomain = cookieDomain;
			if (!string.IsNullOrWhiteSpace(cookiePath)) options.CookiePath = cookiePath;
			if (cookieHttpOnly != null) options.CookieHttpOnly = cookieHttpOnly.GetValueOrDefault();
			if (cookieSecure != null) options.CookieSecure = cookieSecure.GetValueOrDefault();
			if (slidingExpiration != null) options.SlidingExpiration = slidingExpiration.GetValueOrDefault();
			if (logoutPath != null) options.LogoutPath = logoutPath.GetValueOrDefault();
			if (!string.IsNullOrWhiteSpace(returnUrlParameter)) options.ReturnUrlParameter = returnUrlParameter;

			options.ExpireTimeSpan = expireTimeSpan.GetValueOrDefault(TimeSpan.FromDays(1));

			return options;
		}

		protected virtual Func<CookieValidateIdentityContext, Task> GetOnValidateIdentity(CrmWebsite website)
		{
			var validateInterval = website.Settings.Get<TimeSpan?>("Authentication/ApplicationCookie/SecurityStampValidator/ValidateInterval");
			var refreshInterval = website.Settings.Get<TimeSpan?>("Authentication/ApplicationCookie/SecurityStampValidator/RefreshInterval");

			return OnValidateIdentity(
				validateInterval.GetValueOrDefault(TimeSpan.Zero),
				refreshInterval.GetValueOrDefault(TimeSpan.FromMinutes(30)),
				(manager, user) => _regenerateIdentityCallback(manager, user),
				id => id.GetUserId<string>());
		}

		private Func<CookieValidateIdentityContext, Task> OnValidateIdentity(
			TimeSpan validateInterval,
			TimeSpan refreshInterval,
			Func<UserManager<TUser, string>, TUser, Task<ClaimsIdentity>> regenerateIdentityCallback,
			Func<ClaimsIdentity, string> getUserIdCallback)
		{
			if (getUserIdCallback == null)
			{
				throw new ArgumentNullException("getUserIdCallback");
			}

			return async context =>
			{
				var currentUtc = DateTimeOffset.UtcNow;

				if (context.Options != null && context.Options.SystemClock != null)
				{
					currentUtc = context.Options.SystemClock.UtcNow;
				}

				var issuedUtc = context.Properties.IssuedUtc;

				// Only validate if enough time has elapsed
				var validate = (issuedUtc == null);
				var refresh = false;

				if (issuedUtc != null)
				{
					var timeElapsed = currentUtc.Subtract(issuedUtc.Value);
					validate = timeElapsed > new[] { validateInterval, refreshInterval }.Min();
					refresh = timeElapsed > refreshInterval;
				}

				if (validate)
				{
					var manager = this.GetUserManager(context.OwinContext);
					var userId = getUserIdCallback(context.Identity);

					if (manager != null && userId != null)
					{
						var user = await manager.FindByIdAsync(userId).WithCurrentCulture();
						var reject = true;

						// Refresh the identity if the stamp matches, otherwise reject
						if (user != null && manager.SupportsUserSecurityStamp)
						{
							var securityStamp = context.Identity.FindFirstValue(Constants.DefaultSecurityStampClaimType);

							if (securityStamp == await manager.GetSecurityStampAsync(userId).WithCurrentCulture())
							{
								reject = false;

								if (refresh)
								{
									// Regenerate fresh claims if possible and resign in
									if (regenerateIdentityCallback != null)
									{
										var identity = await regenerateIdentityCallback.Invoke(manager, user).WithCurrentCulture();

										if (identity != null)
										{
											// Fix for regression where this value is not updated
											// Setting it to null so that it is refreshed by the cookie middleware
											context.Properties.IssuedUtc = null;
											context.Properties.ExpiresUtc = null;
											context.OwinContext.Authentication.SignIn(context.Properties, identity);
										}
									}
								}
							}
						}

						if (reject)
						{
							context.RejectIdentity();
							context.OwinContext.Authentication.SignOut(context.Options.AuthenticationType);
						}
					}
				}
			};
		}

		protected virtual CookieAuthenticationOptions ToCookieTwoFactorOptions(CrmWebsite website)
		{
			var authenticationType = website.Settings.Get<string>("Authentication/TwoFactorCookie/AuthenticationType");
			var expire = website.Settings.Get<TimeSpan?>("Authentication/TwoFactorCookie/ExpireTimeSpan");

			return new CookieAuthenticationOptions
			{
				CookieManager = new AuthenticationCookieManager(),
				AuthenticationType = authenticationType ?? DefaultAuthenticationTypes.TwoFactorCookie,
				ExpireTimeSpan = expire.GetValueOrDefault(TimeSpan.FromMinutes(5)),
			};
		}

		#endregion

		#region WsFederation

		protected virtual WsFederationAuthenticationOptionsExtended ToWsFederationOptions(string providerName, CrmWebsiteSettingCollection settings)
		{
			return ToWsFederationOptions(settings, null, providerName, null, null, null, OnRedirectToIdentityProvider, null);
		}

		protected virtual WsFederationAuthenticationOptionsExtended ToWsFederationOptions(
			CrmWebsiteSettingCollection settings,
			string defaultMetdataAddress,
			string defaultCaption,
			string defaultWtrealm,
			string defaultWreply,
			string[] defaultValidAudiences,
			Func<RedirectToIdentityProviderNotification<WsFederationMessage, WsFederationAuthenticationOptions>, string, Task> onRedirectToIdentityProvider,
			Func<SecurityTokenValidatedNotification<WsFederationMessage, WsFederationAuthenticationOptions>, Task> onSecurityTokenValidated)
		{
			var authenticationType = settings.Get<string>("AuthenticationType");
			var metadataAddress = settings.Get<string>("MetadataAddress") ?? defaultMetdataAddress;
			var wtrealmText = settings.Get<string>("Wtrealm") ?? defaultWtrealm;
			var wreplyText = settings.Get<string>("Wreply") ?? defaultWreply;
			var wtrealm = wtrealmText ?? wreplyText;
			var wreply = wreplyText ?? wtrealmText;

			if (string.IsNullOrWhiteSpace(metadataAddress) || string.IsNullOrWhiteSpace(wtrealm)) return null;

			var whr = settings.Get<string>("Whr");
			var authenticationMode = settings.GetEnum<AuthenticationMode>("AuthenticationMode");
			var signInAsAuthenticationType = settings.Get<string>("SignInAsAuthenticationType");
			var caption = settings.Get<string>("Caption") ?? defaultCaption;

			var signOutWreply = settings.Get<string>("SignOutWreply")
				?? (!string.IsNullOrWhiteSpace(wreply) ? new Uri(wreply).GetLeftPart(UriPartial.Authority) : null);
			var callbackPath = settings.Get<PathString?>("CallbackPath");

			var backchannelTimeout = settings.Get<TimeSpan?>("BackchannelTimeout");
			var refreshOnIssuerKeyNotFound = settings.Get<bool?>("RefreshOnIssuerKeyNotFound");
			var useTokenLifetime = settings.Get<bool?>("UseTokenLifetime");
			var registrationClaimsMapping = settings.Get<string>("RegistrationClaimsMapping");
			var loginClaimsMapping = settings.Get<string>("LoginClaimsMapping");
			var logoutEnabled = settings.Get<bool?>("ExternalLogoutEnabled").GetValueOrDefault(DefaultExternalLogoutEnabled);
			var registrationEnabled = settings.Get<bool?>("RegistrationEnabled").GetValueOrDefault(DefaultRegistrationEnabled);
			var allowContactMappingWithEmail = settings.Get<bool?>("AllowContactMappingWithEmail").GetValueOrDefault(DefaultAllowContactMappingWithEmail);

			// required settings

			var options = new WsFederationAuthenticationOptionsExtended
			{
				MetadataAddress = metadataAddress,
				Wtrealm = wtrealm,
				AuthenticationMode = authenticationMode ?? AuthenticationMode.Passive,
				ExternalLogoutEnabled = logoutEnabled,
				RegistrationEnabled = registrationEnabled,
				RegistrationClaimsMapping = registrationClaimsMapping,
				LoginClaimsMapping = loginClaimsMapping,
				AllowContactMappingWithEmail = allowContactMappingWithEmail
			};

			// optional settings

			if (!string.IsNullOrWhiteSpace(authenticationType)) options.AuthenticationType = authenticationType;
			if (!string.IsNullOrWhiteSpace(signInAsAuthenticationType)) options.SignInAsAuthenticationType = signInAsAuthenticationType;
			if (!string.IsNullOrWhiteSpace(caption)) options.Caption = caption;

			if (!string.IsNullOrWhiteSpace(wreply)) options.Wreply = wreply;
			if (!string.IsNullOrWhiteSpace(signOutWreply)) options.SignOutWreply = signOutWreply;
			if (callbackPath != null) options.CallbackPath = callbackPath.GetValueOrDefault();

			if (backchannelTimeout != null) options.BackchannelTimeout = backchannelTimeout.GetValueOrDefault();
			if (refreshOnIssuerKeyNotFound != null) options.RefreshOnIssuerKeyNotFound = refreshOnIssuerKeyNotFound.GetValueOrDefault();
			if (useTokenLifetime != null) options.UseTokenLifetime = useTokenLifetime.GetValueOrDefault();

			options.Notifications = new WsFederationAuthenticationNotifications();

			if (onRedirectToIdentityProvider != null) options.Notifications.RedirectToIdentityProvider = notification => onRedirectToIdentityProvider(notification, whr);
			if (onSecurityTokenValidated != null) options.Notifications.SecurityTokenValidated = onSecurityTokenValidated;

			SetTokenValidationParameters(settings, options.TokenValidationParameters, defaultValidAudiences);

			return options;
		}

		protected virtual Task OnRedirectToIdentityProvider(RedirectToIdentityProviderNotification<WsFederationMessage, WsFederationAuthenticationOptions> notification, string defaultWhr)
		{
			var properties = GetAuthenticationProperties(notification.OwinContext);

			if (properties != null)
			{
				string whr, wtrealm, wreply;

				if (properties.Dictionary.TryGetValue("whr", out whr))
				{
					notification.ProtocolMessage.Whr = whr;
				}
				else if (!string.IsNullOrWhiteSpace(defaultWhr))
				{
					notification.ProtocolMessage.Whr = defaultWhr;
				}

				if (properties.Dictionary.TryGetValue("wtrealm", out wtrealm))
				{
					notification.ProtocolMessage.Wtrealm = wtrealm;
				}

				if (properties.Dictionary.TryGetValue("wreply", out wreply))
				{
					notification.ProtocolMessage.Wreply = wreply;
				}
			}

			return Task.FromResult(0);
		}

		private static AuthenticationProperties GetAuthenticationProperties(IOwinContext owinContext)
		{
			if (owinContext.Authentication == null) return null;
			if (owinContext.Authentication.AuthenticationResponseChallenge == null) return null;

			return owinContext.Authentication.AuthenticationResponseChallenge.Properties;
		}

		#endregion

		#region Saml2

		protected virtual Saml2AuthenticationOptionsExtended ToSaml2Options(string providerName, CrmWebsiteSettingCollection settings)
		{
			var authenticationType = settings.Get<string>("AuthenticationType");
			var metadataAddress = settings.Get<string>("MetadataAddress");
			var wtrealmText = settings.Get<string>("ServiceProviderRealm")
				?? settings.Get<string>("Wtrealm");
			var wreplyText = settings.Get<string>("AssertionConsumerServiceUrl")
				?? settings.Get<string>("Wreply");
			var wtrealm = wtrealmText ?? wreplyText;
			var wreply = wreplyText ?? wtrealmText;

			if (string.IsNullOrWhiteSpace(metadataAddress) || string.IsNullOrWhiteSpace(wtrealm)) return null;

			var authenticationMode = settings.GetEnum<AuthenticationMode>("AuthenticationMode");
			var signInAsAuthenticationType = settings.Get<string>("SignInAsAuthenticationType");
			var caption = settings.Get<string>("Caption") ?? providerName;

			var signOutWreply = settings.Get<string>("SignOutWreply")
				?? (!string.IsNullOrWhiteSpace(wreply) ? new Uri(wreply).GetLeftPart(UriPartial.Authority) : null);
			var callbackPath = settings.Get<PathString?>("CallbackPath");
			var externalLoginCallbackPath = settings.Get<PathString?>("ExternalLoginCallbackPath");
			var singleLogoutServiceRequestPath = settings.Get<PathString?>("SingleLogoutServiceRequestPath");
			var singleLogoutServiceResponsePath = settings.Get<PathString?>("SingleLogoutServiceResponsePath");

			var backchannelTimeout = settings.Get<TimeSpan?>("BackchannelTimeout");
			var refreshOnIssuerKeyNotFound = settings.Get<bool?>("RefreshOnIssuerKeyNotFound");
			var useTokenLifetime = settings.Get<bool?>("UseTokenLifetime");

			var signingCertificateFindType = settings.GetEnum<X509FindType>("SigningCertificateFindType");
			var signingCertificateFindValue = settings.Get<string>("SigningCertificateFindValue");
			var registrationClaimsMapping = settings.Get<string>("RegistrationClaimsMapping");
			var loginClaimsMapping = settings.Get<string>("LoginClaimsMapping");
			var logoutEnabled = settings.Get<bool?>("ExternalLogoutEnabled").GetValueOrDefault(DefaultExternalLogoutEnabled);
			var registrationEnabled = settings.Get<bool?>("RegistrationEnabled").GetValueOrDefault(DefaultRegistrationEnabled);
			var allowContactMappingWithEmail = settings.Get<bool?>("AllowContactMappingWithEmail").GetValueOrDefault(DefaultAllowContactMappingWithEmail);

			// required settings

			var options = new Saml2AuthenticationOptionsExtended
			{
				ExternalLoginCallbackPath = externalLoginCallbackPath ?? _externalLoginCallbackPath,
				MetadataAddress = metadataAddress,
				Wtrealm = wtrealm,
				AuthenticationMode = authenticationMode ?? AuthenticationMode.Passive,
				ExternalLogoutEnabled = logoutEnabled,
				RegistrationEnabled = registrationEnabled,
				RegistrationClaimsMapping = registrationClaimsMapping,
				LoginClaimsMapping = loginClaimsMapping,
				AllowContactMappingWithEmail = allowContactMappingWithEmail
			};

			// optional settings

			if (!string.IsNullOrWhiteSpace(authenticationType)) options.AuthenticationType = authenticationType;
			if (!string.IsNullOrWhiteSpace(signInAsAuthenticationType)) options.SignInAsAuthenticationType = signInAsAuthenticationType;
			if (!string.IsNullOrWhiteSpace(caption)) options.Caption = caption;

			if (!string.IsNullOrWhiteSpace(wreply)) options.Wreply = wreply;
			if (!string.IsNullOrWhiteSpace(signOutWreply)) options.SignOutWreply = signOutWreply;
			if (callbackPath != null) options.CallbackPath = callbackPath.GetValueOrDefault();
			if (singleLogoutServiceRequestPath != null) options.SingleLogoutServiceRequestPath = singleLogoutServiceRequestPath.GetValueOrDefault();
			if (singleLogoutServiceResponsePath != null) options.SingleLogoutServiceResponsePath = singleLogoutServiceResponsePath.GetValueOrDefault();

			if (backchannelTimeout != null) options.BackchannelTimeout = backchannelTimeout.GetValueOrDefault();
			if (refreshOnIssuerKeyNotFound != null) options.RefreshOnIssuerKeyNotFound = refreshOnIssuerKeyNotFound.GetValueOrDefault();
			if (useTokenLifetime != null) options.UseTokenLifetime = useTokenLifetime.GetValueOrDefault();

			if (signingCertificateFindType != null && signingCertificateFindValue != null)
			{
				options.SigningCertificate = CertificateUtil.Load(StoreName.My, StoreLocation.LocalMachine, signingCertificateFindType.Value, signingCertificateFindValue);
			}

			SetTokenValidationParameters(settings, options.TokenValidationParameters, null);

			return options;
		}

		#endregion

		#region OAuth2

		protected virtual MicrosoftAccountAuthenticationOptionsExtended ToMicrosoft(CrmWebsite website)
		{
			var options = ToOAuthOptions(website, "Microsoft", "ClientId", "ClientSecret");
			if (options == null) return null;

			var microsoft = new MicrosoftAccountAuthenticationOptionsExtended
			{
				CookieManager = new AuthenticationCookieManager(),
				ClientId = options.Id,
				ClientSecret = options.Secret,
				ExternalLogoutEnabled = options.ExternalLogoutEnabled,
				RegistrationEnabled = options.RegistrationEnabled,
				RegistrationClaimsMapping = options.RegistrationClaimsMapping,
				LoginClaimsMapping = options.LoginClaimsMapping,
				AllowContactMappingWithEmail = options.AllowContactMappingWithEmail
			};

			if (!string.IsNullOrWhiteSpace(options.Caption)) microsoft.Caption = options.Caption;
			if (options.CallbackPath != null) microsoft.CallbackPath = options.CallbackPath.GetValueOrDefault();
			if (!string.IsNullOrWhiteSpace(options.AuthenticationType)) microsoft.AuthenticationType = options.AuthenticationType;
			if (!string.IsNullOrWhiteSpace(options.SignInAsAuthenticationType)) microsoft.SignInAsAuthenticationType = options.SignInAsAuthenticationType;
			if (options.BackchannelTimeout != null) microsoft.BackchannelTimeout = options.BackchannelTimeout.GetValueOrDefault();
			if (options.AuthenticationMode != null) microsoft.AuthenticationMode = options.AuthenticationMode.GetValueOrDefault();

			// TODO: apply corresponding default permissions
			// override the default wl.basic scope with wl.emails
			//Rescope(microsoft.Scope, options.Scope ?? new[]{ "wl.emails" });

			if (options.Scope != null)
			{
				Rescope(microsoft.Scope, options.Scope);
			}

			return microsoft;
		}

		protected virtual TwitterAuthenticationOptionsExtended ToTwitter(CrmWebsite website)
		{
			var options = ToOAuthOptions(website, "Twitter", "ConsumerKey", "ConsumerSecret");

			if (options == null) return null;

			var twitter = new TwitterAuthenticationOptionsExtended
			{
				CookieManager = new AuthenticationCookieManager(),
				ConsumerKey = options.Id,
				ConsumerSecret = options.Secret,
				BackchannelCertificateValidator = new CertificateSubjectKeyIdentifierValidator(new[]
				{
					// specify the subject key identifier for the latest api.twitter.com root CA certificate
					"A5EF0B11CEC04103A34A659048B21CE0572D7D47", // VeriSign Class 3 Secure Server CA - G2
					"0D445C165344C1827E1D20AB25F40163D8BE79A5", // VeriSign Class 3 Secure Server CA - G3
					"5F60CF619055DF8443148A602AB2F57AF44318EF", // Symantec Class 3 Secure Server CA - G4
					"B13EC36903F8BF4701D498261A0802EF63642BC3", // DigiCert High Assurance EV Root CA
				}),
				ExternalLogoutEnabled = options.ExternalLogoutEnabled,
				RegistrationEnabled = options.RegistrationEnabled,
				RegistrationClaimsMapping = options.RegistrationClaimsMapping,
				LoginClaimsMapping = options.LoginClaimsMapping,
				AllowContactMappingWithEmail = options.AllowContactMappingWithEmail
			};

			if (!string.IsNullOrWhiteSpace(options.Caption)) twitter.Caption = options.Caption;
			if (options.CallbackPath != null) twitter.CallbackPath = options.CallbackPath.GetValueOrDefault();
			if (!string.IsNullOrWhiteSpace(options.AuthenticationType)) twitter.AuthenticationType = options.AuthenticationType;
			if (!string.IsNullOrWhiteSpace(options.SignInAsAuthenticationType)) twitter.SignInAsAuthenticationType = options.SignInAsAuthenticationType;
			if (options.BackchannelTimeout != null) twitter.BackchannelTimeout = options.BackchannelTimeout.GetValueOrDefault();
			if (options.AuthenticationMode != null) twitter.AuthenticationMode = options.AuthenticationMode.GetValueOrDefault();
			if (options.CertificateSubjectKeyIdentifierValidator != null) twitter.BackchannelCertificateValidator = new CertificateSubjectKeyIdentifierValidator(options.CertificateSubjectKeyIdentifierValidator);
			if (options.CertificateThumbprintValidator != null) twitter.BackchannelCertificateValidator = new CertificateThumbprintValidator(options.CertificateThumbprintValidator);

			return twitter;
		}

		protected virtual FacebookAuthenticationOptionsExtended ToFacebook(CrmWebsite website)
		{
			var options = ToOAuthOptions(website, "Facebook", "AppId", "AppSecret");

			if (options == null) return null;

			var facebook = new FacebookAuthenticationOptionsExtended
			{
				CookieManager = new AuthenticationCookieManager(),
				AppId = options.Id,
				AppSecret = options.Secret,
				ExternalLogoutEnabled = options.ExternalLogoutEnabled,
				RegistrationEnabled = options.RegistrationEnabled,
				RegistrationClaimsMapping = options.RegistrationClaimsMapping,
				LoginClaimsMapping = options.LoginClaimsMapping,
				AllowContactMappingWithEmail = options.AllowContactMappingWithEmail
			};

			if (!string.IsNullOrWhiteSpace(options.Caption)) facebook.Caption = options.Caption;
			if (options.CallbackPath != null) facebook.CallbackPath = options.CallbackPath.GetValueOrDefault();
			if (!string.IsNullOrWhiteSpace(options.AuthenticationType)) facebook.AuthenticationType = options.AuthenticationType;
			if (!string.IsNullOrWhiteSpace(options.SignInAsAuthenticationType)) facebook.SignInAsAuthenticationType = options.SignInAsAuthenticationType;
			if (options.BackchannelTimeout != null) facebook.BackchannelTimeout = options.BackchannelTimeout.GetValueOrDefault();
			if (options.AuthenticationMode != null) facebook.AuthenticationMode = options.AuthenticationMode.GetValueOrDefault();
			if (options.Scope != null) Rescope(facebook.Scope, options.Scope);


			// Portal can not work without email. With recent fb changes it is not available without explicit request
			if (!facebook.Scope.Contains("email"))
			{
				facebook.Scope.Add("email");
			}

			if (!facebook.Fields.Contains("email"))
			{
				facebook.Fields.Add("email");
			}

			return facebook;
		}

		protected virtual GoogleOAuth2AuthenticationOptionsExtended ToGoogle(CrmWebsite website)
		{
			var options = ToOAuthOptions(website, "Google", "ClientId", "ClientSecret");

			if (options == null) return null;

			var google = new GoogleOAuth2AuthenticationOptionsExtended
			{
				CookieManager = new AuthenticationCookieManager(),
				ClientId = options.Id,
				ClientSecret = options.Secret,
				ExternalLogoutEnabled = options.ExternalLogoutEnabled,
				RegistrationEnabled = options.RegistrationEnabled,
				RegistrationClaimsMapping = options.RegistrationClaimsMapping,
				LoginClaimsMapping = options.LoginClaimsMapping,
				AllowContactMappingWithEmail = options.AllowContactMappingWithEmail
			};

			if (!string.IsNullOrWhiteSpace(options.Caption)) google.Caption = options.Caption;
			if (options.CallbackPath != null) google.CallbackPath = options.CallbackPath.GetValueOrDefault();
			if (!string.IsNullOrWhiteSpace(options.AuthenticationType)) google.AuthenticationType = options.AuthenticationType;
			if (!string.IsNullOrWhiteSpace(options.SignInAsAuthenticationType)) google.SignInAsAuthenticationType = options.SignInAsAuthenticationType;
			if (options.BackchannelTimeout != null) google.BackchannelTimeout = options.BackchannelTimeout.GetValueOrDefault();
			if (options.AuthenticationMode != null) google.AuthenticationMode = options.AuthenticationMode.GetValueOrDefault();
			if (options.Scope != null) Rescope(google.Scope, options.Scope);

			return google;
		}

		protected virtual LinkedInAuthenticationOptionsExtended ToLinkedIn(CrmWebsite website)
		{
			var options = ToOAuthOptions(website, "LinkedIn", "ConsumerKey", "ConsumerSecret");

			if (options == null) return null;

			var linkedIn = new LinkedInAuthenticationOptionsExtended
			{
				ClientId = options.Id,
				ClientSecret = options.Secret,
				ExternalLogoutEnabled = options.ExternalLogoutEnabled,
				RegistrationEnabled = options.RegistrationEnabled,
				RegistrationClaimsMapping = options.RegistrationClaimsMapping,
				LoginClaimsMapping = options.LoginClaimsMapping,
				AllowContactMappingWithEmail = options.AllowContactMappingWithEmail
			};

			if (!string.IsNullOrWhiteSpace(options.Caption)) linkedIn.Caption = options.Caption;
			if (options.CallbackPath != null) linkedIn.CallbackPath = options.CallbackPath.GetValueOrDefault();
			if (!string.IsNullOrWhiteSpace(options.AuthenticationType)) linkedIn.AuthenticationType = options.AuthenticationType;
			if (!string.IsNullOrWhiteSpace(options.SignInAsAuthenticationType)) linkedIn.SignInAsAuthenticationType = options.SignInAsAuthenticationType;
			if (options.BackchannelTimeout != null) linkedIn.BackchannelTimeout = options.BackchannelTimeout.GetValueOrDefault();
			if (options.AuthenticationMode != null) linkedIn.AuthenticationMode = options.AuthenticationMode.GetValueOrDefault();
			if (options.Scope != null) Rescope(linkedIn.Scope, options.Scope);

			return linkedIn;
		}

		protected virtual YahooAuthenticationOptionsExtended ToYahoo(CrmWebsite website)
		{
			var options = ToOAuthOptions(website, "Yahoo", "ClientId", "ClientSecret");

			if (options == null) return null;

			var yahoo = new YahooAuthenticationOptionsExtended
			{
				ConsumerKey = options.Id,
				ConsumerSecret = options.Secret,
				ExternalLogoutEnabled = options.ExternalLogoutEnabled,
				RegistrationEnabled = options.RegistrationEnabled,
				RegistrationClaimsMapping = options.RegistrationClaimsMapping,
				LoginClaimsMapping = options.LoginClaimsMapping,
				AllowContactMappingWithEmail = options.AllowContactMappingWithEmail
			};

			if (!string.IsNullOrWhiteSpace(options.Caption)) yahoo.Caption = options.Caption;
			if (options.CallbackPath != null) yahoo.CallbackPath = options.CallbackPath.GetValueOrDefault();
			if (!string.IsNullOrWhiteSpace(options.AuthenticationType)) yahoo.AuthenticationType = options.AuthenticationType;
			if (!string.IsNullOrWhiteSpace(options.SignInAsAuthenticationType)) yahoo.SignInAsAuthenticationType = options.SignInAsAuthenticationType;
			if (options.BackchannelTimeout != null) yahoo.BackchannelTimeout = options.BackchannelTimeout.GetValueOrDefault();
			if (options.AuthenticationMode != null) yahoo.AuthenticationMode = options.AuthenticationMode.GetValueOrDefault();

			return yahoo;
		}

		private static CommonAuthenticationOptions ToOAuthOptions(CrmWebsite website, string providerName, string idSettingName, string secretSettingName)
		{
			const string basePath = "Authentication/OpenAuth";

			var id = website.Settings.Get<string>("{0}/{1}/{2}".FormatWith(basePath, providerName, idSettingName))
				?? website.Settings.Get<string>("{0}/{1}/ClientId".FormatWith(basePath, providerName));

			var secret = website.Settings.Get<string>("{0}/{1}/{2}".FormatWith(basePath, providerName, secretSettingName))
				?? website.Settings.Get<string>("{0}/{1}/ClientSecret".FormatWith(basePath, providerName));

			if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(secret)) return null;

			var authenticationType = website.Settings.Get<string>("{0}/{1}/AuthenticationType".FormatWith(basePath, providerName));

			var authenticationMode = website.Settings.GetEnum<AuthenticationMode>("{0}/{1}/AuthenticationMode".FormatWith(basePath, providerName));
			var scopeText = website.Settings.Get<string>("{0}/{1}/Scope".FormatWith(basePath, providerName));
			var scope = !string.IsNullOrWhiteSpace(scopeText)
				? scopeText.Split(' ', ',')
				: null;

			var certificateSubjectKeyIdentifierValidatorText = website.Settings.Get<string>("{0}/{1}/CertificateSubjectKeyIdentifierValidator".FormatWith(basePath, providerName));
			var certificateSubjectKeyIdentifierValidator = !string.IsNullOrWhiteSpace(certificateSubjectKeyIdentifierValidatorText)
				? certificateSubjectKeyIdentifierValidatorText.Split(' ', ',')
				: null;

			var certificateThumbprintValidatorText = website.Settings.Get<string>("{0}/{1}/CertificateThumbprintValidator".FormatWith(basePath, providerName));
			var certificateThumbprintValidator = !string.IsNullOrWhiteSpace(certificateThumbprintValidatorText)
				? certificateThumbprintValidatorText.Split(' ', ',')
				: null;

			var registrationClaimsMapping = website.Settings.Get<string>("{0}/{1}/RegistrationClaimsMapping".FormatWith(basePath, providerName));
			var loginClaimsMapping = website.Settings.Get<string>("{0}/{1}/LoginClaimsMapping".FormatWith(basePath, providerName));
			var logoutEnabled = website.Settings.Get<bool?>("{0}/{1}/ExternalLogoutEnabled".FormatWith(basePath, providerName)).GetValueOrDefault(DefaultExternalLogoutEnabled);
			var registrationEnabled = website.Settings.Get<bool?>("{0}/{1}/RegistrationEnabled".FormatWith(basePath, providerName)).GetValueOrDefault(DefaultRegistrationEnabled);
			var allowContactMappingWithEmail = website.Settings.Get<bool?>("{0}/{1}/AllowContactMappingWithEmail".FormatWith(basePath, providerName)).GetValueOrDefault(DefaultAllowContactMappingWithEmail);

			var options = new CommonAuthenticationOptions(authenticationType)
			{
				Id = id,
				Secret = secret,
				Caption = website.Settings.Get<string>("{0}/{1}/Caption".FormatWith(basePath, providerName)),
				BackchannelTimeout = website.Settings.Get<TimeSpan?>("{0}/{1}/BackchannelTimeout".FormatWith(basePath, providerName)),
				CallbackPath = website.Settings.Get<PathString?>("{0}/{1}/CallbackPath".FormatWith(basePath, providerName)),
				SignInAsAuthenticationType = website.Settings.Get<string>("{0}/{1}/SignInAsAuthenticationType".FormatWith(basePath, providerName)),
				AuthenticationMode = authenticationMode,
				Scope = scope,
				CertificateSubjectKeyIdentifierValidator = certificateSubjectKeyIdentifierValidator,
				CertificateThumbprintValidator = certificateThumbprintValidator,
				ExternalLogoutEnabled = logoutEnabled,
				RegistrationEnabled = registrationEnabled,
				RegistrationClaimsMapping = registrationClaimsMapping,
				LoginClaimsMapping = loginClaimsMapping,
				AllowContactMappingWithEmail = allowContactMappingWithEmail
			};

			return options;
		}

		private static void Rescope(ICollection<string> scope, IEnumerable<string> values)
		{
			scope.Clear();
			foreach (var value in values) scope.Add(value);
		}

		#endregion

		#region OpenIdConnect

		protected virtual OpenIdConnectAuthenticationOptionsExtended ToOpenIdConnectOptions(string providerName, CrmWebsiteSettingCollection settings)
		{
			var clientId = settings.Get<string>("ClientId");
			var metadataAddress = settings.Get<string>("MetadataAddress");
			var authority = settings.Get<string>("Authority");
			var authenticationType = settings.Get<string>("AuthenticationType");

			// do not allow the Azure AD application to be added through site settings
			if (!string.IsNullOrWhiteSpace(clientId) && string.Equals(clientId, Configuration.PortalSettings.Instance.Authentication.ClientId, StringComparison.OrdinalIgnoreCase))
			{
				return null;
			}

			var enabled = 
				(!string.IsNullOrWhiteSpace(clientId) && !string.IsNullOrWhiteSpace(authority))
				|| (!string.IsNullOrWhiteSpace(clientId) && !string.IsNullOrWhiteSpace(metadataAddress) && !string.IsNullOrWhiteSpace(authenticationType));

			if (!enabled) return null;

			var clientSecret = settings.Get<string>("ClientSecret");
			var authenticationMode = settings.GetEnum<AuthenticationMode>("AuthenticationMode");
			var signInAsAuthenticationType = settings.Get<string>("SignInAsAuthenticationType");
			var caption = settings.Get<string>("Caption") ?? providerName;
			var resource = settings.Get<string>("Resource");
			var responseType = settings.Get<string>("ResponseType");

			var redirectUri = settings.Get<string>("RedirectUri");
			var postLogoutRedirectUri = settings.Get<string>("PostLogoutRedirectUri")
				?? (!string.IsNullOrWhiteSpace(redirectUri) ? new Uri(redirectUri).GetLeftPart(UriPartial.Authority) : null);
			var callbackPath = settings.Get<PathString?>("CallbackPath");

			var backchannelTimeout = settings.Get<TimeSpan?>("BackchannelTimeout");
			var refreshOnIssuerKeyNotFound = settings.Get<bool?>("RefreshOnIssuerKeyNotFound");
			var useTokenLifetime = settings.Get<bool?>("UseTokenLifetime");

			var scopeText = settings.Get<string>("Scope");
			var scope = !string.IsNullOrWhiteSpace(scopeText)
				? scopeText.Split(' ', ',')
				: new[] { "openid" };
			var registrationClaimsMapping = settings.Get<string>("RegistrationClaimsMapping");
			var loginClaimsMapping = settings.Get<string>("LoginClaimsMapping");
			var oIDAsNameIDClaimEnabled = settings.Get<bool?>("ObjectIdentifierAsNameIdentifierClaimEnabled").GetValueOrDefault(DefaultObjectIdentifierAsNameIdentifierClaimEnabled);
			var logoutEnabled = settings.Get<bool?>("ExternalLogoutEnabled").GetValueOrDefault(DefaultExternalLogoutEnabled);
			var registrationEnabled = settings.Get<bool?>("RegistrationEnabled").GetValueOrDefault(DefaultRegistrationEnabled);
			var allowContactMappingWithEmail = settings.Get<bool?>("AllowContactMappingWithEmail").GetValueOrDefault(DefaultAllowContactMappingWithEmail);
			var passwordResetPolicyId = settings.Get<string>("PasswordResetPolicyId");
			var profileEditPolicyId = settings.Get<string>("ProfileEditPolicyId");
			var defaultPolicyId = settings.Get<string>("DefaultPolicyId");

			// required settings

			var options = new OpenIdConnectAuthenticationOptionsExtended
			{
				CookieManager = new AuthenticationCookieManager(),
				ClientId = clientId,
				MetadataAddress = metadataAddress,
				Authority = authority,
				AuthenticationMode = authenticationMode ?? AuthenticationMode.Passive,
				AuthenticationType = authenticationType ?? authority,
				Notifications = new OpenIdConnectAuthenticationNotifications
				{
					RedirectToIdentityProvider = OnRedirectToIdentityProvider,
					SecurityTokenValidated = notification => OnOpenIdConnectSecurityTokenValidated(notification, oIDAsNameIDClaimEnabled),
					AuthenticationFailed = OnAuthenticationFailed,
				},
				ExternalLogoutEnabled = logoutEnabled,
				RegistrationEnabled = registrationEnabled,
				RegistrationClaimsMapping = registrationClaimsMapping,
				LoginClaimsMapping = loginClaimsMapping,
				AllowContactMappingWithEmail = allowContactMappingWithEmail,
				PasswordResetPolicyId = passwordResetPolicyId,
				ProfileEditPolicyId = profileEditPolicyId,
				DefaultPolicyId = defaultPolicyId
			};

			// optional settings

			if (!string.IsNullOrWhiteSpace(clientSecret)) options.ClientSecret = clientSecret;
			if (!string.IsNullOrWhiteSpace(signInAsAuthenticationType)) options.SignInAsAuthenticationType = signInAsAuthenticationType;
			if (!string.IsNullOrWhiteSpace(caption)) options.Caption = caption;
			if (!string.IsNullOrWhiteSpace(resource)) options.Resource = resource;
			if (!string.IsNullOrWhiteSpace(responseType)) options.ResponseType = responseType;

			if (!string.IsNullOrWhiteSpace(redirectUri)) options.RedirectUri = redirectUri;
			if (!string.IsNullOrWhiteSpace(postLogoutRedirectUri)) options.PostLogoutRedirectUri = postLogoutRedirectUri;
			if (callbackPath != null) options.CallbackPath = callbackPath.Value;

			if (backchannelTimeout != null) options.BackchannelTimeout = backchannelTimeout.Value;
			if (refreshOnIssuerKeyNotFound != null) options.RefreshOnIssuerKeyNotFound = refreshOnIssuerKeyNotFound.Value;
			if (useTokenLifetime != null) options.UseTokenLifetime = useTokenLifetime.Value;

			if (scope != null) options.Scope = string.Join(" ", scope);

			SetTokenValidationParameters(settings, options.TokenValidationParameters, null);

			return options;
		}

		/// <summary>
		/// On unhandled exceptions in OWIN authentication redirects to
		/// special ExternalAuthenticationFailed page with predefined messages if any.
		/// Used for avoiding yellow-screen-of-death
		/// </summary>
		/// <param name="notification">0</param>
		/// <returns>Redirect to SignIn</returns>
		protected virtual Task OnAuthenticationFailed(
			AuthenticationFailedNotification<OpenIdConnectMessage, OpenIdConnectAuthenticationOptions> notification)
		{
			notification.HandleResponse();

			var errorHandlerPath = _externalAuthenticationFailedPath;
			var errorHandlerUrl = errorHandlerPath.ToString();
			var errorDescription = notification.ProtocolMessage?.ErrorDescription;
			
			if (errorDescription != null && errorDescription.Contains(AzureADB2CPasswordResetPolicyErrorCode))
			{
				// Handle the error code that Azure AD B2C throws when trying to reset a password from the login page because password reset is not supported by a "sign-up or sign-in policy"

				ADXTrace.Instance.TraceInfo(TraceCategory.Application, $"User requested Password Reset. Error Description: {notification.ProtocolMessage?.ErrorDescription}");

				var passwordResetUrl = new UrlBuilder(_externalPasswordResetPath.ToString());

				var options = notification.Options as OpenIdConnectAuthenticationOptionsExtended;

				if (string.IsNullOrWhiteSpace(options?.PasswordResetPolicyId))
				{
					notification.Response.Redirect(_loginPath.ToString());

					return Task.FromResult(0);
				}

				passwordResetUrl.QueryString.Add(PasswordResetPolicyQueryStringParameter, options.PasswordResetPolicyId);
				passwordResetUrl.QueryString.Add("provider", notification.Options.AuthenticationType);
				
				notification.Response.Redirect(passwordResetUrl.PathWithQueryString);
			}
			else if (errorDescription != null && errorDescription.Contains(AzureADB2CUserCancelledErrorCode))
			{
				// Handle the error code that AD B2C emits when a users cancels sign-up or cancels password reset or cancels profile edit

				ADXTrace.Instance.TraceInfo(TraceCategory.Application, $"Sign-up or Password Reset or Profile Edit cancelled. Error Description: {notification.ProtocolMessage?.ErrorDescription}");

				if (notification.Request.User?.Identity != null && notification.Request.User.Identity.IsAuthenticated)
				{
					notification.Response.Redirect("~/");
				}
				else
				{
					notification.Response.Redirect(SingleSignOn ? "~/" : _loginPath.ToString());
				}
			}
			else
			{
				if (notification.Exception.Message == OwinAuthenticationFailedAccessDeniedMsg)
				{
					errorHandlerUrl = errorHandlerPath.Add(new QueryString(MessageQueryStringParameter, OwinAuthenticationFailedAccessDeniedMsg));

					ADXTrace.Instance.TraceInfo(TraceCategory.Application, $"Access Denied Exception during OpenIdConnect or Azure Authentication in {notification.Exception.Source}: {notification.Exception.Message}; Error Description: {notification.ProtocolMessage?.ErrorDescription}");
				}
				else
				{
					ADXTrace.Instance.TraceError(TraceCategory.Application, $"Exception during OpenIdConnect or Azure Authentication in {notification.Exception.Source}: {notification.Exception.Message}; Error Description: {notification.ProtocolMessage?.ErrorDescription}");
				}

				notification.Response.Redirect(errorHandlerUrl);
			}

			return Task.FromResult(0);
		}

		protected virtual Task OnOpenIdConnectSecurityTokenValidated(SecurityTokenValidatedNotification<OpenIdConnectMessage, OpenIdConnectAuthenticationOptions> notification, bool oIDAsNameIDClaimEnabled)
		{
			if (oIDAsNameIDClaimEnabled)
			{
				// replace the regular Azure persistent ID NameIdentifier with the ObjectId as the new NameIdentifier

				ReplaceNameIdentifierClaim(notification.AuthenticationTicket.Identity);
			}

			if (!string.IsNullOrWhiteSpace(notification.ProtocolMessage?.Code))
			{
				notification.AuthenticationTicket.Identity.AddClaim(new Claim("AccessCode", notification.ProtocolMessage.Code));
			}
			
			return Task.FromResult(0);
		}

		protected virtual Task OnRedirectToIdentityProvider(RedirectToIdentityProviderNotification<OpenIdConnectMessage, OpenIdConnectAuthenticationOptions> notification)
		{
			// adjust the reply URL to use the hostname of the current request

			var request = notification.Request;
			var baseUri = request.Scheme + Uri.SchemeDelimiter + request.Host + request.PathBase;
			var redirectUri = baseUri + notification.Options.CallbackPath;

			notification.ProtocolMessage.RedirectUri = redirectUri;
			
			// handle special case for Azure AD B2C password reset policy and profile edit policy
			var policy = notification.OwinContext.Get<string>("Policy");
			var options = notification.Options as OpenIdConnectAuthenticationOptionsExtended;
			var isPasswordPolicy = !string.IsNullOrEmpty(policy) && !string.IsNullOrWhiteSpace(options?.PasswordResetPolicyId) && policy.Equals(options.PasswordResetPolicyId, StringComparison.InvariantCultureIgnoreCase);
			var isProfileEditPolicy = !string.IsNullOrEmpty(policy) && !string.IsNullOrWhiteSpace(options?.ProfileEditPolicyId) && policy.Equals(options.ProfileEditPolicyId, StringComparison.InvariantCultureIgnoreCase);
			if (isPasswordPolicy || isProfileEditPolicy)
			{
				var issuerPath = notification.ProtocolMessage.IssuerAddress;
				var issuerUri = new Uri(issuerPath);
				var query = issuerUri.ParseQueryString();
				notification.ProtocolMessage.Scope = OpenIdConnectScope.OpenId;
				notification.ProtocolMessage.ResponseType = OpenIdConnectResponseType.IdToken;
				if (!string.IsNullOrWhiteSpace(options.DefaultPolicyId))
				{
					notification.ProtocolMessage.IssuerAddress = Regex.Replace(notification.ProtocolMessage.IssuerAddress, options.DefaultPolicyId, policy, RegexOptions.IgnoreCase);
				}
				else if (query != null && query.HasKeys() && !string.IsNullOrWhiteSpace(query.Get("p")))
				{
					query.Set("p", policy);
					var issuerAddress = new UriBuilder(issuerPath) { Query = query.ToString() };
					notification.ProtocolMessage.IssuerAddress = issuerAddress.ToString();
				}
				else
				{
					ADXTrace.Instance.TraceWarning(TraceCategory.Application, "Could not change policy. Default Policy could not be determined.");
				}
			}

			return Task.FromResult(0);
		}

		protected virtual void SetTokenValidationParameters(CrmWebsiteSettingCollection settings, TokenValidationParameters parameters, string[] defaultValidAudiences)
		{
			var validAudiencesText = settings.Get<string>("ValidAudiences");
			var validAudiences = !string.IsNullOrWhiteSpace(validAudiencesText) ? validAudiencesText.Split(',') : defaultValidAudiences;
			var validAudience = settings.Get<string>("ValidAudience");

			var validIssuersText = settings.Get<string>("ValidIssuers");
			var validIssuers = !string.IsNullOrWhiteSpace(validIssuersText) ? validIssuersText.Split(',') : null;
			var validIssuer = settings.Get<string>("ValidIssuer");

			var clockSkew = settings.Get<TimeSpan?>("ClockSkew");
			var nameClaimType = settings.Get<string>("NameClaimType");
			var roleClaimType = settings.Get<string>("RoleClaimType");
			var requireExpirationTime = settings.Get<bool?>("RequireExpirationTime");
			var requireSignedTokens = settings.Get<bool?>("RequireSignedTokens");
			var saveSigninToken = settings.Get<bool?>("SaveSigninToken");
			var validateActor = settings.Get<bool?>("ValidateActor");
			var validateAudience = settings.Get<bool?>("ValidateAudience");
			var validateIssuer = settings.Get<bool?>("ValidateIssuer");
			var validateLifetime = settings.Get<bool?>("ValidateLifetime");
			var validateIssuerSigningKey = settings.Get<bool?>("ValidateIssuerSigningKey");

			if (validAudiences != null) parameters.ValidAudiences = validAudiences;
			if (validAudience != null) parameters.ValidAudience = validAudience;
			if (validIssuers != null) parameters.ValidIssuers = validIssuers;
			if (validIssuer != null) parameters.ValidIssuer = validIssuer;

			if (clockSkew != null) parameters.ClockSkew = clockSkew.GetValueOrDefault();
			if (nameClaimType != null) parameters.NameClaimType = nameClaimType;
			if (roleClaimType != null) parameters.RoleClaimType = roleClaimType;
			if (requireExpirationTime != null) parameters.RequireExpirationTime = requireExpirationTime.GetValueOrDefault();
			if (requireSignedTokens != null) parameters.RequireSignedTokens = requireSignedTokens.GetValueOrDefault();
			if (saveSigninToken != null) parameters.SaveSigninToken = saveSigninToken.GetValueOrDefault();
			if (validateActor != null) parameters.ValidateActor = validateActor.GetValueOrDefault();
			if (validateAudience != null) parameters.ValidateAudience = validateAudience.GetValueOrDefault();
			if (validateIssuer != null) parameters.ValidateIssuer = validateIssuer.GetValueOrDefault();
			if (validateLifetime != null) parameters.ValidateLifetime = validateLifetime.GetValueOrDefault();
			if (validateIssuerSigningKey != null) parameters.ValidateIssuerSigningKey = validateIssuerSigningKey.GetValueOrDefault();
		}

		protected virtual IEnumerable<KeyValuePair<string, CrmWebsiteSettingCollection>> ToGroupedSettings(CrmWebsiteSettingCollection settings, string pattern)
		{
			// filter settings and group by the provider: Authentication/WsFederation/<provider>/<name>

			return
				from setting in settings
				let match = Regex.Match(setting.Name, pattern)
				where match.Success
				let provider = match.Groups["provider"].Value
				let siteSetting = ToCrmWebsiteSetting(match.Groups["name"].Value, setting)
				group siteSetting by provider into grp
				select new KeyValuePair<string, CrmWebsiteSettingCollection>(grp.Key, new CrmWebsiteSettingCollection(grp));
		}

		protected virtual IEnumerable<T> ToGroupedOptions<T>(CrmWebsiteSettingCollection settings, string pattern, Func<string, CrmWebsiteSettingCollection, T> toOptions)
			where T : AuthenticationOptions
		{
			return ToGroupedSettings(settings, pattern).Select(group => toOptions(group.Key, group.Value)).Where(option => option != null);
		}

		private static CrmWebsiteSetting ToCrmWebsiteSetting(string name, CrmWebsiteSetting setting)
		{
			var entity = Microsoft.Xrm.Client.EntityExtensions.Clone(setting.Entity);
			entity.SetAttributeValue("adx_name", name);
			return new CrmWebsiteSetting(entity);
		}

		private static void ReplaceNameIdentifierClaim(ClaimsIdentity identity)
		{
			var objectIdClaim = identity.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier");
			var nameIdentifierClaim = identity.FindFirst(ClaimTypes.NameIdentifier);

			identity.RemoveClaim(nameIdentifierClaim);
			identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, objectIdClaim.Value, objectIdClaim.ValueType,
				objectIdClaim.Issuer, objectIdClaim.OriginalIssuer, objectIdClaim.Subject));
		}

		#endregion

		public async Task<string> GetAuthenticationTypeAsync(ExternalLoginInfo loginInfo, CancellationToken cancellationToken)
		{
			var options = await this.GetAuthenticationOptionsExtendedAsync(loginInfo, cancellationToken);
			return options?.AuthenticationType;
		}

		public Task<IAuthenticationOptionsExtended> GetAuthenticationOptionsExtendedAsync(ExternalLoginInfo loginInfo, CancellationToken cancellationToken)
		{
			return this.GetAuthenticationOptionsExtendedAsync(loginInfo?.Login?.LoginProvider, cancellationToken);
		}

		public async Task<IAuthenticationOptionsExtended> GetAuthenticationOptionsExtendedAsync(string provider, CancellationToken cancellationToken)
		{
			if (string.IsNullOrWhiteSpace(provider))
			{
				return null;
			}

			foreach (var option in this.GetAllAuthenticationOptions())
			{
				if (await option.ToIssuer(cancellationToken) == provider)
				{
					return option;
				}
			}

			return null;
		}

		public IAuthenticationOptionsExtended GetAuthenticationOptionsExtended(string authenticationType)
		{
			return !string.IsNullOrWhiteSpace(authenticationType)
				? this.GetAllAuthenticationOptions()?.FirstOrDefault(option => option.AuthenticationType == authenticationType)
				: null;
		}

		private IEnumerable<IAuthenticationOptionsExtended> GetAllAuthenticationOptions()
		{
			if (this.MicrosoftAccount != null)
			{
				yield return this.MicrosoftAccount;
			}

			if (this.Twitter != null)
			{
				yield return this.Twitter;
			}

			if (this.Facebook != null)
			{
				yield return this.Facebook;
			}

			if (this.Google != null)
			{
				yield return this.Google;
			}

			if (this.LinkedIn != null)
			{
				yield return this.LinkedIn;
			}

			if (this.Yahoo != null)
			{
				yield return this.Yahoo;
			}

			if (this.AzureAdOptions != null)
			{
				yield return this.AzureAdOptions;
			}

			if (this.OpenIdConnectOptions != null)
			{
				foreach (var option in this.OpenIdConnectOptions)
				{
					yield return option;
				}
			}

			if (this.WsFederationOptions != null)
			{
				foreach (var option in this.WsFederationOptions)
				{
					yield return option;
				}
			}

			if (this.Saml2Options != null)
			{
				foreach (var option in this.Saml2Options)
				{
					yield return option;
				}
			}
		}
	}
}
