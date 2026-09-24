/*
  Copyright (c) Microsoft Corporation. All rights reserved.
  Licensed under the MIT License. See License.txt in the project root for license information.
*/

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Web.Caching;
using System.Web.Mvc;
using System.Web.Routing;
using System.Web.Security.AntiXss;
using System.Web.WebPages;
using Adxstudio.Xrm;
using Adxstudio.Xrm.AspNet.Cms;
using Adxstudio.Xrm.AspNet.Identity;
using Adxstudio.Xrm.AspNet.Mvc;
using Adxstudio.Xrm.Configuration;
using Adxstudio.Xrm.Core.Flighting;
using Adxstudio.Xrm.IdentityModel.ActiveDirectory;
using Adxstudio.Xrm.Resources;
using Adxstudio.Xrm.Services;
using Adxstudio.Xrm.Web;
using Adxstudio.Xrm.Web.Mvc;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.Owin;
using Microsoft.Owin.Security;
using Microsoft.Xrm.Client;
using Microsoft.Xrm.Sdk;
using Site.Areas.Account.Models;
using Site.Areas.Account.ViewModels;
using Site.Areas.AccountManagement;

namespace Site.Areas.Account.Controllers
{
	[Authorize]
	[PortalView, UnwrapNotFoundException]
	[OutputCache(NoStore = true, Duration = 0)]
	public class LoginController : Controller
	{
		public LoginController()
		{
		}

		public LoginController(
			ApplicationUserManager userManager,
			ApplicationSignInManager signInManager,
			ApplicationInvitationManager invitationManager,
			ApplicationOrganizationManager organizationManager)
		{
			UserManager = userManager;
			SignInManager = signInManager;
			InvitationManager = invitationManager;
			OrganizationManager = organizationManager;
		}

		private ApplicationUserManager _userManager;
		public ApplicationUserManager UserManager
		{
			get
			{
				return _userManager ?? HttpContext.GetOwinContext().GetUserManager<ApplicationUserManager>();
			}
			private set
			{
				_userManager = value;
			}
		}

		private ApplicationInvitationManager _invitationManager;
		public ApplicationInvitationManager InvitationManager
		{
			get
			{
				return _invitationManager ?? HttpContext.GetOwinContext().Get<ApplicationInvitationManager>();
			}
			private set
			{
				_invitationManager = value;
			}
		}

		private ApplicationOrganizationManager _organizationManager;
		public ApplicationOrganizationManager OrganizationManager
		{
			get
			{
				return _organizationManager ?? HttpContext.GetOwinContext().Get<ApplicationOrganizationManager>();
			}
			private set
			{
				_organizationManager = value;
			}
		}

		private ApplicationStartupSettingsManager _startupSettingsManager;
		public ApplicationStartupSettingsManager StartupSettingsManager
		{
			get
			{
				return _startupSettingsManager ?? HttpContext.GetOwinContext().Get<ApplicationStartupSettingsManager>();
			}
			private set
			{
				_startupSettingsManager = value;
			}
		}

		private static Dictionary<string, bool> _validEssLicenses;
		private static Dictionary<string, bool> ValidEssLicenses
		{
			get
			{
				if (_validEssLicenses == null)
				{
					// get a comma delimited list of license skus from config
					var licenseConfigValue = PortalSettings.Instance.Ess.ValidLicenseSkus;

					var licenses = licenseConfigValue.Split(new[] { ',' });

					_validEssLicenses = new Dictionary<string, bool>();

					// build the dictionary
					foreach (var license in licenses)
					{
						_validEssLicenses[license] = true;   // we don't really care about the value. just need a quick lookup of the sku as key
					}
				}

				return _validEssLicenses;
			}
		}

		// the duration in minutes to allow cached ESS graph pieces to stay cached 
		private const int GraphCacheTtlMinutes = 5;

		//
		// GET: /Login/Login
		[HttpGet]
		[AllowAnonymous]
		[LanguageActionFilter]
		[OutputCache(CacheProfile = "UserShared")]
		public ActionResult Login(string returnUrl, string invitationCode)
		{
			if (!string.IsNullOrWhiteSpace(ViewBag.Settings.LoginButtonAuthenticationType))
			{
				return RedirectToAction("ExternalLogin", "Login", new { returnUrl, area = "Account", provider = ViewBag.Settings.LoginButtonAuthenticationType });
			}

			// if the portal is ess, don't use regular local signin (this page shows signin options)
			if (ViewBag.IsESS)
			{
				// this action will redirect to the specified provider's login page
				return RedirectToAction("ExternalLogin", "Login", new { returnUrl, area = "Account", provider = StartupSettingsManager.AzureAdOptions.AuthenticationType });
			}

			// this seems to be the only easy way to get this setting to the master page
			this.Request.RequestContext.RouteData.Values["DisableChatWidget"] = true;

			return View(GetLoginViewModel(null, null, returnUrl, invitationCode));
		}

		private ApplicationSignInManager _signInManager;

		public ApplicationSignInManager SignInManager
		{
			get
			{
				return _signInManager ?? HttpContext.GetOwinContext().Get<ApplicationSignInManager>();
			}
			private set { _signInManager = value; }
		}

		protected override void Initialize(RequestContext requestContext)
		{
			base.Initialize(requestContext);

			var isLocal = requestContext.HttpContext.IsDebuggingEnabled && requestContext.HttpContext.Request.IsLocal;
			var website = requestContext.HttpContext.GetWebsite();

			ViewBag.Settings = website.GetAuthenticationSettings(isLocal);
			ViewBag.IsESS = PortalSettings.Instance.Ess.IsEss;
			ViewBag.AzureAdOrExternalLoginEnabled = ViewBag.Settings.ExternalLoginEnabled || StartupSettingsManager.AzureAdOptions != null;
			ViewBag.ExternalRegistrationEnabled = StartupSettingsManager.ExternalRegistrationEnabled;
			ViewBag.IdentityErrors = website.GetIdentityErrors(this.HttpContext.GetOwinContext());
		}

		//
		// POST: /Login/Login
		[HttpPost]
		[AllowAnonymous]
		[ValidateAntiForgeryToken]
		[LocalLogin]
		[Throttle(Name= "LoginThrottle")]
		public async Task<ActionResult> Login(LoginViewModel model, string returnUrl, string invitationCode)
		{
			ViewBag.LoginSuccessful = false;

			if (ViewBag.Locked == true)
			{
				AddErrors(ViewBag.IdentityErrors.TooManyAttempts());
				return View(GetLoginViewModel(null, null, returnUrl, invitationCode));
			}

			if (!ModelState.IsValid
				|| (ViewBag.Settings.LocalLoginByEmail && string.IsNullOrWhiteSpace(model.Email))
				|| (!ViewBag.Settings.LocalLoginByEmail && string.IsNullOrWhiteSpace(model.Username)))
			{
				AddErrors(ViewBag.IdentityErrors.InvalidLogin());
				return View(GetLoginViewModel(model, null, returnUrl, invitationCode));
			}

			var rememberMe = ViewBag.Settings.RememberMeEnabled && model.RememberMe;

			// This doen't count login failures towards lockout only two factor authentication
			// To enable password failures to trigger lockout, change to shouldLockout: true
			SignInStatus result = ViewBag.Settings.LocalLoginByEmail
				? await SignInManager.PasswordSignInByEmailAsync(model.Email, model.Password, rememberMe, ViewBag.Settings.TriggerLockoutOnFailedPassword)
				: await SignInManager.PasswordSignInAsync(model.Username, model.Password, rememberMe, ViewBag.Settings.TriggerLockoutOnFailedPassword);

			switch (result)
			{
				case SignInStatus.Success:
					ViewBag.LoginSuccessful = true;
					return await RedirectOnPostAuthenticate(returnUrl, invitationCode);
				case SignInStatus.LockedOut:
					AddErrors(ViewBag.IdentityErrors.UserLocked());
					return View(GetLoginViewModel(model, null, returnUrl, invitationCode));
				case SignInStatus.RequiresVerification:
					ViewBag.LoginSuccessful = true;
					return RedirectToAction("SendCode", new { ReturnUrl = returnUrl, InvitationCode = invitationCode, RememberMe = rememberMe });
				case SignInStatus.Failure:
				default:
					AddErrors(ViewBag.IdentityErrors.InvalidLogin());
					return View(GetLoginViewModel(model, null, returnUrl, invitationCode));
			}
		}

