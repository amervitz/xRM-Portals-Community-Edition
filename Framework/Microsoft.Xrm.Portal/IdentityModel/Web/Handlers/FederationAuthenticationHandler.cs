/*
  Copyright (c) Microsoft Corporation. All rights reserved.
  Licensed under the MIT License. See License.txt in the project root for license information.
*/

using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Web;
using System.IdentityModel.Tokens;
using System.IdentityModel.Services;
using Microsoft.Xrm.Client;
using Microsoft.Xrm.Client.Collections.Generic;
using Microsoft.Xrm.Client.Diagnostics;
using Microsoft.Xrm.Portal.Configuration;
using Microsoft.Xrm.Portal.IdentityModel.Configuration;
using Microsoft.Xrm.Portal.IdentityModel.Web.Modules;
using Microsoft.Xrm.Portal.Web;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Client;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;

namespace Microsoft.Xrm.Portal.IdentityModel.Web.Handlers
{
	public class FederationAuthenticationHandler : IHttpHandler
	{
		#region Constructors

		public FederationAuthenticationHandler()
			: this(FederationCrmConfigurationManager.GetUserRegistrationSettings())
		{
		}

		public FederationAuthenticationHandler(IUserRegistrationSettings registrationSettings)
			: this(registrationSettings, GetUserSettings(registrationSettings))
		{
		}

		public FederationAuthenticationHandler(IUserRegistrationSettings registrationSettings, IUserResolutionSettings userSettings)
		{
			RegistrationSettings = registrationSettings;
			UserSettings = userSettings;
		}

		private static IUserResolutionSettings GetUserSettings(IUserRegistrationSettings registrationSettings)
		{
			var userSettings = FederationCrmConfigurationManager.GetUserResolutionSettings(registrationSettings.PortalName);

			return userSettings;
		}

		#endregion

		#region Properties

		private const string _invalidInvitationCode = "invalid-invitation-code";
		private const string _confirm = "confirm";
		private const string _inactive = "inactive";

		public virtual IUserRegistrationSettings RegistrationSettings { get; private set; }

		public virtual IUserResolutionSettings UserSettings { get; private set; }

		protected string MemberEntityName
		{
			get { return SelectUserSetting(setting => setting.MemberEntityName, "contact"); }
		}

		protected string AttributeMapUsername
		{
			get { return SelectUserSetting(setting => setting.AttributeMapUsername, "adx_username"); }
		}

		protected string ReturnUrlKey
		{
			get { return SelectRegistrationSetting(setting => setting.ReturnUrlKey, "returnurl"); }
		}

		protected string InvitationCodeKey
		{
			get { return SelectRegistrationSetting(setting => setting.InvitationCodeKey, "invitation"); }
		}

		protected string ChallengeAnswerKey
		{
			get { return SelectRegistrationSetting(setting => setting.ChallengeAnswerKey, "answer"); }
		}

		protected string ResultCodeKey
		{
			get { return SelectRegistrationSetting(setting => setting.ResultCodeKey, "result-code"); }
		}

		protected string DefaultReturnPath
		{
			get { return SelectRegistrationSetting(setting => setting.DefaultReturnPath, "~/"); }
		}

		protected string ProfilePath
		{
			get { return SelectRegistrationSetting(setting => setting.ProfilePath, DefaultReturnPath); }
		}

		protected string ErrorPath
		{
			get { return SelectRegistrationSetting(setting => setting.ErrorPath, DefaultReturnPath); }
		}

		protected string AttributeMapLogonEnabled
		{
			get { return SelectRegistrationSetting(setting => setting.AttributeMapLogonEnabled, "adx_logonenabled"); }
		}

		protected string AttributeMapInvitationCode
		{
			get { return SelectRegistrationSetting(setting => setting.AttributeMapInvitationCode, "adx_invitationcode"); }
		}

		protected string AttributeMapInvitationCodeExpiryDate
		{
			get { return SelectRegistrationSetting(setting => setting.AttributeMapInvitationCodeExpiryDate, "adx_invitationcodeexpirydate"); }
		}

		protected string AttributeMapChallengeAnswer
		{
			get { return SelectRegistrationSetting(setting => setting.AttributeMapChallengeAnswer, "adx_passwordanswer"); }
		}

		protected string AttributeMapEmail
		{
			get { return SelectRegistrationSetting(setting => setting.AttributeMapEmail, "emailaddress1"); }
		}

		protected string AttributeMapDisplayName
		{
			get { return SelectRegistrationSetting(setting => setting.AttributeMapDisplayName, "lastname"); }
		}

		protected string AttributeMapLastSuccessfulLogon
		{
			get { return SelectRegistrationSetting(setting => setting.AttributeMapLastSuccessfulLogon, "adx_lastsuccessfullogon"); }
		}

		protected string AttributeMapIdentityProvider
		{
			get { return SelectRegistrationSetting(setting => setting.AttributeMapIdentityProvider, "adx_identityprovidername"); }
		}

