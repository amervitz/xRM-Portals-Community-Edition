/*
  Copyright (c) Microsoft Corporation. All rights reserved.
  Licensed under the MIT License. See License.txt in the project root for license information.
*/

using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Web;
using System.Web.UI.HtmlControls;
using System.Security.Claims;
using System.IdentityModel.Tokens;
using System.IdentityModel.Services;
using Microsoft.Xrm.Client;
using Microsoft.Xrm.Client.Collections.Generic;
using Microsoft.Xrm.Client.Diagnostics;
using Microsoft.Xrm.Portal.IdentityModel.Configuration;
using Microsoft.Xrm.Portal.IdentityModel.Web.Handlers;
using Microsoft.Xrm.Portal.Web;

namespace Microsoft.Xrm.Portal.IdentityModel.Web.Modules
{
	public static class WSFederationAuthenticationModuleExtensions
	{
		public const string DefaultReturnUrlKey = "returnurl";
		public const string DefaultInvitationCodeKey = "invitation";
		public const string DefaultChallengeAnswerKey = "answer";
		public const string DefaultEmailClaimType = ClaimTypes.Email;
		public const string DefaultDisplayNameClaimType = ClaimTypes.Name;
		public const string DefaultIdentityProviderClaimType = "http://schemas.microsoft.com/accesscontrolservice/2010/07/claims/identityprovider";

		public static ClaimsPrincipal GetClaimsPrincipal(this WSFederationAuthenticationModule fam, HttpContext context)
		{
			var token = fam.GetSecurityToken(new HttpRequestWrapper(context.Request));
			var identities = fam.FederationConfiguration.IdentityConfiguration.SecurityTokenHandlers.ValidateToken(token);
			var principal = new ClaimsPrincipal(identities);

			return principal;
		}

		public static SessionSecurityToken GetSessionSecurityToken(
			this WSFederationAuthenticationModule fam,
			HttpContext context,
			out string identityProvider,
			out string userName,
			out string email,
			out string displayName,
			string emailClaimType = DefaultEmailClaimType,
			string displayNameClaimType = DefaultDisplayNameClaimType,
			string identityProviderClaimType = DefaultIdentityProviderClaimType)
		{
			var principal = fam.GetClaimsPrincipal(context);
			var sessionSecurityToken = new SessionSecurityToken(principal);
			userName = principal.Identity.Name;

			Func<string, string> find = claimType => !string.IsNullOrWhiteSpace(claimType)
				? principal.Identities
					.SelectMany(identity => identity.Claims)
					.Where(claim => string.Equals(claim.Type, claimType, StringComparison.OrdinalIgnoreCase))
					.Select(claim => claim.Value)
					.FirstOrDefault()
				: null;

			identityProvider = find(identityProviderClaimType);
			email = find(emailClaimType);
			displayName = find(displayNameClaimType);

			return sessionSecurityToken;
		}

		public static IDictionary<string, string> GetSignInResponseMessageContext(
			this WSFederationAuthenticationModule fam,
			HttpContext context)
		{
			var message = fam.GetSignInResponseMessage(new HttpRequestWrapper(context.Request));
			var ctx = message.Context.ToDictionary();

			return ctx;
		}

		public static string GetSignInRequestUrl(
			this WSFederationAuthenticationModule fam,
			string returnUrl,
			string returnUrlKey = DefaultReturnUrlKey)
		{
			var ctx = new Dictionary<string, string>
			{
				{ returnUrlKey, returnUrl },
			};

			var requestUrl = GetSignInRequestUrl(fam, ctx);

			return requestUrl;
		}

		public static string GetSignInRequestUrl(this WSFederationAuthenticationModule fam, IDictionary<string, string> context = null)
		{
			var ctx = context != null ? ToConnectionString(context) : null;

			var baseUrl = new Uri(fam.Issuer);
			var signIn = new SignInRequestMessage(baseUrl, fam.Realm, fam.Reply) { Context = ctx };
			var requestUrl = signIn.RequestUrl;

			return requestUrl;
		}