		//
		// GET: /Login/VerifyCode
		[HttpGet]
		[AllowAnonymous]
		public async Task<ActionResult> VerifyCode(string provider, string returnUrl, bool rememberMe, string invitationCode)
		{
			if (PortalSettings.Instance.Ess.IsEss)
			{
				return HttpNotFound();
			}

			// Require that the user has already logged in via username/password or external login
			if (!await SignInManager.HasBeenVerifiedAsync())
			{
				return HttpNotFound();
			}

			if (ViewBag.Settings.IsDemoMode)
			{
				var user = await UserManager.FindByIdAsync(await SignInManager.GetVerifiedUserIdAsync());

				if (user != null && user.LogonEnabled)
				{
					var code = await UserManager.GenerateTwoFactorTokenAsync(user.Id, provider);
					ViewBag.DemoModeCode = code;
				}
			}

			return View(new VerifyCodeViewModel { Provider = provider, ReturnUrl = returnUrl, RememberMe = rememberMe, InvitationCode = invitationCode });
		}

		//
		// POST: /Login/VerifyCode
		[HttpPost]
		[AllowAnonymous]
		[ValidateAntiForgeryToken]
		public async Task<ActionResult> VerifyCode(VerifyCodeViewModel model)
		{
			if (PortalSettings.Instance.Ess.IsEss)
			{
				return HttpNotFound();
			}

			if (!ModelState.IsValid)
			{
				return View(model);
			}

			var rememberMe = ViewBag.Settings.RememberMeEnabled && model.RememberMe;
			var rememberBrowser = ViewBag.Settings.TwoFactorEnabled && ViewBag.Settings.RememberBrowserEnabled && model.RememberBrowser;

			SignInStatus result = await SignInManager.TwoFactorSignInAsync(model.Provider, model.Code, isPersistent: rememberMe, rememberBrowser: rememberBrowser);

			switch (result)
			{
				case SignInStatus.Success:
					return await RedirectOnPostAuthenticate(model.ReturnUrl, model.InvitationCode);
				case SignInStatus.LockedOut:
					AddErrors(ViewBag.IdentityErrors.UserLocked());
					return View(model);
				case SignInStatus.Failure:
				default:
					AddErrors(ViewBag.IdentityErrors.InvalidTwoFactorCode());
					return View(model);
			}
		}

		//
		// GET: /Login/ForgotPassword
		[HttpGet]
		[AllowAnonymous]
		[LocalLogin]
		[LanguageActionFilter]
		public ActionResult ForgotPassword()
		{
			if (!ViewBag.Settings.ResetPasswordEnabled)
			{
				return HttpNotFound();
			}

			return View();
		}

		//
		// POST: /Login/ForgotPassword
		[HttpPost]
		[AllowAnonymous]
		[ValidateAntiForgeryToken]
		[LocalLogin]
		public async Task<ActionResult> ForgotPassword(ForgotPasswordViewModel model)
		{
			if (!ViewBag.Settings.ResetPasswordEnabled)
			{
				return HttpNotFound();
			}

			if (ModelState.IsValid)
			{
				if (string.IsNullOrWhiteSpace(model.Email))
				{
					return HttpNotFound();
				}

				var user = await UserManager.FindByEmailAsync(model.Email);

				if (user == null || !user.LogonEnabled || (ViewBag.Settings.ResetPasswordRequiresConfirmedEmail && !(await UserManager.IsEmailConfirmedAsync(user.Id))))
				{
					// Don't reveal that the user does not exist or is not confirmed
					return View("ForgotPasswordConfirmation");
				}

				var code = await UserManager.GeneratePasswordResetTokenAsync(user.Id);
				var callbackUrl = Url.Action("ResetPassword", "Login", new { userId = user.Id, code = code }, protocol: Request.Url.Scheme);
				var parameters = new Dictionary<string, object> { { "UserId", user.Id }, { "Code", code }, { "UrlCode", AntiXssEncoder.UrlEncode(code) }, { "CallbackUrl", callbackUrl }, { "Email", model.Email } };
				try
				{
					await OrganizationManager.InvokeProcessAsync("adx_SendPasswordResetToContact", user.ContactId, parameters);
				}
				catch (System.ServiceModel.FaultException ex)
				{
					var guid = WebEventSource.Log.GenericErrorException(ex);
					ViewBag.ErrorMessage = string.Format(ResourceManager.GetString("Generic_Error_Message"), guid);

					return View("ForgotPassword");
				}
				//await	UserManager.SendEmailAsync(user.Id,	"Reset Password", "Please reset	your password by clicking here:	<a href=\""	+ callbackUrl +	"\">link</a>");

				if (ViewBag.Settings.IsDemoMode)
				{
					ViewBag.DemoModeLink = callbackUrl;
				}

				return View("ForgotPasswordConfirmation");
			}

			// If we got this far, something failed, redisplay form
			return View(model);
		}

		//
		// GET: /Login/ForgotPasswordConfirmation
		[HttpGet]
		[AllowAnonymous]
		[LocalLogin]
		public ActionResult ForgotPasswordConfirmation()
		{
			if (!ViewBag.Settings.ResetPasswordEnabled)
			{
				return HttpNotFound();
			}

			return View();
		}

		//
		// GET: /Login/ResetPassword
		[HttpGet]
		[AllowAnonymous]
		[LocalLogin]
		[LanguageActionFilter]
		public ActionResult ResetPassword(string userId, string code)
		{
			if (!ViewBag.Settings.ResetPasswordEnabled)
			{
				return HttpNotFound();
			}

			if (userId == null || code == null)
			{
				return HttpNotFound();
			}

			return View();
		}