		protected string EmailClaimType
		{
			get { return SelectRegistrationSetting(setting => setting.EmailClaimType, System.Security.Claims.ClaimTypes.Email); }
		}

		protected string DisplayNameClaimType
		{
			get { return SelectRegistrationSetting(setting => setting.DisplayNameClaimType, System.Security.Claims.ClaimTypes.Name); }
		}

		private static string SelectSetting<T>(T setting, Func<T, string> selector, string defaultValue) where T : class
		{
			return setting != null ? selector(setting) ?? defaultValue : defaultValue;
		}

		private string SelectUserSetting(Func<IUserResolutionSettings, string> selector, string defaultValue)
		{
			return SelectSetting(UserSettings, selector, defaultValue);
		}

		private string SelectRegistrationSetting(Func<IUserRegistrationSettings, string> selector, string defaultValue)
		{
			return SelectSetting(RegistrationSettings, selector, defaultValue);
		}

		#endregion

		#region IHttpHandler Members

		/// <summary>
		/// Gets a value indicating whether another request can use the <see cref="T:System.Web.IHttpHandler" /> instance.
		/// </summary>
		/// <returns>
		/// true if the <see cref="T:System.Web.IHttpHandler" /> instance is reusable; otherwise, false.
		/// </returns>
		public virtual bool IsReusable
		{
			get { return false; }
		}

		/// <summary>
		/// Enables processing of HTTP Web requests by a custom HttpHandler that implements the <see cref="T:System.Web.IHttpHandler" /> interface.
		/// </summary>
		/// <param name="context">An <see cref="T:System.Web.HttpContext" /> object that provides references to the intrinsic server objects (for example, Request, Response, Session, and Server) used to service HTTP requests. </param>
		public virtual void ProcessRequest(HttpContext context)
		{
			var action = context.Request.QueryString["wa"];
			var fam = CreateFederationAuthenticationModule(context);

			TraceInformation("ProcessRequest", "action={0}", action);

			try
			{
				if (action == "wsignout1.0")
				{
					SignOut(context, fam);
				}
				else if (fam.CanReadSignInResponse(new HttpRequestWrapper(context.Request), true))
				{
					if (TryHandleSignInResponse(context, fam))
					{
						return;
					}
				}
				else
				{
					// sign-in with an embedded invitation code pulled from the query string

					var invitationCodeValue = context.Request[InvitationCodeKey];

					TraceInformation("ProcessRequest", "invitationCodeValue={0}", invitationCodeValue);

					if (!string.IsNullOrWhiteSpace(invitationCodeValue))
					{
						RedirectToSignInWithInvitationCode(context, fam, context.Request[ReturnUrlKey], invitationCodeValue);
					}
				}
			}
			catch (Exception exception)
			{
				if (!TryHandleException(context, fam, exception))
				{
					throw new FederationAuthenticationException("Federated sign-in error.", exception);
				}
			}
		}

		#endregion

		protected virtual WSFederationAuthenticationModule CreateFederationAuthenticationModule(HttpContext context)
		{
			var sam = FederatedAuthentication.SessionAuthenticationModule;

			if (sam == null)
			{
				throw new ConfigurationErrorsException("Add a '{0}' to the modules collection of the configuration.".FormatWith(typeof(SessionAuthenticationModule).FullName));
			}

			var fam = new CrmFederationAuthenticationModule(context);

			return fam;
		}

		protected virtual void SignOut(HttpContext context, WSFederationAuthenticationModule fam)
		{
			fam.SignOut(false);
		}

		protected virtual bool TryHandleSignInResponse(HttpContext context, WSFederationAuthenticationModule fam)
		{
			var signInContext = fam.GetSignInResponseMessageContext(context);

			return TryHandleSignInResponse(context, fam, signInContext);
		}

