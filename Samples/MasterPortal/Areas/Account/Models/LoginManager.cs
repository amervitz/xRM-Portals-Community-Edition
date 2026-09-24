/*
  Copyright (c) Microsoft Corporation. All rights reserved.
  Licensed under the MIT License. See License.txt in the project root for license information.
*/

namespace Site.Areas.Account.Models
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Mail;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Web;
    using System.Web.Caching;
    using System.Web.Mvc;
    using System.Web.WebPages;
    using AccountManagement;
    using Adxstudio.Xrm;
    using Adxstudio.Xrm.AspNet.Cms;
    using Adxstudio.Xrm.AspNet.Identity;
    using Adxstudio.Xrm.Resources;
    using Adxstudio.Xrm.Web;
    using Adxstudio.Xrm.AspNet.Mvc;
    using Adxstudio.Xrm.Configuration;
    using Adxstudio.Xrm.IdentityModel.ActiveDirectory;
    using Adxstudio.Xrm.Services;
    using Microsoft.AspNet.Identity;
    using Microsoft.AspNet.Identity.Owin;
    using Microsoft.Owin.Security;
    using Microsoft.Xrm.Sdk;
    using Microsoft.Xrm.Client;

    /// <summary>
    /// Validating the Registration details
    /// </summary>
    public class LoginManager
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="LoginManager" /> class.
		/// </summary>
		/// <param name="httpContext">The context.</param>
		/// <param name="controller">The controller.</param>
		public LoginManager(HttpContextBase httpContext, Controller controller = null)
		{
			this.HttpContext = httpContext;
			this.Controller = controller;
			this.Error = new List<string>();
			this.SetAuthSettings();
		}
		

		/// <summary>
		/// Token refresh retry count
		/// </summary>
		private const int TokenRetryCount = 3;

		/// <summary>
		/// Graph Cache total minutes
		/// </summary>
		private const int GraphCacheTtlMinutes = 5;

		/// <summary>
		/// Http Context base
		/// </summary>
		public HttpContextBase HttpContext { get; private set; }

		/// <summary>
		/// Login Controller 
		/// </summary>
		public Controller Controller { get; private set; }

		/// <summary>
		/// Holds error messages retrieved while validations
		/// </summary>
		public List<string> Error { get; private set; }

		/// <summary>
		/// Holds Authentication settings required for the page
		/// </summary>
		public Adxstudio.Xrm.AspNet.Mvc.AuthenticationSettings AuthSettings { get; private set; }

		/// <summary>
		/// Application Invitation Manager local variable
		/// </summary>
		private ApplicationInvitationManager invitationManager;

		/// <summary>
		/// Application Invitation Manager
		/// </summary>
		public ApplicationInvitationManager InvitationManager
		{
			get
			{
				return this.invitationManager ?? this.HttpContext.GetOwinContext().Get<ApplicationInvitationManager>();
			}

			set
			{
				this.invitationManager = value;
			}
		}

		/// <summary>
		/// Application User Manager local variable
		/// </summary>
		private ApplicationUserManager userManager;

		/// <summary>
		/// Application User Manager 
		/// </summary>
		public ApplicationUserManager UserManager
		{
			get
			{
				return this.userManager ?? this.HttpContext.GetOwinContext().GetUserManager<ApplicationUserManager>();
			}

			set
			{
				this.userManager = value;
			}
		}

		/// <summary>
		/// Application Website Manager local variable
		/// </summary>
		private ApplicationWebsiteManager websiteManager;

		/// <summary>
		/// Application Website Manager
		/// </summary>
		public ApplicationWebsiteManager WebsiteManager
		{
			get
			{
				return this.websiteManager ?? this.HttpContext.GetOwinContext().Get<ApplicationWebsiteManager>();
			}

			set
			{
				this.websiteManager = value;
			}
		}

		/// <summary>
		/// Application SignIn Manager local variable
		/// </summary>
		private ApplicationSignInManager signInManager;

		/// <summary>
		/// Application SignIn Manager
		/// </summary>
		public ApplicationSignInManager SignInManager
		{
			get
			{
				return this.signInManager ?? this.HttpContext.GetOwinContext().Get<ApplicationSignInManager>();
			}

			set
			{
				this.signInManager = value;
			}
		}

		/// <summary>
		/// Identity Errors to get error descriptions
		/// </summary>
		public CrmIdentityErrorDescriber IdentityErrors { get; set; }

		/// <summary>
		/// Authentication Manager
		/// </summary>
		public IAuthenticationManager AuthenticationManager
		{
			get
			{
				return this.HttpContext.GetOwinContext().Authentication;
			}
		}

		/// <summary>
		/// Application Startup Settings Manager local variable
		/// </summary>
		private ApplicationStartupSettingsManager startupSettingsManager;

		/// <summary>
		/// Application Startup Settings Manager
		/// </summary>
		public ApplicationStartupSettingsManager StartupSettingsManager
		{
			get
			{
				return this.startupSettingsManager ?? this.HttpContext.GetOwinContext().Get<ApplicationStartupSettingsManager>();
			}

			private set
			{
				this.startupSettingsManager = value;
			}
		}

		/// <summary>
		/// Gets Graph Client
		/// </summary>
		/// <param name="loginInfo">login info</param>
		/// <returns>retuns the Microsoft Graph client</returns>
		private PortalGraphClient GetGraphClient(ExternalLoginInfo loginInfo)
		{
			var accessCodeClaim = loginInfo.ExternalIdentity.FindFirst("AccessCode");
			var accessCode = accessCodeClaim?.Value;

			return new PortalGraphClient(
				PortalSettings.Instance.Graph.RootUrl,
				() => this.TokenManager.GetTokenAsync(accessCode));
		}

		/// <summary>
		/// token Manager
		/// </summary>
		private Lazy<CrmTokenManager> tokenManager = new Lazy<CrmTokenManager>(CreateCrmTokenManager);

		/// <summary>
		/// Create Crm TokenManager
		/// </summary>
		/// <returns>Crm Token Manager</returns>
		private static CrmTokenManager CreateCrmTokenManager()
		{
			return new CrmTokenManager(PortalSettings.Instance.Authentication, PortalSettings.Instance.Certificate, PortalSettings.Instance.Graph.RootUrl);
		}

		/// <summary>
		/// Token Manager property
		/// </summary>
		private ICrmTokenManager TokenManager => this.tokenManager.Value;

		/// <summary>
		/// ToEmail: Gets the email for the graph user
		/// </summary>
		/// <param name="graphUser">graph user</param>
		/// <returns>returns email id</returns>
		public static string ToEmail(GraphUser graphUser)
		{
			if (!string.IsNullOrWhiteSpace(graphUser.Mail))
			{
				return graphUser.Mail;
			}

			return graphUser.OtherMails?.FirstOrDefault() ?? graphUser.UserPrincipalName;
		}

		/// <summary>
		/// Apply Claims Mapping
		/// </summary>
		/// <param name="user">Application User</param>
		/// <param name="loginInfo">Login Info</param>
		/// <param name="claimsMapping">Claims mapping</param>
		private static void ApplyClaimsMapping(ApplicationUser user, ExternalLoginInfo loginInfo, string claimsMapping)
		{
			try
			{
				if (user != null && !string.IsNullOrWhiteSpace(claimsMapping))
				{
					foreach (var pair in claimsMapping.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
					{
						var pieces = pair.Split('=');
						var claimValue = loginInfo.ExternalIdentity.Claims.FirstOrDefault(c => c.Type == pieces[1]);

						if (pieces.Length == 2
							&& !user.Entity.Attributes.ContainsKey(pieces[0])
							&& claimValue != null)
						{
							user.Entity.SetAttributeValue(pieces[0], claimValue.Value);
							user.IsDirty = true;
						}
					}
				}
			}
			catch (Exception ex)
			{
				WebEventSource.Log.GenericErrorException(ex);
			}
		}

		/// <summary>
		/// Get Auth Settings
		/// </summary>
		/// <returns>Return Auth Settings</returns>
		private void SetAuthSettings()
		{
			var isLocal = this.HttpContext.IsDebuggingEnabled && this.HttpContext.Request.IsLocal;
			var website = this.HttpContext.GetWebsite();
			this.AuthSettings = website.GetAuthenticationSettings(isLocal);
		}

		/// <summary>
		/// Add validation errors 
		/// </summary>
		/// <param name="error">error identified</param>
		public void AddErrors(IdentityError error)
		{
			this.AddErrors(IdentityResult.Failed(error.Description));
		}

		/// <summary>
		/// Format error
		/// </summary>
		/// <param name="result">error description</param>
		public void AddErrors(IdentityResult result)
		{
			foreach (var error in result.Errors)
			{
				if (this.Controller != null)
				{
					this.Controller.ModelState.AddModelError(string.Empty, error);
				}
				else
				{
					this.Error.Add(string.Concat(this.Error.Count == 0 ? string.Empty : "<li>", error, this.Error.Count == 0 ? "\n" : "</li>\n"));
				}
			}
		}

		/// <summary>
		/// Validate Email
		/// </summary>
		/// <param name="email">email value to validate</param>
		/// <returns>returns true if validated</returns>
		public bool ValidateEmail(string email)
		{
			try
			{
				MailAddress m = new MailAddress(email);

				return true;
			}
			catch (FormatException)
			{
				return false;
			}
		}

		/// <summary>
		/// To ContactId 
		/// </summary>
		/// <param name="invitation">Application Invitation</param>
		/// <returns>returns Invitation entity reference</returns>
		public EntityReference ToContactId(ApplicationInvitation invitation)
		{
			return invitation != null && invitation.InvitedContact != null
				? new EntityReference(invitation.InvitedContact.LogicalName, invitation.InvitedContact.Id) { Name = invitation.Email }
				: null;
		}

		/// <summary>
		/// Find Invitation value by Code Async
		/// </summary>
		/// <param name="invitationCode">invitation code</param>
		/// <returns>returns application invitation value</returns>
		public async Task<ApplicationInvitation> FindInvitationByCodeAsync(string invitationCode)
		{
			if (string.IsNullOrWhiteSpace(invitationCode))
			{
				return null;
			}

			return await this.InvitationManager.FindByCodeAsync(invitationCode);
		}

		/// <summary>
		/// Sign In Async
		/// </summary>
		/// <param name="user">user value</param>
		/// <param name="returnUrl">return url</param>
		/// <param name="isPersistent">is persistent value</param>
		/// <param name="rememberBrowser">remeber browser value</param>
		/// <returns>action result</returns>
		public async Task<Enums.RedirectTo> SignInAsync(ApplicationUser user, string returnUrl, bool isPersistent = false, bool rememberBrowser = false)
		{
			await this.SignInManager.SignInAsync(user, isPersistent, rememberBrowser);
			return await this.RedirectOnPostAuthenticate(returnUrl, null);
		}

		/// <summary>
		/// Updates the current request language based on user preferences. If needed, updates the return URL as well.
		/// </summary>
		/// <param name="user">Application User that is currently being logged in.</param>
		/// <param name="returnUrl">Return URL to be updated if needed.</param>
		private void UpdateCurrentLanguage(ApplicationUser user, ref string returnUrl)
		{
			var languageContext = this.HttpContext.GetContextLanguageInfo();
			if (languageContext.IsCrmMultiLanguageEnabled)
			{
				// At this point, ContextLanguageInfo.UserPreferredLanguage is not set, as the user is technically not yet logged in
				// As this is a one-time operation, accessing adx_preferredlanguageid here directly instead of modifying CrmUser
				var preferredLanguage = user.Entity.GetAttributeValue<EntityReference>("adx_preferredlanguageid");
				if (preferredLanguage != null)
				{
					var websiteLangauges = languageContext.ActiveWebsiteLanguages.ToArray();

					// Only consider published website languages for users
					var newLanguage = languageContext.GetWebsiteLanguageByPortalLanguageId(preferredLanguage.Id, websiteLangauges, true);
					if (newLanguage != null)
					{
						if (ContextLanguageInfo.DisplayLanguageCodeInUrl && !string.IsNullOrEmpty(returnUrl))
						{
							returnUrl = languageContext.FormatUrlWithLanguage(false, newLanguage.Code, returnUrl.AsAbsoluteUri(this.HttpContext.Request.Url));
						}
					}
				}
			}
		}

		/// <summary>
		/// Redirect page on post Authentication
		/// </summary>
		/// <param name="returnUrl">return Url</param>
		/// <param name="invitationCode">invitation code</param>
		/// <param name="loginInfo">login information</param>
		/// <param name="cancellationToken">cancellation token</param>
		/// <returns>Action result</returns>
		private async Task<Enums.RedirectTo> RedirectOnPostAuthenticate(string returnUrl, string invitationCode, ExternalLoginInfo loginInfo = null, CancellationToken cancellationToken = default(CancellationToken))
		{
			var identity = this.AuthenticationManager.AuthenticationResponseGrant.Identity;
			var userId = identity.GetUserId();
			var user = await this.UserManager.FindByIdAsync(userId);

			if (user != null && loginInfo != null)
			{
				var options = await this.StartupSettingsManager.GetAuthenticationOptionsExtendedAsync(loginInfo, cancellationToken);
				var claimsMapping = options?.LoginClaimsMapping;

				if (!string.IsNullOrWhiteSpace(claimsMapping))
				{
					ApplyClaimsMapping(user, loginInfo, claimsMapping);
				}
			}

			return await this.RedirectOnPostAuthenticate(user, returnUrl, invitationCode, loginInfo, cancellationToken);
		}

		
		/// <summary>
		/// Post Authentication, redirect to appropirate page.
		/// </summary>
		/// <param name="user">Application user</param>
		/// <param name="returnUrl">return url</param>
		/// <param name="invitationCode">invitation code</param>
		/// <param name="loginInfo">Login info</param>
		/// <param name="cancellationToken">cancellation token</param>
		/// <returns>return enum</returns>
		private async Task<Enums.RedirectTo> RedirectOnPostAuthenticate(ApplicationUser user, string returnUrl, string invitationCode, ExternalLoginInfo loginInfo = null, CancellationToken cancellationToken = default(CancellationToken))
		{
			if (user != null)
			{
				this.UpdateCurrentLanguage(user, ref returnUrl);
				this.UpdateLastSuccessfulLogin(user);
				await this.ApplyGraphUser(user, loginInfo, cancellationToken);

				if (user.IsDirty)
				{
					await this.UserManager.UpdateAsync(user);
					user.IsDirty = false;
				}

				IdentityResult redeemResult;
				var invitation = await this.FindInvitationByCodeAsync(invitationCode);

				if (invitation != null)
				{
					// Redeem invitation for the existing/registered contact
					redeemResult = await this.InvitationManager.RedeemAsync(invitation, user, this.HttpContext.Request.UserHostAddress);
				}
				else if (!string.IsNullOrWhiteSpace(invitationCode))
				{
					redeemResult = IdentityResult.Failed(this.IdentityErrors.InvalidInvitationCode().Description);
				}
				else
				{
					redeemResult = IdentityResult.Success;
				}

				if (!redeemResult.Succeeded)
				{
					return Enums.RedirectTo.Redeem;
				}

				if (!this.DisplayModeIsActive() && (user.HasProfileAlert || user.ProfileModifiedOn == null))
				{
					return Enums.RedirectTo.Profile;
				}
			}

			return Enums.RedirectTo.Local;
		}

		/// <summary>
		/// Updates date and time of last successful login
		/// </summary>
		/// <param name="user">Application User that is currently being logged in.</param>
		private void UpdateLastSuccessfulLogin(ApplicationUser user)
		{
			if (!this.AuthSettings.LoginTrackingEnabled)
			{
				return;
			}

			user.Entity.SetAttributeValue("adx_identity_lastsuccessfullogin", DateTime.UtcNow);
			user.IsDirty = true;
		}

		/// <summary>
		/// Apply Graph User
		/// </summary>
		/// <param name="user">Application user</param>
		/// <param name="loginInfo">Login Info</param>
		/// <param name="cancellationToken">Cancellation token</param>
		/// <returns>async task</returns>
		private async Task ApplyGraphUser(ApplicationUser user, ExternalLoginInfo loginInfo, CancellationToken cancellationToken)
		{
			if (loginInfo != null
				&& this.StartupSettingsManager.AzureAdOptions != null
				&& !string.IsNullOrWhiteSpace(this.StartupSettingsManager.AzureAdOptions.AuthenticationType)
				&& !string.IsNullOrWhiteSpace(PortalSettings.Instance.Graph.RootUrl)
				&& string.IsNullOrWhiteSpace(user.FirstName)
				&& string.IsNullOrWhiteSpace(user.LastName)
				&& string.IsNullOrWhiteSpace(user.Email))
			{
				var authenticationType = await this.StartupSettingsManager.GetAuthenticationTypeAsync(loginInfo, cancellationToken);

				if (this.StartupSettingsManager.AzureAdOptions.AuthenticationType == authenticationType)
				{
					// update the contact using Graph

					try
					{
						var graphUser = await this.GetGraphUser(loginInfo);

						user.FirstName = graphUser.GivenName;
						user.LastName = graphUser.Surname;
						user.Email = ToEmail(graphUser);
						user.IsDirty = true;
					}
					catch (Exception ex)
					{
						var guid = WebEventSource.Log.GenericErrorException(ex);
						this.Error.Add(string.Format(ResourceManager.GetString("Generic_Error_Message"), guid));
					}
				}
			}
		}

		/// <summary>
		/// Check whether Display Mode Is Active or not
		/// </summary>
		/// <returns>returns true/false</returns>
		private bool DisplayModeIsActive()
		{
			return DisplayModeProvider.Instance
				.GetAvailableDisplayModesForContext(this.HttpContext, null)
				.OfType<HostNameSettingDisplayMode>()
				.Any();
		}

		/// <summary>
		/// Gets Graph User
		/// </summary>
		/// <param name="loginInfo">Login information</param>
		/// <returns>user value</returns>
		private async Task<GraphUser> GetGraphUser(ExternalLoginInfo loginInfo)
		{
			var userCacheKey = $"{loginInfo.Login.ProviderKey}_graphUser";
			var userAuthResultCacheKey = $"{loginInfo.Login.ProviderKey}_userAuthResult";

			// if the user's already gone through the Graph check, this will be set with the error that happened
			if (this.HttpContext.Cache[userAuthResultCacheKey] != null)
			{
				return null;
			}

			// if the cache here is null, we haven't retrieved the Graph user yet. retrieve it
			if (this.HttpContext.Cache[userCacheKey] == null)
			{
				return await this.GetGraphUser(loginInfo, userCacheKey, userAuthResultCacheKey);
			}

			return (GraphUser)this.HttpContext.Cache[userCacheKey];
		}

		/// <summary>
		/// Gets Graphical User
		/// </summary>
		/// <param name="loginInfo">login information</param>
		/// <param name="userCacheKey">User Cache key value</param>
		/// <param name="userAuthResultCacheKey">User authentication cache key</param>
		/// <returns>User value</returns>
		private async Task<GraphUser> GetGraphUser(ExternalLoginInfo loginInfo, string userCacheKey, string userAuthResultCacheKey)
		{
			var client = this.GetGraphClient(loginInfo);

			GraphUser user = null;
			

			// retry tokenRetryCount times to retrieve the users. each time it fails, it will nullify the cache and try again
			for (var x = 0; x < TokenRetryCount; x++)
			{
				try
				{
					ADXTrace.Instance.TraceInfo(TraceCategory.Application, $"Attempting to retrieve user from Graph with NameIdentifier {loginInfo.Login.ProviderKey}.");

					// when we call this, the client will try to retrieve a token from GetAuthTokenTask()
					user = await client.GetMeAsync();

					// if we get here then everything is alright. stop looping
					break;
				}
				catch (GraphServiceException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
				{
					ADXTrace.Instance.TraceInfo(TraceCategory.Application, "Current GraphClient auth token didn't seem to work. Discarding...");

					// the token didn't seem to work. throw away cached token to retrieve new one
					this.TokenManager.Reset();
				}
			}

			// if users is null here, we have a config problem where we can't get correct auth tokens despite repeated attempts
			if (user == null)
			{
				this.OutputGraphError(Enums.AzureADGraphAuthResults.AuthConfigProblem, userAuthResultCacheKey, loginInfo);
				return null;
			}

			// add cache entry for graph user object. it will expire in GraphCacheTtlMinutes minutes
			HttpRuntime.Cache.Add(userCacheKey, user, null, DateTime.MaxValue, TimeSpan.FromMinutes(GraphCacheTtlMinutes), CacheItemPriority.Normal, null);

			return user;
		}

		/// <summary>
		/// Adds error occured while creating Graph user to trace
		/// </summary>
		/// <param name="result">Azure AD Graph Authentication Results</param>
		/// <param name="userAuthResultCacheKey">User authentication result cache key</param>
		/// <param name="loginInfo">Login information</param>
		/// <returns>Azure AD graph authentication results</returns>
		private Enums.AzureADGraphAuthResults OutputGraphError(Enums.AzureADGraphAuthResults result, string userAuthResultCacheKey, ExternalLoginInfo loginInfo)
		{
			// add cache entry for graph check result. it will expire in GraphCacheTtlMinutes minutes
			HttpRuntime.Cache.Add(userAuthResultCacheKey, result, null, DateTime.MaxValue, TimeSpan.FromMinutes(GraphCacheTtlMinutes), CacheItemPriority.Normal, null);

			switch (result)
			{
				case Enums.AzureADGraphAuthResults.UserNotFound:
					ADXTrace.Instance.TraceError(TraceCategory.Application, $"Azure AD didn't have the user with the specified NameIdentifier: {loginInfo.Login.ProviderKey}");

					return Enums.AzureADGraphAuthResults.UserNotFound;
				case Enums.AzureADGraphAuthResults.UserHasNoEmail:
					ADXTrace.Instance.TraceError(TraceCategory.Application, "UPN was not set on user.");

					return Enums.AzureADGraphAuthResults.UserHasNoEmail;
				case Enums.AzureADGraphAuthResults.NoValidLicense:
					ADXTrace.Instance.TraceError(TraceCategory.Application, $"No valid license was found assigned to the user: {loginInfo.Login.ProviderKey}");

					return Enums.AzureADGraphAuthResults.NoValidLicense;
				case Enums.AzureADGraphAuthResults.AuthConfigProblem:
					ADXTrace.Instance.TraceError(TraceCategory.Application, "There's a critical problem with retrieving Graph auth tokens.");

					return Enums.AzureADGraphAuthResults.AuthConfigProblem;
			}

			ADXTrace.Instance.TraceError(TraceCategory.Application, $"An unknown graph error occurred. Passed through UserNotFound, UserHasNoEmail, and NoValidLicense. NameIdentifier: {loginInfo.Login.ProviderKey}");

			return Enums.AzureADGraphAuthResults.UnknownError;
		}
	}
}