		//
		// POST: /Login/ResetPassword
		[HttpPost]
		[AllowAnonymous]
		[ValidateAntiForgeryToken]
		[LocalLogin]
		public async Task<ActionResult> ResetPassword(ResetPasswordViewModel model)
		{
			if (!ViewBag.Settings.ResetPasswordEnabled)
			{
				return HttpNotFound();
			}

			if (!string.Equals(model.Password, model.ConfirmPassword))
			{
				ModelState.AddModelError("Password", ViewBag.IdentityErrors.PasswordConfirmationFailure().Description);
			}

			if (!ModelState.IsValid)
			{
				return View(model);
			}

			var user = await UserManager.FindByIdAsync(model.UserId);

			if (user == null || !user.LogonEnabled)
			{
				// Don't reveal that the user does not exist
				return RedirectToAction("ResetPasswordConfirmation", "Login");
			}

			var result = await UserManager.ResetPasswordAsync(user.Id, model.Code, model.Password);
			if (result.Succeeded)
			{
				return RedirectToAction("ResetPasswordConfirmation", "Login");
			}

			AddErrors(result);
			return View();
		}

		//
		// GET: /Login/ResetPasswordConfirmation
		[HttpGet]
		[AllowAnonymous]
		[LocalLogin]
		public ActionResult ResetPasswordConfirmation()
		{
			if (!ViewBag.Settings.ResetPasswordEnabled)
			{
				return HttpNotFound();
			}

			return View();
		}

		//
		// GET: /Login/ConfirmEmail
		[HttpGet]
		[AllowAnonymous]
		public ActionResult ConfirmEmail()
		{
			if (User.Identity.IsAuthenticated)
			{
				return RedirectToProfile(null);
			}

			return View();
		}

		//
		// POST: /Login/ExternalLogin
		[HttpPost]
		[AllowAnonymous]
		[ValidateAntiForgeryToken]
		[ExternalLogin]
		public ActionResult ExternalLogin(string provider, string returnUrl, string invitationCode)
		{
			//StartupSettingsManager.AzureAdOptions.Notifications.AuthorizationCodeReceived
			// Request a redirect to the external login provider
			return new ChallengeResult(provider, Url.Action("ExternalLoginCallback", "Login", new { ReturnUrl = returnUrl, InvitationCode = invitationCode, Provider = provider }));
		}

		//
		// GET: /Login/ExternalLogin
		[HttpGet]
		[AllowAnonymous]
		[ExternalLogin]
		[ActionName("ExternalLogin")]
		public ActionResult GetExternalLogin(string provider, string returnUrl, string invitationCode)
		{
			return ExternalLogin(provider, returnUrl, invitationCode);
		}

		//
		// GET: /Login/ExternalPasswordReset
		[HttpGet]
		[AllowAnonymous]
		[ExternalLogin]
		public void ExternalPasswordReset(string passwordResetPolicyId, string provider)
		{
			if (string.IsNullOrWhiteSpace(passwordResetPolicyId) || string.IsNullOrWhiteSpace(provider))
			{
				ADXTrace.Instance.TraceInfo(TraceCategory.Application, "External Password Reset could not be invoked. Password Reset Policy ID was not specified or the provider was not defined.");

				Redirect("~/");
			}

			// Let the middleware know you are trying to use the password reset policy
			HttpContext.GetOwinContext().Set("Policy",  passwordResetPolicyId);

			var redirectUri = Url.Action("ExternalLogin", "Login", new { area = "Account", provider });

			// Set the page to redirect to after password has been successfully changed.
			var authenticationProperties = new AuthenticationProperties { RedirectUri = redirectUri };

			ADXTrace.Instance.TraceInfo(TraceCategory.Application, "External Password Reset invoked.");

			HttpContext.GetOwinContext().Authentication.Challenge(authenticationProperties, provider);
		}

		//
		// GET: /Login/SendCode
		[HttpGet]
		[AllowAnonymous]
		public async Task<ActionResult> SendCode(string returnUrl, string invitationCode, bool rememberMe = false)
		{
			if (PortalSettings.Instance.Ess.IsEss)
			{
				return HttpNotFound();
			}

			var userId = await SignInManager.GetVerifiedUserIdAsync();
			if (userId == null)
			{
				throw new ApplicationException("Account error.");
			}
			var userFactors = await UserManager.GetValidTwoFactorProvidersAsync(userId);

			if (userFactors.Count() == 1)
			{
				// Send the code directly for a single option
				return await SendCode(new SendCodeViewModel { SelectedProvider = userFactors.Single(), ReturnUrl = returnUrl, RememberMe = rememberMe, InvitationCode = invitationCode });
			}

			var factorOptions = userFactors.Select(purpose => new SelectListItem { Text = purpose, Value = purpose }).ToList();
			return View(new SendCodeViewModel { Providers = factorOptions, ReturnUrl = returnUrl, RememberMe = rememberMe, InvitationCode = invitationCode });
		}

		//
		// POST: /Login/SendCode
		[HttpPost]
		[AllowAnonymous]
		[ValidateAntiForgeryToken]
		public async Task<ActionResult> SendCode(SendCodeViewModel model)
		{
			if (PortalSettings.Instance.Ess.IsEss)
			{
				return HttpNotFound();
			}

			if (!ModelState.IsValid)
			{
				return View();
			}

			// Generate the token and send it
			if (!await SignInManager.SendTwoFactorCodeAsync(model.SelectedProvider))
			{
				throw new ApplicationException("Account error.");
			}
			return RedirectToAction("VerifyCode", new { Provider = model.SelectedProvider, ReturnUrl = model.ReturnUrl, RememberMe = model.RememberMe, InvitationCode = model.InvitationCode });
		}

		//
		// GET: /Login/ExternalLoginCallback
		[HttpGet]
		[AllowAnonymous]
		[ExternalLogin]
		public async Task<ActionResult> ExternalLoginCallback(string returnUrl, string invitationCode, string provider)
		{
			var loginInfo = await AuthenticationManager.GetExternalLoginInfoAsync();

			if (loginInfo == null)
			{
				return RedirectToAction("Login");
			}

			if (ViewBag.IsESS)
			{
				var graphResult = await DoAdditionalEssGraphWork(loginInfo);

				// will be passed as query parameter to Access Denied - Missing License liquid template
				// to determine correct error message to display
				string error = string.Empty;

				switch (graphResult)
				{
					case Enums.AzureADGraphAuthResults.NoValidLicense:
						error = "missing_license";
						break;
					case Enums.AzureADGraphAuthResults.UserHasNoEmail:
						break;
					case Enums.AzureADGraphAuthResults.UserNotFound:
						break;
				}
				if (graphResult != Enums.AzureADGraphAuthResults.NoErrors)
				{
					return new RedirectToSiteMarkerResult("Access Denied - Missing License", new NameValueCollection { { "error", error } });
				}
			}

			// Sign in the user with this external login provider if the user already has a login
			var result = await SignInManager.ExternalSignInAsync(loginInfo, false, (bool)ViewBag.Settings.TriggerLockoutOnFailedPassword);

			switch (result)
			{
				case SignInStatus.Success:
					return await RedirectOnPostAuthenticate(returnUrl, invitationCode, loginInfo);
				case SignInStatus.LockedOut:
					AddErrors(ViewBag.IdentityErrors.UserLocked());
					return View("Login", GetLoginViewModel(null, null, returnUrl, invitationCode));
				case SignInStatus.RequiresVerification:
					return RedirectToAction("SendCode", new { ReturnUrl = returnUrl, InvitationCode = invitationCode });
				case SignInStatus.Failure:
				default:
					// If the user does not have an account, then prompt the user to create an account
					ViewBag.ReturnUrl = returnUrl;
					ViewBag.InvitationCode = invitationCode;
					var contactId = ToContactId(await FindInvitationByCodeAsync(invitationCode));
					var email = (contactId != null ? contactId.Name : null) ?? await ToEmail(loginInfo);
					var username = loginInfo.Login.ProviderKey;
					var firstName = loginInfo.ExternalIdentity.FindFirstValue(System.Security.Claims.ClaimTypes.GivenName);
					var lastName = loginInfo.ExternalIdentity.FindFirstValue(System.Security.Claims.ClaimTypes.Surname);

					return await ExternalLoginConfirmation(username, email, firstName, lastName, returnUrl, invitationCode, loginInfo);
			}
		}