		public virtual bool TryHandleSignInResponse(HttpContext context, WSFederationAuthenticationModule fam, IDictionary<string, string> signInContext)
		{
			string resultCode = null;
			string identityProvider, userName, email, displayName;
			var sessionSecurityToken = GetSessionSecurityToken(context, fam, signInContext, out identityProvider, out userName, out email, out displayName);

			using (var serviceContext = CreateServiceContext(context, fam, signInContext))
			{
				// check if the user already exists

				var existingContact = FindContactByUserName(
					context,
					fam,
					signInContext,
					serviceContext,
					identityProvider,
					userName);

				TraceInformation("TryHandleSignInResponse", "existingContact={0}", existingContact);

				if (existingContact == null)
				{
					// begin registration process if it is enabled

					if (!RegistrationSettings.Enabled) return true;

					if (TryRegisterNewContact(
						context,
						fam,
						signInContext,
						serviceContext,
						sessionSecurityToken,
						identityProvider,
						userName,
						email,
						displayName))
					{
						return true;
					}
				}
				else
				{
					var logonEnabled = existingContact.GetAttributeValue<bool>(AttributeMapLogonEnabled);

					TraceInformation("TryHandleSignInResponse", "logonEnabled={0}", logonEnabled);

					if (logonEnabled)
					{
						// successfully found an existing contact

						if (TryAuthenticateExistingContact(context, fam, signInContext, serviceContext, identityProvider, userName, existingContact, sessionSecurityToken))
						{
							return true;
						}
					}
					else
					{
						resultCode = _inactive;
					}

					if (!RegistrationSettings.Enabled) return true;

					if (TryRegisterExistingContact(
						context,
						fam,
						signInContext,
						serviceContext,
						sessionSecurityToken,
						identityProvider,
						userName,
						email,
						displayName,
						existingContact))
					{
						return true;
					}
				}

				// invalid user account

				if (TryHandleUnregisteredUser(
					context,
					fam,
					signInContext,
					serviceContext,
					sessionSecurityToken,
					identityProvider,
					userName,
					email,
					displayName,
					resultCode))
				{
					return true;
				}
			}

			return false;
		}

		protected virtual bool TryRegisterNewContact(
			HttpContext context,
			WSFederationAuthenticationModule fam,
			IDictionary<string, string> signInContext,
			OrganizationServiceContext serviceContext,
			SessionSecurityToken sessionSecurityToken,
			string identityProvider,
			string userName,
			string email,
			string displayName)
		{
			var invitationCode = signInContext.FirstNotNullOrEmpty(InvitationCodeKey);

			TraceInformation("TryRegisterNewContact", "invitationCode={0}", invitationCode);

			if (RegistrationSettings.RequiresInvitation || !string.IsNullOrWhiteSpace(invitationCode))
			{
				if (!string.IsNullOrWhiteSpace(invitationCode))
				{
					var challengeAnswer = signInContext.FirstNotNullOrEmpty(ChallengeAnswerKey);

					TraceInformation("TryRegisterNewContact", "challengeAnswer={0}", challengeAnswer);

					var invitedContact = RegistrationSettings.RequiresChallengeAnswer || !string.IsNullOrWhiteSpace(challengeAnswer)
						? FindContactByInvitationCodeAndChallengeAnswer(
							context,
							fam,
							signInContext,
							serviceContext,
							invitationCode,
							challengeAnswer)
						: FindContactByInvitationCode(
							context,
							fam,
							signInContext,
							serviceContext,
							invitationCode,
							challengeAnswer);

					TraceInformation("TryRegisterNewContact", "invitedContact={0}", invitedContact);

					if (invitedContact != null)
					{
						var updated = TryUpdateInvitedContact(
							context,
							fam,
							signInContext,
							serviceContext,
							invitedContact,
							identityProvider,
							userName,
							email,
							displayName);

						TraceInformation("TryRegisterNewContact", "updated={0}", updated);

						if (updated)
						{
							// successfully registered a contact to an account

							return TryAuthenticateNewContact(context, fam, signInContext, serviceContext, identityProvider, userName, invitedContact, sessionSecurityToken);
						}
					}
					else
					{
						return TryHandleUnregisteredUser(context, fam, signInContext, serviceContext, sessionSecurityToken, identityProvider, userName, email, displayName, _invalidInvitationCode);
					}

					// unable to find a contact with the given invitation code and challenge answer
				}

				// invitation code not provided, redirect to the registration page
			}
			else
			{
				// handle instant sign-up scenario

				if (RegistrationSettings.RequiresConfirmation)
				{
					var createdConfirmationContact = CreateNewConfirmationContact(
						context,
						fam,
						signInContext,
						serviceContext,
						identityProvider,
						userName,
						email,
						displayName);

					TraceInformation("TryRegisterNewContact", "createdConfirmationContact={0}", createdConfirmationContact);

					if (createdConfirmationContact != null)
					{
						return TryRedirectConfirmingContact(context, fam, signInContext, serviceContext, identityProvider, userName, email, displayName, createdConfirmationContact, sessionSecurityToken);
					}
				}
				else
				{
					// invitation and confirmation is not required, try to create a new contact if the fields are available

					var createdContact = CreateNewContact(
						context,
						fam,
						signInContext,
						serviceContext,
						identityProvider,
						userName,
						email,
						displayName);

					TraceInformation("TryRegisterNewContact", "createdContact={0}", createdContact);

					if (createdContact != null)
					{
						return TryAuthenticateNewContact(context, fam, signInContext, serviceContext, identityProvider, userName, createdContact, sessionSecurityToken);
					}
				}
			}

			return false;
		}