		public static string GetHomeRealmDiscoveryMetadataFeedUrl(
			this WSFederationAuthenticationModule fam,
			string path = "/v2/metadata/IdentityProviders.js",
			string protocol = "wsfederation",
			string version = "1.0",
			string replyTo = null,
			string callback = null,
			IDictionary<string, string> context = null)
		{
			var ctx = context != null ? ToConnectionString(context) : null;

			var query = "?protocol={0}&realm={1}&reply_to={2}&context={3}&version={4}&callback={5}".FormatWith(
				HttpUtility.UrlEncode(protocol),
				HttpUtility.UrlEncode(fam.Realm),
				HttpUtility.UrlEncode(replyTo),
				HttpUtility.UrlEncode(ctx),
				HttpUtility.UrlEncode(version),
				HttpUtility.UrlEncode(callback));

			var baseUri = new Uri(fam.Issuer);
			var url = baseUri.GetLeftPart(UriPartial.Authority) + path + query;

			return url;
		}

		public static void RedirectToSignIn(
			this WSFederationAuthenticationModule fam,
			HttpContext context,
			string returnUrl,
			string invitationCodeValue,
			string challengeAnswerValue,
			IUserRegistrationSettings registrationSettings)
		{
			RedirectToSignIn(
				fam,
				context,
				returnUrl,
				invitationCodeValue,
				challengeAnswerValue,
				registrationSettings.ReturnUrlKey,
				registrationSettings.InvitationCodeKey,
				registrationSettings.ChallengeAnswerKey);
		}

		public static void RedirectToSignIn(
			this WSFederationAuthenticationModule fam,
			HttpContext context,
			string returnUrl,
			string invitationCodeValue,
			string challengeAnswerValue,
			string returnUrlKey = DefaultReturnUrlKey,
			string invitationCodeKey = DefaultInvitationCodeKey,
			string challengeAnswerKey = DefaultChallengeAnswerKey)
		{
			var ctx = new Dictionary<string, string>
			{
				{ returnUrlKey ?? DefaultReturnUrlKey, returnUrl },
				{ invitationCodeKey ?? DefaultInvitationCodeKey, invitationCodeValue },
				{ challengeAnswerKey ?? DefaultChallengeAnswerKey, challengeAnswerValue },
			};

			RedirectToSignIn(fam, context, ctx);
		}

		public static void RedirectToSignIn(
			this WSFederationAuthenticationModule fam,
			HttpContext context,
			IDictionary<string, string> signInContext = null)
		{
			var ctx = signInContext != null ? ToConnectionString(signInContext) : null;

			var baseUrl = new Uri(fam.Issuer);
			var signIn = new SignInRequestMessage(baseUrl, fam.Realm) { Context = ctx };
			var url = signIn.RequestUrl;

			TraceInformation("RedirectToSignIn", "url={0}", url);

			context.RedirectAndEndResponse(url);
		}

		public static void SetSessionSecurityTokenAndRedirect(
			this WSFederationAuthenticationModule fam,
			HttpContext context,
			SessionSecurityToken token,
			string url)
		{
			fam.SetPrincipalAndWriteSessionToken(token, true);

			TraceInformation("SetSessionSecurityTokenAndRedirect", "url={0}", url);

			context.RedirectAndEndResponse(url);
		}

		public static string GetSignInResponseMessageAsFormPost(
			this WSFederationAuthenticationModule fam,
			HttpContext context,
			Uri uri)
		{
			var message = fam.GetSignInResponseMessage(new HttpRequestWrapper(context.Request));
			message.BaseUri = uri;

			// clean out the non-WIF parameters

			var parametersToRemove = GetUnknownParameters(message).Select(parameter => parameter.Key).ToList();

			foreach (var parameter in parametersToRemove)
			{
				message.RemoveParameter(parameter);
			}

			var post = message.WriteFormPost();

			return post;
		}