		private async Task<string> ToEmail(ExternalLoginInfo loginInfo, CancellationToken cancellationToken = default(CancellationToken))
		{
			if (!string.IsNullOrWhiteSpace(loginInfo?.Email))
			{
				return loginInfo.Email;
			}

			if (this.StartupSettingsManager.AzureAdOptions != null
				&& !string.IsNullOrWhiteSpace(this.StartupSettingsManager.AzureAdOptions.AuthenticationType)
				&& !string.IsNullOrWhiteSpace(PortalSettings.Instance.Graph.RootUrl))
			{
				var authenticationType = await this.StartupSettingsManager.GetAuthenticationTypeAsync(loginInfo, cancellationToken);

				if (this.StartupSettingsManager.AzureAdOptions.AuthenticationType == authenticationType)
				{
					// this is an Azure AD sign-in
					GraphUser graphUser;

					try
					{
						graphUser = await GetGraphUser(loginInfo);
					}
					catch (Exception ex)
					{
						var guid = WebEventSource.Log.GenericErrorException(ex);
						ViewBag.ErrorMessage = string.Format(ResourceManager.GetString("Generic_Error_Message"), guid);

						graphUser = null;
					}

					if (graphUser != null)
					{
						return ToEmail(graphUser);
					}
				}
			}

			var identity = loginInfo?.ExternalIdentity;

			if (identity != null)
			{
				var claim = identity.FindFirst(System.Security.Claims.ClaimTypes.Email)
					?? identity.FindFirst("email")
					?? identity.FindFirst("emails")
					?? identity.FindFirst(System.Security.Claims.ClaimTypes.Upn);

				if (claim != null)
				{
					return claim.Value;
				}
			}

			return null;
		}

		private static string ToEmail(GraphUser graphUser)
		{
			if (!string.IsNullOrWhiteSpace(graphUser.Mail)) return graphUser.Mail;

			return graphUser.OtherMails?.FirstOrDefault() ?? graphUser.UserPrincipalName;
		}

		private async Task<Enums.AzureADGraphAuthResults> DoAdditionalEssGraphWork(ExternalLoginInfo loginInfo)
		{
			var userCacheKey = $"{loginInfo.Login.ProviderKey}_graphUser";
			var userAuthResultCacheKey = $"{loginInfo.Login.ProviderKey}_userAuthResult";
			GraphUser user = null;

			// if the user's already gone through the Graph check, this will be set with the error that happened
			if (HttpContext.Cache[userAuthResultCacheKey] != null)
			{
				return OutputGraphError((Enums.AzureADGraphAuthResults)HttpContext.Cache[userAuthResultCacheKey], userAuthResultCacheKey, loginInfo);
			}

			// if the cache here is null, we haven't retrieved the Graph user yet. retrieve it
			if (HttpContext.Cache[userCacheKey] == null)
			{
				user = await GetGraphUser(loginInfo, userCacheKey, userAuthResultCacheKey);
				if (user == null)
				{
					// resharper warns that Cache[] here might be null. won't be null since we're calling something that sets this if the return is null
					return (Enums.AzureADGraphAuthResults)HttpContext.Cache[userAuthResultCacheKey];
				}
			}
			else
			{
				user = (GraphUser)HttpContext.Cache[userCacheKey];
			}

			// if the user doesn't have an email address, try to use the UPN
			if (string.IsNullOrEmpty(user.Mail))
			{
				ADXTrace.Instance.TraceWarning(TraceCategory.Application, "Email was not set on user. Trying UPN.");

				// if the UPN isn't set, fail
				if (string.IsNullOrEmpty(user.UserPrincipalName))
				{
					return OutputGraphError(Enums.AzureADGraphAuthResults.UserHasNoEmail, userAuthResultCacheKey, loginInfo);
				}

				loginInfo.Email = user.UserPrincipalName;
			}
			else
			{
				// retrieve email
				loginInfo.Email = user.Mail;
			}

			// do license check
			foreach (var plan in user.AssignedPlans)
			{
				if (ValidEssLicenses.ContainsKey(plan.ServicePlanId.ToString()) && plan.CapabilityStatus.Equals("Enabled", StringComparison.InvariantCultureIgnoreCase))
				{
					return Enums.AzureADGraphAuthResults.NoErrors;
				}
			}

			return OutputGraphError(Enums.AzureADGraphAuthResults.NoValidLicense, userAuthResultCacheKey, loginInfo);
		}

		private async Task<GraphUser> GetGraphUser(ExternalLoginInfo loginInfo)
		{
			var userCacheKey = $"{loginInfo.Login.ProviderKey}_graphUser";
			var userAuthResultCacheKey = $"{loginInfo.Login.ProviderKey}_userAuthResult";

			// if the user's already gone through the Graph check, this will be set with the error that happened
			if (HttpContext.Cache[userAuthResultCacheKey] != null)
			{
				return null;
			}

			// if the cache here is null, we haven't retrieved the Graph user yet. retrieve it
			if (HttpContext.Cache[userCacheKey] == null)
			{
				return await GetGraphUser(loginInfo, userCacheKey, userAuthResultCacheKey);
			}

			return (GraphUser)HttpContext.Cache[userCacheKey];
		}