		protected virtual bool TryRegisterExistingContact(
			HttpContext context,
			WSFederationAuthenticationModule fam,
			IDictionary<string, string> signInContext,
			OrganizationServiceContext serviceContext,
			SessionSecurityToken sessionSecurityToken,
			string identityProvider,
			string userName,
			string email,
			string displayName,
			Entity existingContact)
		{
			if (RegistrationSettings.RequiresInvitation || RegistrationSettings.RequiresConfirmation)
			{
				var invitationCode = signInContext.FirstNotNullOrEmpty(InvitationCodeKey);

				TraceInformation("TryRegisterExistingContact", "invitationCode={0}", invitationCode);

				if (!string.IsNullOrWhiteSpace(invitationCode))
				{
					var challengeAnswer = signInContext.FirstNotNullOrEmpty(ChallengeAnswerKey);
					var now = DateTime.UtcNow.Floor(RoundTo.Hour);

					TraceInformation("TryRegisterExistingContact", "challengeAnswer={0}", challengeAnswer);

					var existingInvitationCode = existingContact.GetAttributeValue<string>(AttributeMapInvitationCode);
					var existingInvitationCodeExpiryDate = existingContact.GetAttributeValue<DateTime?>(AttributeMapInvitationCodeExpiryDate);
					var existingChallengeAnswer = existingContact.GetAttributeValue<string>(AttributeMapChallengeAnswer);

					Entity invitedContact = null;

					// the invitation code must match and not be expired

					if (string.Equals(invitationCode, existingInvitationCode))
					{
						if (existingInvitationCodeExpiryDate == null || now < existingInvitationCodeExpiryDate)
						{
							if (RegistrationSettings.RequiresChallengeAnswer)
							{
								// the challenge answer must match

								if (string.Equals(challengeAnswer, existingChallengeAnswer, StringComparison.OrdinalIgnoreCase))
								{
									invitedContact = existingContact;
								}
							}
							else
							{
								// the challenge answer must match if it is not empty

								if (string.IsNullOrWhiteSpace(existingChallengeAnswer)
									|| string.Equals(challengeAnswer, existingChallengeAnswer, StringComparison.OrdinalIgnoreCase))
								{
									invitedContact = existingContact;
								}
							}
						}
					}

					TraceInformation("TryRegisterExistingContact", "invitedContact={0}", invitedContact);

					if (invitedContact != null)
					{
						var updated = TryUpdateInvitedContact(
							context,
							fam,
							signInContext,
							serviceContext,
							invitedContact,
							identityProvider,
							userName,
							email,
							displayName);

						TraceInformation("TryRegisterExistingContact", "updated={0}", updated);

						if (updated)
						{
							// successfully registered a contact to an account

							return TryAuthenticateNewContact(context, fam, signInContext, serviceContext, identityProvider, userName, invitedContact, sessionSecurityToken);
						}
					}
					else
					{
						return TryHandleUnregisteredUser(context, fam, signInContext, serviceContext, sessionSecurityToken, identityProvider, userName, email, displayName, _invalidInvitationCode);
					}

					// unable to find a contact with the given invitation code and challenge answer
				}

				// invitation code not provided, redirect to the registration page
			}
			else
			{
				// handle instant sign-up scenario

				// the contact exists but invitation code is not required so enable logon

				var updated = TryUpdateInvitedContact(
					context,
					fam,
					signInContext,
					serviceContext,
					existingContact,
					identityProvider,
					userName,
					email,
					displayName);

				TraceInformation("TryRegisterExistingContact", "updated={0}", updated);

				if (updated)
				{
					// successfully registered a contact to an account

					return TryAuthenticateNewContact(context, fam, signInContext, serviceContext, identityProvider, userName, existingContact, sessionSecurityToken);
				}
			}

			return false;
		}

		protected virtual bool TryHandleUnregisteredUser(
			HttpContext context,
			WSFederationAuthenticationModule fam,
			IDictionary<string, string> signInContext,
			OrganizationServiceContext serviceContext,
			SessionSecurityToken sessionSecurityToken,
			string identityProvider,
			string userName,
			string email,
			string displayName,
			string resultCode)
		{
			if (!string.IsNullOrEmpty(RegistrationSettings.RegistrationPath))
			{
				// post the sign-in response to the registration page

				var uri = GetRegistrationUri(context, fam, signInContext, serviceContext, resultCode);

				fam.WriteSignInResponseMessageAsFormPost(context, uri);
			}
			else
			{
				// redirect to default page

				var returnUrl = GetDefaultReturnPath(context, fam, signInContext, serviceContext);

				TraceInformation("TryHandleUnregisteredUser", "returnUrl={0}", returnUrl);

				context.RedirectAndEndResponse(returnUrl);
			}

			return true;
		}

