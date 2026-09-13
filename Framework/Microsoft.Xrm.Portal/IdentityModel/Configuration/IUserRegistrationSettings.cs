/*
  Copyright (c) Microsoft Corporation. All rights reserved.
  Licensed under the MIT License. See License.txt in the project root for license information.
*/

using System;
using System.Collections.Generic;
using Microsoft.Xrm.Sdk.Metadata;

namespace Microsoft.Xrm.Portal.IdentityModel.Configuration
{
	/// <summary>
	/// The configuration settings for user registration.
	/// </summary>
	/// <seealso cref="FederationCrmConfigurationManager"/>
	/// <seealso cref="UserRegistrationElement"/>
	public interface IUserRegistrationSettings
	{
		/// <summary>
		/// The name of the associated portal context for retrieving portal related settings.
		/// </summary>
		string PortalName { get; }

		/// <summary>
		/// Allows all user registration processing to be globally enabled or disabled.
		/// </summary>
		bool Enabled { get; }

		/// <summary>
		/// Forces an invitation code to be provided before a user is registered.
		/// </summary>
		bool RequiresInvitation { get; }

		/// <summary>
		/// Forces a challenge answer to be provided in addition to an invitation code before a user is registered.
		/// </summary>
		bool RequiresChallengeAnswer { get; }

		/// <summary>
		/// Enables an email workflow to be triggered to confirm that the registering user possesses a valid email address. Enabling this prevents open registration from occurring.
		/// </summary>
		bool RequiresConfirmation { get; }

		/// <summary>
		/// The logical name of the invitation code attribute.
		/// </summary>
		string AttributeMapInvitationCode { get; }

		/// <summary>
		/// The logical name of the invitation code expiry date attribute.
		/// </summary>
		string AttributeMapInvitationCodeExpiryDate { get; }

		/// <summary>
		/// The logical name of the challenge answer attribute.
		/// </summary>
		string AttributeMapChallengeAnswer { get; }

		/// <summary>
		/// The logical name of the logon enabled attribute.
		/// </summary>
		string AttributeMapLogonEnabled { get; }

		/// <summary>
		/// The logical name of the email attribute.
		/// </summary>
		string AttributeMapEmail { get; }

		/// <summary>
		/// The logical name of the display name attribute.
		/// </summary>
		string AttributeMapDisplayName { get; }

		/// <summary>
		/// The logical name of the last successful logon attribute.
		/// </summary>
		string AttributeMapLastSuccessfulLogon { get; }

		/// <summary>
		/// The logical name of the identity provider attribute.
		/// </summary>
		string AttributeMapIdentityProvider { get; }

		/// <summary>
		/// The claim type of the 'email' claim.
		/// </summary>
		string EmailClaimType { get; }

		/// <summary>
		/// The claim type of the 'name' claim.
		/// </summary>
		string DisplayNameClaimType { get; }

		/// <summary>
		/// The query string name for the return URL.
		/// </summary>
		string ReturnUrlKey { get; }

		/// <summary>
		/// The query string name for the invitation code.
		/// </summary>
		string InvitationCodeKey { get; }

		/// <summary>
		/// The query string name for the challenge answer.
		/// </summary>
		string ChallengeAnswerKey { get; }

		/// <summary>
		/// The query string name for the Live ID token.
		/// </summary>
		string LiveIdTokenKey { get; }

		/// <summary>
		/// The query string name for the result code.
		/// </summary>
		string ResultCodeKey { get; }

		/// <summary>
		/// The return path, used by the federation authentication handler, when no explicit path is provided.
		/// </summary>
		string DefaultReturnPath { get; }

		/// <summary>
		/// The location the user is redirected to after a new contact registration is created. If a contact record is missing values for required fields and the 'requiredLevel' attribute is set to the "ApplicationRequired" value, then the user is redirected to this page after every sign-in. This redirect indicates a successful registration or sign-in. If no value is provided, the user is redirected to the 'defaultReturnPath'.
		/// </summary>
		string ProfilePath { get; }

		/// <summary>
		/// The path to the registration page that renders the form for providing invitation code, challenge question/answer, and open registration details. A user is redirected here if he or she is signing into the portal but is not yet registered. In this case, the sign-in attempt has either failed or is still in progress. If no value is provided, the user is redirected to the 'defaultReturnPath'.
		/// </summary>
		string RegistrationPath { get; }

		/// <summary>
		/// The location the user is redirected to after a email confirmation workflow is triggered to provide further instructions on completing the registration. If no value is provided, the user is redirected to the 'defaultReturnPath'.
		/// </summary>
		string ConfirmationPath { get; }

		/// <summary>
		/// The location the user is redirected to when the federation authentication handler throws an exception. If no value is provided, the user is redirected to the 'defaultReturnPath'.
		/// </summary>
		string ErrorPath { get; }

		/// <summary>
		/// When the LiveIdAccountTransferHandler (rather than the default LiveIdWebAuthenticationHandler) is wired up as the Windows Live ID authentication sign-in response handler (LiveID.axd), users are redirected to this account transfer page. This page should prompt the user to continue to the next step of signing into an AppFabric ACS account.
		/// </summary>
		string AccountTransferPath { get; }

		/// <summary>
		/// When the LiveIdAccountTransferHandler is unable to find the Windows Live ID account that a user is attempting to transfer, the user is redirected to this path.
		/// </summary>
		string UnregisteredUserPath { get; }

		/// <summary>
		/// When a confirmation email workflow is generated, this value specifies the duration that the invitation code is considered valid.
		/// </summary>
		TimeSpan? InvitationCodeDuration { get; }

		/// <summary>
		/// Controls the level of validation when determining if a contact entity contains all the attribute values deemed required.
		/// </summary>
		AttributeRequiredLevel RequiredLevel { get; }

		/// <summary>
		/// A filter for the set of attributes to be accepted as user sign-up attributes.
		/// </summary>
		IEnumerable<string> SignUpAttributes { get; }
	}
}