		private async Task<GraphUser> GetGraphUser(ExternalLoginInfo loginInfo, string userCacheKey, string userAuthResultCacheKey)
		{
			const int tokenRetryCount = 3;

			var client = this.GetGraphClient(loginInfo);

			GraphUser user = null;

			// retry tokenRetryCount times to retrieve the users. each time it fails, it will nullify the cache and try again
			for (var x = 0; x < tokenRetryCount; x++)
			{
				try
				{
					ADXTrace.Instance.TraceInfo(TraceCategory.Application, "Attempting to retrieve user from Graph with UPN ");

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

			// if user is null here, we have a config problem where we can't get correct auth tokens despite repeated attempts
			if (user == null)
			{
				OutputGraphError(Enums.AzureADGraphAuthResults.AuthConfigProblem, userAuthResultCacheKey, loginInfo);
				return null;
			}

			// add cache entry for graph user object. it will expire in GraphCacheTtlMinutes minutes
			HttpRuntime.Cache.Add(userCacheKey, user, null, DateTime.MaxValue, TimeSpan.FromMinutes(GraphCacheTtlMinutes), CacheItemPriority.Normal, null);

			return user;
		}

		private Enums.AzureADGraphAuthResults OutputGraphError(Enums.AzureADGraphAuthResults result, string userAuthResultCacheKey, ExternalLoginInfo loginInfo)
		{
			// add cache entry for graph check result. it will expire in GraphCacheTtlMinutes minutes
			HttpRuntime.Cache.Add(userAuthResultCacheKey, result, null, DateTime.MaxValue, TimeSpan.FromMinutes(GraphCacheTtlMinutes), CacheItemPriority.Normal, null);

			switch (result)
			{
				case Enums.AzureADGraphAuthResults.UserNotFound:
					ADXTrace.Instance.TraceError(TraceCategory.Application, "Azure AD didn't have the user with the specified NameIdentifier");

					return Enums.AzureADGraphAuthResults.UserNotFound;
				case Enums.AzureADGraphAuthResults.UserHasNoEmail:
					ADXTrace.Instance.TraceError(TraceCategory.Application, "UPN was not set on user.");

					return Enums.AzureADGraphAuthResults.UserHasNoEmail;
				case Enums.AzureADGraphAuthResults.NoValidLicense:
					ADXTrace.Instance.TraceError(TraceCategory.Application, "No valid license was found assigned to the user");

					return Enums.AzureADGraphAuthResults.NoValidLicense;
				case Enums.AzureADGraphAuthResults.AuthConfigProblem:
					ADXTrace.Instance.TraceError(TraceCategory.Application, "There's a critical problem with retrieving Graph auth tokens.");

					return Enums.AzureADGraphAuthResults.AuthConfigProblem;
			}

			ADXTrace.Instance.TraceError(TraceCategory.Application, "An unknown graph error occurred. Passed through UserNotFound, UserHasNoEmail, and NoValidLicense.");

			return Enums.AzureADGraphAuthResults.UnknownError;
		}

		private readonly Lazy<CrmTokenManager> tokenManager = new Lazy<CrmTokenManager>(CreateCrmTokenManager);

		private static CrmTokenManager CreateCrmTokenManager()
		{
			return new CrmTokenManager(PortalSettings.Instance.Authentication, PortalSettings.Instance.Certificate, PortalSettings.Instance.Graph.RootUrl);
		}

		private ICrmTokenManager TokenManager => this.tokenManager.Value;

		private PortalGraphClient GetGraphClient(ExternalLoginInfo loginInfo) {
			var accessCodeClaim = loginInfo.ExternalIdentity.FindFirst("AccessCode");
			var accessCode = accessCodeClaim?.Value;

			return new PortalGraphClient(
				PortalSettings.Instance.Graph.RootUrl,
				() => this.TokenManager.GetTokenAsync(accessCode));
		}

		//
		// POST: /Login/ExternalLoginConfirmation
		[HttpPost]
		[AllowAnonymous]
		[ValidateAntiForgeryToken]
		[ExternalLogin]
		public Task<ActionResult> ExternalLoginConfirmation(ExternalLoginConfirmationViewModel model, string returnUrl, string invitationCode, CancellationToken cancellationToken)
		{
			return ExternalLoginConfirmation(model.Username ?? model.Email, model.Email, model.FirstName, model.LastName, returnUrl, invitationCode, null, cancellationToken);
		}

		protected virtual async Task<ActionResult> ExternalLoginConfirmation(string username, string email, string firstName, string lastName, string returnUrl, string invitationCode, ExternalLoginInfo loginInfo, CancellationToken cancellationToken = default(CancellationToken))
		{
			loginInfo = loginInfo ?? await AuthenticationManager.GetExternalLoginInfoAsync();

			if (loginInfo == null)
			{
				ADXTrace.Instance.TraceError(TraceCategory.Application, "External Login Failure. Could not get ExternalLoginInfo.");
				
				return View("ExternalLoginFailure");
			}

			var options = await this.StartupSettingsManager.GetAuthenticationOptionsExtendedAsync(loginInfo, cancellationToken);
			var providerRegistrationEnabled = options?.RegistrationEnabled ?? false;

			if (!ViewBag.Settings.RegistrationEnabled
				|| !providerRegistrationEnabled
				|| (!ViewBag.Settings.OpenRegistrationEnabled && !ViewBag.Settings.InvitationEnabled))
			{
				AddErrors(ViewBag.IdentityErrors.InvalidLogin());

				// Registration is disabled
				ViewBag.ExternalRegistrationFailure = true;
				ADXTrace.Instance.TraceWarning(TraceCategory.Application, "User Registration Failed. Registration is not enabled.");
				return View("Login", GetLoginViewModel(null, null, returnUrl, invitationCode));
			}

			if (!ViewBag.Settings.OpenRegistrationEnabled && ViewBag.Settings.InvitationEnabled && string.IsNullOrWhiteSpace(invitationCode))
			{
				// Registration requires an invitation
				return RedirectToAction("RedeemInvitation", new { ReturnUrl = returnUrl });
			}

			if (User.Identity.IsAuthenticated)
			{
				return Redirect(returnUrl ?? "~/");
			}

			if (ModelState.IsValid)
			{
				var invitation = await FindInvitationByCodeAsync(invitationCode);
				var essUserByEmail = await FindEssUserByEmailAsync(email);
				var associatedUserByEmail = await FindAssociatedUserByEmailAsync(loginInfo, email);
				var contactId = ToContactId(essUserByEmail) ?? ToContactId(invitation) ?? ToContactId(associatedUserByEmail);

				if (ModelState.IsValid)
				{
					// Validate the username and email
					var user = contactId != null
						? new ApplicationUser { UserName = username, Email = email, FirstName = firstName, LastName = lastName, Id = contactId.Id.ToString() }
						: new ApplicationUser { UserName = username, Email = email, FirstName = firstName, LastName = lastName };

					var validateResult = await UserManager.UserValidator.ValidateAsync(user);

					if (validateResult.Succeeded)
					{
						IdentityResult result;

						if (contactId == null)
						{
							if (!ViewBag.Settings.OpenRegistrationEnabled)
							{
								throw new InvalidOperationException("Open registration is not enabled.");
							}

							// Create a new user
							result = await UserManager.CreateAsync(user);
						}
						else
						{
							// Update the existing invited user
							user = await UserManager.FindByIdAsync(contactId.Id.ToString());

							if (user != null)
							{
								result = await UserManager.InitializeUserAsync(user, username, null, !string.IsNullOrWhiteSpace(email) ? email : contactId.Name, ViewBag.Settings.TriggerLockoutOnFailedPassword);
							}
							else
							{
								// Contact does not exist or login is disabled

								if (!ViewBag.Settings.OpenRegistrationEnabled)
								{
									throw new InvalidOperationException("Open registration is not enabled.");
								}

								// Create a new user
								result = await UserManager.CreateAsync(user);
							}

							if (!result.Succeeded)
							{
								AddErrors(result);

								ViewBag.ReturnUrl = returnUrl;
								ViewBag.InvitationCode = invitationCode;

								return View("RedeemInvitation", new RedeemInvitationViewModel { InvitationCode = invitationCode });
							}
						}

						if (result.Succeeded)
						{
							var addResult = await UserManager.AddLoginAsync(user.Id, loginInfo.Login);

							// Treat email as confirmed on external login success
							var confirmEmailResult = await UserManager.AutoConfirmEmailAsync(user.Id);

							if (!confirmEmailResult.Succeeded)
							{
								AddErrors(confirmEmailResult);
							}

							if (addResult.Succeeded)
							{
								// AddLoginAsync added related entity adx_externalidentity and we must re-retrieve the user so the related collection ids are loaded, otherwise the adx_externalidentity will be deleted on subsequent user updates.
								user = await this.UserManager.FindByIdAsync(user.Id);

								var claimsMapping = options?.RegistrationClaimsMapping;

								if (!string.IsNullOrWhiteSpace(claimsMapping))
								{
									ApplyClaimsMapping(user, loginInfo, claimsMapping);
								}

								if (invitation != null)
								{
									var redeemResult = await InvitationManager.RedeemAsync(invitation, user, Request.UserHostAddress);

									if (redeemResult.Succeeded)
									{
										return await SignInAsync(user, returnUrl, loginInfo);
									}
									else
									{
										AddErrors(redeemResult);
									}
								}
								else
								{
									return await SignInAsync(user, returnUrl, loginInfo);
								}
							}
							else
							{
								AddErrors(addResult);
							}
						}
						else
						{
							AddErrors(result);
						}
					}
					else
					{
						AddErrors(validateResult);
					}
				}
			}

			ViewBag.ReturnUrl = returnUrl;
			ViewBag.InvitationCode = invitationCode;
			return View("ExternalLoginConfirmation", new ExternalLoginConfirmationViewModel { Email = email, FirstName = firstName, LastName = lastName, Username = username });
		}

		//
		// GET: /Login/LogOff
		[HttpGet]
		public async Task<ActionResult> LogOff(string returnUrl, CancellationToken cancellationToken)
		{
			if (ViewBag.Settings.SignOutEverywhereEnabled)
			{
				UserManager.UpdateSecurityStamp(User.Identity.GetUserId());
			}

			var authenticationTypes = ViewBag.IsESS
				? GetSignOutAuthenticationTypes(StartupSettingsManager.AzureAdOptions.AuthenticationType)
				: await this.GetSignOutAuthenticationTypes(cancellationToken);

			if (authenticationTypes != null)
			{
				AuthenticationManager.SignOut(authenticationTypes);
			}
			else
			{
				AuthenticationManager.SignOut(new AuthenticationProperties { RedirectUri = returnUrl });
			}

			AuthenticationManager.AuthenticationResponseGrant = null;

			try
			{
				if (FeatureCheckHelper.IsFeatureEnabled(FeatureNames.TelemetryFeatureUsage))
				{
					PortalFeatureTrace.TraceInstance.LogAuthentication(FeatureTraceCategory.Authentication, this.HttpContext, "logOut", "authentication");
				}
			}
			catch (Exception e)
			{
				WebEventSource.Log.GenericErrorException(e);
			}

			return Redirect(!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) ? returnUrl : "~/");
		}

		private static string[] GetSignOutAuthenticationTypes(params string[] authenticationTypes)
		{
			var defaultAuthenticationTypes = new[]
			{
				DefaultAuthenticationTypes.ApplicationCookie,
				DefaultAuthenticationTypes.ExternalCookie,
				DefaultAuthenticationTypes.TwoFactorCookie,
			};
			
			return defaultAuthenticationTypes.Concat(authenticationTypes).ToArray();
		}

		private async Task<string[]> GetSignOutAuthenticationTypes(CancellationToken cancellationToken)
		{
			var provider =
				((ClaimsIdentity)User.Identity).Claims
				.FirstOrDefault(c => c.Type == "http://schemas.adxstudio.com/xrm/2014/02/identity/claims/loginprovider")
				?.Value;

			if (provider != null)
			{
				var options = await this.StartupSettingsManager.GetAuthenticationOptionsExtendedAsync(provider, cancellationToken);

				if (options != null && options.ExternalLogoutEnabled)
				{
					return GetSignOutAuthenticationTypes(options.AuthenticationType);
				}
			}

			return null;
		}

		//
		// GET: /Login/ExternalLoginFailure
		[HttpGet]
		[AllowAnonymous]
		[ExternalLogin]
		public ActionResult ExternalLoginFailure()
		{
			return View();
		}

		// OWINAuthenticationFailedAccessDeniedMsg = "access_denied";
		private const string OwinAuthenticationFailedAccessDeniedMsg = "access_denied";
		private const string MessageQueryStringParameter = "message";

		//
		// GET: /Login/ExternalAuthenticationFailed
		[HttpGet]
		[AllowAnonymous]
		[ExternalLogin]
		public ActionResult ExternalAuthenticationFailed()
		{
			if ((this.Request?.QueryString.GetValues(MessageQueryStringParameter) ?? new string[] { })
				.Any(m => m == OwinAuthenticationFailedAccessDeniedMsg))
			{
				ViewBag.AccessDeniedError = true;
			}
			
			// Because AuthenticationFailedNotification doesn't pass an initial returnUrl from which user might tried to SignIn,
			// and when having redidected to this view makes SignIn link become "SignIn?returnUrl=/Account/Login/ExternalAuthenticationFailed?message=access_denied"
			// which makes an infinite redirect loop - just set returnUrl to root
			ViewBag.ReturnUrl = "/";

			return View();
		}

		//
		// GET: /Login/RedeemInvitation
		[HttpGet]
		[AllowAnonymous]
		[LanguageActionFilter]
		public ActionResult RedeemInvitation(string returnUrl, [Bind(Prefix="invitation")]string invitationCode = "", bool invalid = false)
		{
			if (!ViewBag.Settings.RegistrationEnabled || !ViewBag.Settings.InvitationEnabled)
			{
				return HttpNotFound();
			}

			if (invalid)
			{
				ModelState.AddModelError("InvitationCode", ViewBag.IdentityErrors.InvalidInvitationCode().Description);
			}

			ViewBag.ReturnUrl = returnUrl;

			// Decoding invitation code
			invitationCode = HttpUtility.HtmlDecode(invitationCode).Trim();

			ViewBag.InvitationCode = invitationCode;

			return View(new RedeemInvitationViewModel { InvitationCode = invitationCode });
		}

		//
		// POST: /Login/RedeemInvitation
		[HttpPost]
		[AllowAnonymous]
		[ValidateAntiForgeryToken]
		public async Task<ActionResult> RedeemInvitation(RedeemInvitationViewModel model, string returnUrl)
		{
			if (!ViewBag.Settings.RegistrationEnabled || !ViewBag.Settings.InvitationEnabled)
			{
				return HttpNotFound();
			}

			if (ModelState.IsValid)
			{
				var applicationInvitation = await FindInvitationByCodeAsync(model.InvitationCode);

				var contactId = ToContactId(applicationInvitation);

				ViewBag.ReturnUrl = returnUrl;
				ViewBag.InvitationCode = model.InvitationCode;

				if (contactId != null || applicationInvitation != null)
				{
					if (!string.IsNullOrWhiteSpace(ViewBag.Settings.LoginButtonAuthenticationType))
					{
						return RedirectToAction("ExternalLogin", "Login", new {
							returnUrl,
							area = "Account",
							invitationCode = model.InvitationCode,
							provider = ViewBag.Settings.LoginButtonAuthenticationType
						});
					}

					if (model.RedeemByLogin)
					{
						return View("Login", GetLoginViewModel(null, null, returnUrl, model.InvitationCode));
					}

					return Redirect(Site.Helpers.UrlHelpers.SecureRegistrationUrl(this.Url, returnUrl, model.InvitationCode));
				}

				ModelState.AddModelError("InvitationCode", ViewBag.IdentityErrors.InvalidInvitationCode().Description);
			}

			ViewBag.ReturnUrl = returnUrl;
			return View(model);
		}

		[HttpGet]
		[AllowAnonymous]
		[ExternalLogin]
		public Task<ActionResult> FacebookExternalLogin()
		{
			Response.SuppressFormsAuthenticationRedirect = true;

			var website = HttpContext.GetWebsite();
			var authenticationType = website.GetFacebookAuthenticationType();

			var returnUrl = Url.Action("FacebookReloadParent", "Login");
			var redirectUri = Url.Action("ExternalLoginCallback", "Login", new { ReturnUrl = returnUrl });
			var result = new ChallengeResult(authenticationType.LoginProvider, redirectUri);

			return Task.FromResult(result as ActionResult);
		}

		[HttpGet]
		[AllowAnonymous]
		[ExternalLogin]
		public ActionResult FacebookReloadParent()
		{
			return View("LoginReloadParent");
		}

		[HttpPost]
		[AllowAnonymous]
		[ExternalLogin, SuppressMessage("ASP.NET.MVC.Security", "CA5332:MarkVerbHandlersWithValidateAntiforgeryToken", Justification = "External caller cannot provide anti-forgery token, signed_request value is validated.")]
		public async Task<ActionResult> FacebookExternalLoginCallback(string signed_request, string returnUrl, CancellationToken cancellationToken)
		{
			if (string.IsNullOrWhiteSpace(signed_request))
			{
				return HttpNotFound();
			}

			var website = HttpContext.GetWebsite();
			var loginInfo = website.GetFacebookLoginInfo(signed_request);

			if (loginInfo == null)
			{
				return RedirectToLocal(returnUrl);
			}

			// Sign in the user with this external login provider if the user already has a login
			var result = await SignInManager.ExternalSignInAsync(loginInfo, false, (bool)ViewBag.Settings.TriggerLockoutOnFailedPassword);

			switch (result)
			{
				case SignInStatus.Success:
				case SignInStatus.LockedOut:
					return RedirectToLocal(returnUrl);
				case SignInStatus.RequiresVerification:
					return RedirectToAction("SendCode", new { ReturnUrl = returnUrl });
				case SignInStatus.Failure:
				default:
					// If the user does not have an account, then prompt the user to create an account
					ViewBag.ReturnUrl = returnUrl;
					var email = loginInfo.Email;
					var username = loginInfo.Login.ProviderKey;
					var firstName = loginInfo.ExternalIdentity.FindFirstValue(System.Security.Claims.ClaimTypes.GivenName);
					var lastName = loginInfo.ExternalIdentity.FindFirstValue(System.Security.Claims.ClaimTypes.Surname);

					return await ExternalLoginConfirmation(username, email, firstName, lastName, returnUrl, null, loginInfo, cancellationToken);
			}
		}

		#region Helpers
		// Used for XSRF protection when adding external logins
		private const string XsrfKey = "XsrfId";

		private IAuthenticationManager AuthenticationManager
		{
			get
			{
				return HttpContext.GetOwinContext().Authentication;
			}
		}

		private void AddErrors(IdentityError error)
		{
			AddErrors(IdentityResult.Failed(error.Description));
		}

		private void AddErrors(IdentityResult result)
		{
			foreach (var error in result.Errors)
			{
				ModelState.AddModelError(string.Empty, error);
			}
		}

		private async Task<ActionResult> RedirectOnPostAuthenticate(string returnUrl, string invitationCode, ExternalLoginInfo loginInfo = null, CancellationToken cancellationToken = default(CancellationToken))
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

		private async Task<ActionResult> RedirectOnPostAuthenticate(ApplicationUser user, string returnUrl, string invitationCode, ExternalLoginInfo loginInfo = null, CancellationToken cancellationToken = default(CancellationToken))
		{
			if (user != null)
			{
				this.UpdateCurrentLanguage(user, ref returnUrl);
				this.UpdateLastSuccessfulLogin(user);
				await this.ApplyGraphUser(user, loginInfo, cancellationToken);

				if (user.IsDirty)
				{
					await UserManager.UpdateAsync(user);
					user.IsDirty = false;
				}

				IdentityResult redeemResult;
				var invitation = await FindInvitationByCodeAsync(invitationCode);

				if (invitation != null)
				{
					// Redeem invitation for the existing/registered contact
					redeemResult = await InvitationManager.RedeemAsync(invitation, user, Request.UserHostAddress);
				}
				else if (!string.IsNullOrWhiteSpace(invitationCode))
				{
					redeemResult = IdentityResult.Failed(ViewBag.IdentityErrors.InvalidInvitationCode().Description);
				}
				else
				{
					redeemResult = IdentityResult.Success;
				}

				if (!redeemResult.Succeeded)
				{
					return RedirectToAction("RedeemInvitation", new { ReturnUrl = returnUrl, invitation = invitationCode, invalid = true });
				}

				if (!DisplayModeIsActive() 
					&& (user.HasProfileAlert || user.ProfileModifiedOn == null)
					&& ViewBag.Settings.ProfileRedirectEnabled)
				{
					return RedirectToProfile(returnUrl);
				}
			}

			return RedirectToLocal(returnUrl);
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
				var preferredLanguage = user.Entity.GetAttributeValue<EntityReference>("adx_preferredlanguageid");
				if (preferredLanguage != null)
				{
					var websiteLangauges = languageContext.ActiveWebsiteLanguages.ToArray();
					// Only consider published website languages for users
					var newLanguage = languageContext.GetWebsiteLanguageByPortalLanguageId(preferredLanguage.Id, websiteLangauges, true);
					if (newLanguage != null)
					{
						if (ContextLanguageInfo.DisplayLanguageCodeInUrl)
						{
							returnUrl = languageContext.FormatUrlWithLanguage(false, newLanguage.Code, (returnUrl ?? string.Empty).AsAbsoluteUri(Request.Url));
						}
					}
				}
			}
		}