		protected virtual bool ValidateRequiredAttributesForContact(
			HttpContext context,
			WSFederationAuthenticationModule fam,
			IDictionary<string, string> signInContext,
			OrganizationServiceContext serviceContext,
			Entity contact)
		{
			if (RegistrationSettings.RequiredLevel == AttributeRequiredLevel.None)
			{
				return true;
			}

			// check required fields

			var request = new RetrieveEntityRequest { LogicalName = contact.LogicalName, EntityFilters = EntityFilters.Attributes };
			var response = serviceContext.Execute<RetrieveEntityResponse>(request);
			var attributes = response.EntityMetadata.Attributes;

			foreach (var attribute in attributes.Where(
				a => a.IsValidForUpdate != null
				&& a.IsValidForUpdate.Value
				&& a.RequiredLevel.Value >= AttributeRequiredLevel.ApplicationRequired
				&& a.RequiredLevel.Value <= RegistrationSettings.RequiredLevel))
			{
				if (!contact.Attributes.ContainsKey(attribute.LogicalName))
				{
					TraceInformation("ValidateRequiredAttributesForContact", "attribute.LogicalName={0}", attribute.LogicalName);

					return false;
				}

				var value = contact[attribute.LogicalName];

				if (value == null || (value is string && string.IsNullOrWhiteSpace(value as string)))
				{
					TraceInformation("ValidateRequiredAttributesForContact", "attribute.LogicalName={0}, value={1}", attribute.LogicalName, value);

					return false;
				}
			}

			return true;
		}

		protected virtual bool TryAuthenticateExistingContact(
			HttpContext context,
			WSFederationAuthenticationModule fam,
			IDictionary<string, string> signInContext,
			OrganizationServiceContext serviceContext,
			string identityProvider,
			string userName,
			Entity contact,
			SessionSecurityToken sessionSecurityToken)
		{
			TraceInformation("TryAuthenticateExistingContact", "identityProvider={0}, userName={1}", identityProvider, userName);

			// redirect to the profile if there are required fields missing

			var returnPath = ValidateRequiredAttributesForContact(context, fam, signInContext, serviceContext, contact)
				? GetDefaultReturnPath(context, fam, signInContext, serviceContext)
				: GetProfilePath(context, fam, signInContext, serviceContext);

			return TryAuthenticateContact(context, fam, signInContext, serviceContext, identityProvider, userName, contact, sessionSecurityToken, returnPath);
		}

		protected virtual bool TryAuthenticateNewContact(
			HttpContext context,
			WSFederationAuthenticationModule fam,
			IDictionary<string, string> signInContext,
			OrganizationServiceContext serviceContext,
			string identityProvider,
			string userName,
			Entity contact,
			SessionSecurityToken sessionSecurityToken)
		{
			TraceInformation("TryAuthenticateNewContact", "identityProvider={0}, userName={1}", identityProvider, userName);

			// redirect to the profile

			var returnPath = GetProfilePath(context, fam, signInContext, serviceContext);

			return TryAuthenticateContact(context, fam, signInContext, serviceContext, identityProvider, userName, contact, sessionSecurityToken, returnPath);
		}

		protected virtual bool TryAuthenticateContact(
			HttpContext context,
			WSFederationAuthenticationModule fam,
			IDictionary<string, string> signInContext,
			OrganizationServiceContext serviceContext,
			string identityProvider,
			string userName,
			Entity contact,
			SessionSecurityToken sessionSecurityToken,
			string returnPath)
		{
			var now = DateTime.UtcNow.Floor(RoundTo.Second);

			serviceContext.ReAttach(contact);
			contact.SetAttributeValue(AttributeMapLastSuccessfulLogon, now);

			serviceContext.UpdateObject(contact);
			var results = serviceContext.SaveChanges();

			if (results.HasError)
			{
				throw results.First().Error;
			}

			fam.SetSessionSecurityTokenAndRedirect(context, sessionSecurityToken, returnPath);

			return true;
		}

		protected virtual bool TryRedirectConfirmingContact(
			HttpContext context,
			WSFederationAuthenticationModule fam,
			IDictionary<string, string> signInContext,
			OrganizationServiceContext serviceContext,
			string identityProvider,
			string userName,
			string email,
			string displayName,
			Entity contact,
			SessionSecurityToken sessionSecurityToken)
		{
			TraceInformation("TryRedirectConfirmingContact", "identityProvider={0}, userName={1}", identityProvider, userName);

			if (!string.IsNullOrEmpty(RegistrationSettings.ConfirmationPath))
			{
				// post the sign-in response to the confirmation page

				var uri = GetConfirmationUri(context, fam, signInContext, serviceContext);

				fam.WriteSignInResponseMessageAsFormPost(context, uri);
			}
			else
			{
				return TryHandleUnregisteredUser(context, fam, signInContext, serviceContext, sessionSecurityToken, identityProvider, userName, email, displayName, _confirm);
			}

			return true;
		}

		protected virtual Entity CreateNewContact(
			HttpContext context,
			WSFederationAuthenticationModule fam,
			IDictionary<string, string> signInContext,
			OrganizationServiceContext serviceContext,
			string identityProvider,
			string userName,
			string email,
			string displayName)
		{
			var attributes = ToAttributes(signInContext);

			return CreateNewContact(context, fam, serviceContext, attributes, identityProvider, userName, email, displayName);
		}