		public static void WriteSignInResponseMessageAsFormPost(
			this WSFederationAuthenticationModule fam,
			HttpContext context,
			Uri uri)
		{
			TraceInformation("WriteSignInResponseMessageAsFormPost", "Begin");

			var post = GetSignInResponseMessageAsFormPost(fam, context, uri);

			context.Response.Write(post);

			TraceInformation("WriteSignInResponseMessageAsFormPost", "End");
		}

		public static bool TryHandleSignInResponse(
			this WSFederationAuthenticationModule fam,
			HttpContext context,
			IDictionary<string, string> signInContext,
			IUserRegistrationSettings registrationSettings = null)
		{
			var handler = registrationSettings != null
				? new FederationAuthenticationHandler(registrationSettings)
				: new FederationAuthenticationHandler();

			try
			{
				return handler.TryHandleSignInResponse(context, fam, signInContext);
			}
			catch (Exception exception)
			{
				if (!handler.TryHandleException(context, fam, exception))
				{
					throw new FederationAuthenticationException("Federated sign-in error.", exception);
				}
			}

			return false;
		}

		public static bool TryHandleSignInResponse(
			this WSFederationAuthenticationModule fam,
			HttpContext context,
			string returnUrl,
			string invitationCode,
			string challengeAnswer,
			IUserRegistrationSettings registrationSettings)
		{
			var signInContext = new Dictionary<string, string>
			{
				{ registrationSettings.ReturnUrlKey ?? DefaultReturnUrlKey, returnUrl },
				{ registrationSettings.InvitationCodeKey ?? DefaultInvitationCodeKey, invitationCode },
				{ registrationSettings.ChallengeAnswerKey ?? DefaultChallengeAnswerKey, challengeAnswer },
			};

			return TryHandleSignInResponse(fam, context, signInContext, registrationSettings);
		}

		public static void AddSignInResponseParametersToForm(this WSFederationAuthenticationModule fam, HttpContext context, HtmlForm form)
		{
			var message = fam.GetSignInResponseMessage(new HttpRequestWrapper(context.Request));

			foreach (var parameter in message.GetParameters())
			{
				var input = new HtmlGenericControl("input");
				input.Attributes["type"] = "hidden";
				input.Attributes["name"] = parameter.Key;
				input.Attributes["value"] = parameter.Value;

				TraceInformation("AddSignInResponseParametersToForm", "parameter.Key={0}, parameter.Value={1}", parameter.Key, parameter.Value);

				form.Controls.Add(input);
			}
		}

		public static IEnumerable<KeyValuePair<string, string>> GetParameters(this WSFederationMessage message)
		{
			return GetParameters(message, key => _federationParameters.Contains(key));
		}

		public static IEnumerable<KeyValuePair<string, string>> GetUnknownParameters(this WSFederationMessage message)
		{
			return GetParameters(message, key => !_federationParameters.Contains(key));
		}

		private static readonly IEnumerable<string> _federationParameters = new[]
		{
			"wa",
			"wattr",
			"wattrptr",
			"wauth",
			"wctx",
			"wct",
			"wencoding",
			"wfed",
			"wfresh",
			"whr",
			"wp",
			"wpseudo",
			"wpseudoptr",
			"wtrealm",
			"wreply",
			"wreq",
			"wreqptr",
			"wres",
			"wresult",
			"wresultptr"
		};

		private static IEnumerable<KeyValuePair<string, string>> GetParameters(
			this WSFederationMessage message,
			Func<string, bool> compare)
		{
			return message.Parameters.Where(parameter => compare(parameter.Key));
		}

		private static string ToConnectionString(IEnumerable<KeyValuePair<string, string>> dictionary)
		{
			var filtered = dictionary.Where(item => !string.IsNullOrWhiteSpace(item.Value));

			var builder = new DbConnectionStringBuilder();

			foreach (var item in filtered)
			{
				builder.Add(item.Key, item.Value);
			}

			return builder.ToString();
		}

		private static void TraceInformation(string memberName, string format, params object[] args)
		{
			Tracing.FrameworkInformation(typeof(WSFederationAuthenticationModuleExtensions).Name, memberName, format, args);
		}
	}
}