		/// <summary>
		/// Updates date and time of last successful login
		/// </summary>
		/// <param name="user">Application User that is currently being logged in.</param>
		private void UpdateLastSuccessfulLogin(ApplicationUser user)
		{
			if (!ViewBag.Settings.LoginTrackingEnabled)
			{
				return;
			}

			user.Entity.SetAttributeValue("adx_identity_lastsuccessfullogin", DateTime.UtcNow);
			user.IsDirty = true;
		}

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

						if (pieces.Length == 2 && claimValue != null)
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
						this.ViewBag.ErrorMessage = string.Format(ResourceManager.GetString("Generic_Error_Message"), guid);
					}
				}
			}
		}

		private bool DisplayModeIsActive()
		{
			return DisplayModeProvider.Instance
				.GetAvailableDisplayModesForContext(HttpContext, null)
				.OfType<HostNameSettingDisplayMode>()
				.Any();
		}

		private static ActionResult RedirectToProfile(string returnUrl)
		{
			var query = !string.IsNullOrWhiteSpace(returnUrl) ? new NameValueCollection { { "ReturnUrl", returnUrl } } : null;

			return new RedirectToSiteMarkerResult("Profile", query);
		}

		private ActionResult RedirectToLocal(string returnUrl)
		{
			if (Url.IsLocalUrl(returnUrl))
			{
				return Redirect(returnUrl);
			}
			return Redirect("~/");
		}

		internal class ChallengeResult : HttpUnauthorizedResult
		{
			public ChallengeResult(string provider, string redirectUri)
				: this(provider, redirectUri, null)
			{
			}

			public ChallengeResult(string provider, string redirectUri, string userId)
			{
				LoginProvider = provider;
				RedirectUri = redirectUri;
				UserId = userId;
			}

			public string LoginProvider { get; set; }
			public string RedirectUri { get; set; }
			public string UserId { get; set; }

			public override void ExecuteResult(ControllerContext context)
			{
				var properties = new AuthenticationProperties { RedirectUri = RedirectUri };
				if (UserId != null)
				{
					properties.Dictionary[XsrfKey] = UserId;
				}
				context.HttpContext.GetOwinContext().Authentication.Challenge(properties, LoginProvider);
			}
		}

		private object GetLoginViewModel(LoginViewModel local, IEnumerable<AuthenticationDescription> external, string returnUrl, string invitationCode)
		{
			ViewData["local"] = local ?? new LoginViewModel();
			ViewData["external"] = external ?? GetExternalAuthenticationTypes();

			ViewBag.ReturnUrl = returnUrl;
			ViewBag.InvitationCode = invitationCode;

			return ViewData;
		}

		private IEnumerable<AuthenticationDescription> GetExternalAuthenticationTypes()
		{
			var authTypes = this.HttpContext.GetOwinContext().Authentication
				.GetExternalAuthenticationTypes()
				.OrderBy(type => type.AuthenticationType)
				.ToList();
			
			foreach (var authType in authTypes)
			{
				if (!authType.Properties.ContainsKey("RegistrationEnabled"))
				{
					var options = this.StartupSettingsManager.GetAuthenticationOptionsExtended(authType.AuthenticationType);
					authType.Properties["RegistrationEnabled"] = options.RegistrationEnabled;
				}
			}

			return authTypes;
		}

		private async Task<ApplicationInvitation> FindInvitationByCodeAsync(string invitationCode)
		{
			if (string.IsNullOrWhiteSpace(invitationCode)) return null;
			if (!ViewBag.Settings.InvitationEnabled) return null;

			return await InvitationManager.FindByCodeAsync(invitationCode);
		}

		private async Task<ApplicationUser> FindEssUserByEmailAsync(string email)
		{
			return ViewBag.IsEss
				? await UserManager.FindByEmailAsync(email)
				: null;
		}

		/// <summary>
		/// If AllowContactMappingWithEmail property on the appropriate authentication options class for the provider
		/// is true - finds a single unique contact where emailaddress1 equals email on the ExternalLoginInfo
		/// </summary>
		/// <param name="loginInfo">ExternalLoginIfo to find if AllowContactMappingWithEmail is true</param>
		/// <param name="email">Email to match againts emailaddress1 from portal contacts</param>
		/// <returns>ApplicationUser with unique email same as in external login, otherwise null.</returns>
		private async Task<ApplicationUser> FindAssociatedUserByEmailAsync(ExternalLoginInfo loginInfo, string email)
		{
			return GetLoginProviderSetting<bool>(loginInfo, "AllowContactMappingWithEmail", false)
				? await UserManager.FindByEmailAsync(email)
				: null;
		}

		private static EntityReference ToContactId(ApplicationInvitation invitation)
		{
			return invitation != null && invitation.InvitedContact != null
				? new EntityReference(invitation.InvitedContact.LogicalName, invitation.InvitedContact.Id) { Name = invitation.Email }
				: null;
		}

		private static EntityReference ToContactId(ApplicationUser user)
		{
			return user != null
				? user.ContactId
				: null;
		}

		private async Task<ActionResult> SignInAsync(ApplicationUser user, string returnUrl, ExternalLoginInfo loginInfo = null, bool isPersistent = false, bool rememberBrowser = false)
		{
			await this.SignInManager.SignInAsync(user, isPersistent, rememberBrowser);
			return await this.RedirectOnPostAuthenticate(user, returnUrl, null, loginInfo);
		}

		/// <summary>
		/// Finds setting from the appropriate authentication options class of LoginProvider
		/// </summary>
		/// <typeparam name="T">Type of setting</typeparam>
		/// <param name="loginInfo">Defines LoginProvider</param>
		/// <param name="name">Name of a property in authentication options class</param>
		/// <param name="defaultValue">Will be returned on any exception</param>
		/// <returns>Value if exists. Otherwise - defaultValue</returns>
		private T GetLoginProviderSetting<T>(ExternalLoginInfo loginInfo, string name, T defaultValue)
		{
			if (loginInfo == null)
			{
				return defaultValue;
			}

			T result = defaultValue;
			string loginProvider = loginInfo.Login.LoginProvider;

			try
			{
				// Getting the correct provider options
				switch (loginProvider)
				{
					// OAuth
					case "Twitter":
						result = (T)StartupSettingsManager.Twitter?.GetType()
							.GetProperty(name)?.GetValue(StartupSettingsManager.Twitter);
						break;
					case "Google":
						result = (T)StartupSettingsManager.Google?.GetType()
							.GetProperty(name)?.GetValue(StartupSettingsManager.Google);
						break;
					case "Facebook":
						result = (T)StartupSettingsManager.Facebook?.GetType()
							.GetProperty(name)?.GetValue(StartupSettingsManager.Facebook);
						break;
					case "LinkedIn":
						result = (T)StartupSettingsManager.LinkedIn?.GetType()
							.GetProperty(name)?.GetValue(StartupSettingsManager.LinkedIn);
						break;
					case "Yahoo":
						result = (T)StartupSettingsManager.Yahoo?.GetType()
							.GetProperty(name)?.GetValue(StartupSettingsManager.Yahoo);
						break;
					case "MicrosoftAccount":
						result = (T)StartupSettingsManager.MicrosoftAccount?.GetType()
							.GetProperty(name)?.GetValue(StartupSettingsManager.MicrosoftAccount);
						break;

					default:
						// OpenIdConnect - checking Authority
						var options = StartupSettingsManager.OpenIdConnectOptions?.FirstOrDefault(o => 
							string.Equals(o.Authority, loginProvider, StringComparison.InvariantCultureIgnoreCase));
						if (options != null)
						{
							result = (T)options.GetType().GetProperty(name)?.GetValue(options);
						}
						else
						{
							// wsFed - checking Caption
							var wsFedOptions = StartupSettingsManager.WsFederationOptions?.FirstOrDefault(o =>
								string.Equals(o.Caption, loginProvider, StringComparison.InvariantCultureIgnoreCase));
							if (wsFedOptions != null)
							{
								result = (T)wsFedOptions.GetType().GetProperty(name)?.GetValue(wsFedOptions);
							}
							else
							{
								// saml2 - checking Caption
								var saml2options = StartupSettingsManager.Saml2Options?.FirstOrDefault(o =>
									string.Equals(o.Caption, loginProvider, StringComparison.InvariantCultureIgnoreCase));
								if (saml2options != null)
								{
									result = (T)saml2options.GetType().GetProperty(name)?.GetValue(saml2options);
								}
							}
						}
						break;
				}
			}
			catch (Exception)
			{
				return defaultValue;
			}

			return result;
		}

		#endregion
	}
}