		protected virtual Entity CreateNewContact(
			HttpContext context,
			WSFederationAuthenticationModule fam,
			OrganizationServiceContext serviceContext,
			IEnumerable<KeyValuePair<string, string>> attributes,
			string identityProvider,
			string userName,
			string email,
			string displayName)
		{
			TraceInformation("CreateNewContact", "identityProvider={0}, userName={1}, email={2}, displayName={3}", identityProvider, userName, email, displayName);

			var entity = new Entity(MemberEntityName);

			foreach (var attribute in attributes)
			{
				entity[attribute.Key] = attribute.Value;
			}

			entity.Attributes[AttributeMapLogonEnabled] = true;
			entity.Attributes[AttributeMapIdentityProvider] = identityProvider;
			entity.Attributes[AttributeMapUsername] = userName;
			entity.Attributes[AttributeMapInvitationCode] = string.Empty;
			entity.Attributes[AttributeMapInvitationCodeExpiryDate] = null;

			if (!string.IsNullOrEmpty(email) && !string.IsNullOrWhiteSpace(AttributeMapEmail) && !entity.Attributes.ContainsKey(AttributeMapEmail))
			{
				entity.Attributes[AttributeMapEmail] = email;
			}

			serviceContext.AddObject(entity);
			var results = serviceContext.SaveChanges();

			if (results.HasError)
			{
				throw results.First().Error;
			}

			return entity;
		}

		protected virtual Entity CreateNewConfirmationContact(
			HttpContext context,
			WSFederationAuthenticationModule fam,
			IDictionary<string, string> signInContext,
			OrganizationServiceContext serviceContext,
			string identityProvider,
			string userName,
			string email,
			string displayName)
		{
			// verify that an email address is provided in the signInContext

			if (!signInContext.ContainsKey(AttributeMapEmail))
			{
				return null;
			}

			var invitationCode = Guid.NewGuid().ToString().Replace("-", string.Empty).ToUpper();
			var now = DateTime.UtcNow.Floor(RoundTo.Second);
			var invitationCodeExpiryDate = RegistrationSettings.InvitationCodeDuration != null
				? now + RegistrationSettings.InvitationCodeDuration
				: null;

			return CreateNewConfirmationContact(context, fam, signInContext, serviceContext, identityProvider, userName, email, displayName, invitationCode, invitationCodeExpiryDate);
		}

		protected virtual Entity CreateNewConfirmationContact(
			HttpContext context,
			WSFederationAuthenticationModule fam,
			IDictionary<string, string> signInContext,
			OrganizationServiceContext serviceContext,
			string identityProvider,
			string userName,
			string email,
			string displayName,
			string invitationCode,
			DateTime? invitationCodeExpiryDate)
		{
			var attributes = ToAttributes(signInContext);

			return CreateNewConfirmationContact(context, fam, serviceContext, attributes, identityProvider, userName, email, displayName, invitationCode, invitationCodeExpiryDate);
		}

		protected virtual Entity CreateNewConfirmationContact(
			HttpContext context,
			WSFederationAuthenticationModule fam,
			OrganizationServiceContext serviceContext,
			IEnumerable<KeyValuePair<string, string>> attributes,
			string identityProvider,
			string userName,
			string email,
			string displayName,
			string invitationCode,
			DateTime? invitationCodeExpiryDate)
		{
			TraceInformation("CreateNewConfirmationContact", "identityProvider={0}, userName={1}, email={2}, displayName={3}, invitationCode={4}", identityProvider, userName, email, displayName, invitationCode);

			var entity = new Entity(MemberEntityName);

			foreach (var attribute in attributes)
			{
				entity[attribute.Key] = attribute.Value;
			}

			entity.Attributes[AttributeMapLogonEnabled] = false;
			entity.Attributes[AttributeMapIdentityProvider] = identityProvider;
			entity.Attributes[AttributeMapUsername] = userName;
			entity.Attributes[AttributeMapInvitationCode] = invitationCode;

			if (invitationCodeExpiryDate != null)
			{
				entity.Attributes[AttributeMapInvitationCodeExpiryDate] = invitationCodeExpiryDate.Value;
			}

			if (!string.IsNullOrWhiteSpace(email) && !string.IsNullOrWhiteSpace(AttributeMapEmail) && !entity.Attributes.ContainsKey(AttributeMapEmail))
			{
				entity.Attributes[AttributeMapEmail] = email;
			}

			if (!string.IsNullOrWhiteSpace(displayName) && !string.IsNullOrWhiteSpace(AttributeMapDisplayName) && !entity.Attributes.ContainsKey(AttributeMapDisplayName))
			{
				entity.Attributes[AttributeMapDisplayName] = displayName;
			}

			serviceContext.AddObject(entity);
			var results = serviceContext.SaveChanges();

			if (results.HasError)
			{
				throw results.First().Error;
			}

			return entity;
		}

		protected virtual SessionSecurityToken GetSessionSecurityToken(
			HttpContext context,
			WSFederationAuthenticationModule fam,
			IDictionary<string, string> signInContext,
			out string identityProvider,
			out string userName,
			out string email,
			out string displayName)
		{
			var token = fam.GetSessionSecurityToken(context, out identityProvider, out userName, out email, out displayName, EmailClaimType, DisplayNameClaimType);

			return token;
		}

		protected virtual OrganizationServiceContext CreateServiceContext(
			HttpContext context,
			WSFederationAuthenticationModule fam,
			IDictionary<string, string> signInContext)
		{
			var serviceContext = PortalCrmConfigurationManager.CreateServiceContext(RegistrationSettings.PortalName);

			return serviceContext;
		}

		protected virtual Entity FindContactByUserName(
			HttpContext context,
			WSFederationAuthenticationModule fam,
			IDictionary<string, string> signInContext,
			OrganizationServiceContext serviceContext,
			string identityProvider,
			string userName)
		{
			TraceInformation("FindContactByUserName", "identityProvider={0}, userName={1}", identityProvider, userName);

			var entity = serviceContext.CreateQuery(MemberEntityName).SingleOrDefault(c => c.GetAttributeValue<string>(AttributeMapUsername) == userName);

			return entity;
		}

		protected virtual Entity FindContactByInvitationCode(
			HttpContext context,
			WSFederationAuthenticationModule fam,
			IDictionary<string, string> signInContext,
			OrganizationServiceContext serviceContext,
			string invitationCode,
			string optionalChallengeAnswer = null)
		{
			TraceInformation("FindContactByInvitationCode", "invitationCode={0}, optionalChallengeAnswer={1}", invitationCode, optionalChallengeAnswer);

			if (string.IsNullOrWhiteSpace(invitationCode))
			{
				return null;
			}

			// find a contact with a non-expired invitation code and an empty (or matching) challenge answer

			var entity = serviceContext.CreateQuery(MemberEntityName).SingleOrDefault(
				c => c.GetAttributeValue<string>(AttributeMapInvitationCode) == invitationCode
				&& (
					c.GetAttributeValue<string>(AttributeMapChallengeAnswer) == null
					|| c.GetAttributeValue<string>(AttributeMapChallengeAnswer) == string.Empty
					|| c.GetAttributeValue<string>(AttributeMapChallengeAnswer) == optionalChallengeAnswer));

			if (entity != null)
			{
				var now = DateTime.UtcNow.Floor(RoundTo.Second);
				var existingInvitationCodeExpiryDate = entity.GetAttributeValue<DateTime?>(AttributeMapInvitationCodeExpiryDate);

				if (existingInvitationCodeExpiryDate != null && existingInvitationCodeExpiryDate < now)
				{
					// invitation code is expired

					return null;
				}
			}

			return entity;
		}

		protected virtual Entity FindContactByInvitationCodeAndChallengeAnswer(
			HttpContext context,
			WSFederationAuthenticationModule fam,
			IDictionary<string, string> signInContext,
			OrganizationServiceContext serviceContext,
			string invitationCode,
			string challengeAnswer)
		{
			if (string.IsNullOrWhiteSpace(invitationCode) || string.IsNullOrWhiteSpace(challengeAnswer))
			{
				return null;
			}

			var entity = FindContactByInvitationCode(context, fam, signInContext, serviceContext, invitationCode, challengeAnswer);

			if (entity != null)
			{
				var answer = entity.GetAttributeValue<string>(AttributeMapChallengeAnswer);

				if (string.IsNullOrWhiteSpace(answer))
				{
					// the contact does not have an answer specified but it is required, administrator intervention is needed

					throw new FederationAuthenticationException(FederationAuthenticationErrorReason.MissingChallengeAnswer);
				}
			}

			return entity;
		}

		protected virtual bool TryUpdateInvitedContact(
			HttpContext context,
			WSFederationAuthenticationModule fam,
			IDictionary<string, string> signInContext,
			OrganizationServiceContext serviceContext,
			Entity entity,
			string identityProvider,
			string userName,
			string email,
			string displayName)
		{
			TraceInformation("TryUpdateInvitedContact", "identityProvider={0}, userName={1}, email={2}, displayName={3}", identityProvider, userName, email, displayName);

			entity.Attributes[AttributeMapLogonEnabled] = true;
			entity.Attributes[AttributeMapIdentityProvider] = identityProvider;
			entity.Attributes[AttributeMapUsername] = userName;
			entity.Attributes[AttributeMapInvitationCode] = string.Empty;
			entity.Attributes[AttributeMapInvitationCodeExpiryDate] = null;

			if (!string.IsNullOrWhiteSpace(email) && !string.IsNullOrWhiteSpace(AttributeMapEmail) && !entity.Attributes.ContainsKey(AttributeMapEmail))
			{
				entity.Attributes[AttributeMapEmail] = email;
			}

			if (!string.IsNullOrWhiteSpace(displayName) && !string.IsNullOrWhiteSpace(AttributeMapDisplayName) && !entity.Attributes.ContainsKey(AttributeMapDisplayName))
			{
				entity.Attributes[AttributeMapDisplayName] = displayName;
			}

			serviceContext.UpdateObject(entity);
			var results = serviceContext.SaveChanges();

			if (results.HasError)
			{
				throw results.First().Error;
			}

			return true;
		}

		public virtual bool TryHandleException(
			HttpContext context,
			WSFederationAuthenticationModule fam,
			Exception exception)
		{
			// redirect to the error page if it is specified

			if (RegistrationSettings != null && !string.IsNullOrEmpty(RegistrationSettings.ErrorPath))
			{
				var signInContext = fam.GetSignInResponseMessageContext(context);
				var returnPath = GetErrorPath(context, fam, signInContext);

				TraceInformation("TryHandleException", "returnPath={0}", returnPath);

				context.RedirectAndEndResponse(returnPath);

				return true;
			}

			return false;
		}

		protected virtual string GetDefaultReturnPath(
			HttpContext context,
			WSFederationAuthenticationModule fam,
			IDictionary<string, string> signInContext,
			OrganizationServiceContext serviceContext)
		{
			var returnPath = signInContext.FirstNotNullOrEmpty(ReturnUrlKey);
			var path = !string.IsNullOrWhiteSpace(returnPath) ? returnPath : DefaultReturnPath;

			return path;
		}

		protected virtual string GetProfilePath(
			HttpContext context,
			WSFederationAuthenticationModule fam,
			IDictionary<string, string> signInContext,
			OrganizationServiceContext serviceContext)
		{
			return GetReturnPath(context, fam, signInContext, ProfilePath);
		}

		protected virtual string GetErrorPath(
			HttpContext context,
			WSFederationAuthenticationModule fam,
			IDictionary<string, string> signInContext)
		{
			return GetReturnPath(context, fam, signInContext, ErrorPath);
		}

		protected virtual Uri GetConfirmationUri(
			HttpContext context,
			WSFederationAuthenticationModule fam,
			IDictionary<string, string> signInContext,
			OrganizationServiceContext serviceContext)
		{
			return GetReturnUri(context, RegistrationSettings.ConfirmationPath, null);
		}

		protected virtual Uri GetRegistrationUri(
			HttpContext context,
			WSFederationAuthenticationModule fam,
			IDictionary<string, string> signInContext,
			OrganizationServiceContext serviceContext,
			string resultCode)
		{
			return GetReturnUri(context, RegistrationSettings.RegistrationPath, resultCode);
		}

		protected virtual void RedirectToSignInWithInvitationCode(
			HttpContext context,
			WSFederationAuthenticationModule fam,
			string returnUrl,
			string invitationCode)
		{
			fam.RedirectToSignIn(context, returnUrl, invitationCode, null, RegistrationSettings);
		}

		protected string GetReturnPath(
			HttpContext context,
			WSFederationAuthenticationModule fam,
			IDictionary<string, string> signInContext,
			string path)
		{
			var returnPath = VirtualPathUtility.ToAbsolute(GetDefaultReturnPath(context, fam, signInContext, null));
			var errorPath = !string.IsNullOrWhiteSpace(returnPath)
				? "{0}{1}{2}={3}".FormatWith(path, path.Contains("?") ? "&" : "?", ReturnUrlKey, returnPath)
				: path;

			return errorPath;
		}

		protected Uri GetReturnUri(HttpContext context, string path, string resultCode)
		{
			var baseUri = context.Request.Url.GetLeftPart(UriPartial.Authority);
			var returnPath = VirtualPathUtility.ToAbsolute(path);
			var returnPathWithResult = !string.IsNullOrWhiteSpace(resultCode)
				? "{0}{1}{2}={3}".FormatWith(returnPath, returnPath.Contains("?") ? "&" : "?", ResultCodeKey, resultCode)
				: returnPath;

			return new Uri(baseUri + returnPathWithResult);
		}

		protected IEnumerable<KeyValuePair<string, string>> ToAttributes(IEnumerable<KeyValuePair<string, string>> signInContext)
		{
			var attributeFilterEnabled = RegistrationSettings.SignUpAttributes != null && RegistrationSettings.SignUpAttributes.Any();

			var attributes = attributeFilterEnabled
				? signInContext.Where(item => RegistrationSettings.SignUpAttributes.Contains(item.Key))
				: signInContext.Where(item => !new[] { ReturnUrlKey, InvitationCodeKey, ChallengeAnswerKey }.Contains(item.Key));

			return attributes;
		}

		private static void TraceInformation(string memberName, string format, params object[] args)
		{
			Tracing.FrameworkInformation(typeof(FederationAuthenticationHandler).Name, memberName, format, args);
		}
	}
}
