/*
  Copyright (c) Microsoft Corporation. All rights reserved.
  Licensed under the MIT License. See License.txt in the project root for license information.
*/

using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using Microsoft.Xrm.Sdk.Metadata;

namespace Microsoft.Xrm.Portal.IdentityModel.Configuration
{
	/// <summary>
	/// The configuration settings for user registration.
	/// </summary>
	/// <seealso cref="FederationCrmConfigurationManager"/>
	public sealed class UserRegistrationElement : ConfigurationElement, IUserRegistrationSettings
	{
		private static readonly ConfigurationPropertyCollection _properties;

		private static readonly ConfigurationProperty _propPortalName;
		private static readonly ConfigurationProperty _propEnabled;

		private static readonly ConfigurationProperty _propRequiresInvitation;
		private static readonly ConfigurationProperty _propRequiresChallengeAnswer;
		private static readonly ConfigurationProperty _propRequiresConfirmation;

		private static readonly ConfigurationProperty _propAttributeMapInvitationCode;
		private static readonly ConfigurationProperty _propAttributeMapInvitationCodeExpiryDate;
		private static readonly ConfigurationProperty _propAttributeMapChallengeAnswer;
		private static readonly ConfigurationProperty _propAttributeMapLogonEnabled;
		private static readonly ConfigurationProperty _propAttributeMapEmail;
		private static readonly ConfigurationProperty _propAttributeMapDisplayName;
		private static readonly ConfigurationProperty _propAttributeMapLastSuccessfulLogon;
		private static readonly ConfigurationProperty _propAttributeMapIdentityProvider;

		private static readonly ConfigurationProperty _propEmailClaimType;
		private static readonly ConfigurationProperty _propDisplayNameClaimType;

		private static readonly ConfigurationProperty _propReturnUrlKey;
		private static readonly ConfigurationProperty _propInvitationCodeKey;
		private static readonly ConfigurationProperty _propChallengeAnswerKey;
		private static readonly ConfigurationProperty _propResultCodeKey;

		private static readonly ConfigurationProperty _propDefaultReturnPath;
		private static readonly ConfigurationProperty _propProfilePath;
		private static readonly ConfigurationProperty _propRegistrationPath;
		private static readonly ConfigurationProperty _propConfirmationPath;
		private static readonly ConfigurationProperty _propErrorPath;

		private static readonly ConfigurationProperty _propInvitationCodeDuration;

		private static readonly ConfigurationProperty _propRequiredLevel;

		private static readonly ConfigurationProperty _propSignUpAttributes;

		static UserRegistrationElement()
		{
			_propPortalName = new ConfigurationProperty("portalName", typeof(string), null, ConfigurationPropertyOptions.None);
			_propEnabled = new ConfigurationProperty("enabled", typeof(bool), false, ConfigurationPropertyOptions.None);

			_propRequiresInvitation = new ConfigurationProperty("requiresInvitation", typeof(bool), true, ConfigurationPropertyOptions.None);
			_propRequiresChallengeAnswer = new ConfigurationProperty("requiresChallengeAnswer", typeof(bool), true, ConfigurationPropertyOptions.None);
			_propRequiresConfirmation = new ConfigurationProperty("requiresConfirmation", typeof(bool), true, ConfigurationPropertyOptions.None);

			_propAttributeMapInvitationCode = new ConfigurationProperty("attributeMapInvitationCode", typeof(string), null, ConfigurationPropertyOptions.None);
			_propAttributeMapInvitationCodeExpiryDate = new ConfigurationProperty("atributeMapInvitationCodeExpiryDate", typeof(string), null, ConfigurationPropertyOptions.None);
			_propAttributeMapChallengeAnswer = new ConfigurationProperty("attributeMapChallengeAnswer", typeof(string), null, ConfigurationPropertyOptions.None);
			_propAttributeMapLogonEnabled = new ConfigurationProperty("attributeMapLogonEnabled", typeof(string), null, ConfigurationPropertyOptions.None);
			_propAttributeMapEmail = new ConfigurationProperty("attributeMapEmail", typeof(string), null, ConfigurationPropertyOptions.None);
			_propAttributeMapDisplayName = new ConfigurationProperty("attributeMapDisplayName", typeof(string), null, ConfigurationPropertyOptions.None);
			_propAttributeMapLastSuccessfulLogon = new ConfigurationProperty("attributeMapLastSuccessfulLogon", typeof(string), null, ConfigurationPropertyOptions.None);
			_propAttributeMapIdentityProvider = new ConfigurationProperty("attributeMapIdentityProvider", typeof(string), null, ConfigurationPropertyOptions.None);

			_propEmailClaimType = new ConfigurationProperty("emailClaimType", typeof(string), null, ConfigurationPropertyOptions.None);
			_propDisplayNameClaimType = new ConfigurationProperty("displayNameClaimType", typeof(string), null, ConfigurationPropertyOptions.None);

			_propReturnUrlKey = new ConfigurationProperty("returnUrlKey", typeof(string), null, ConfigurationPropertyOptions.None);
			_propInvitationCodeKey = new ConfigurationProperty("invitationCodeKey", typeof(string), null, ConfigurationPropertyOptions.None);
			_propChallengeAnswerKey = new ConfigurationProperty("challengeAnswerKey", typeof(string), null, ConfigurationPropertyOptions.None);
			_propResultCodeKey = new ConfigurationProperty("resultCodeKey", typeof(string), null, ConfigurationPropertyOptions.None);

			_propDefaultReturnPath = new ConfigurationProperty("defaultReturnPath", typeof(string), null, ConfigurationPropertyOptions.None);
			_propProfilePath = new ConfigurationProperty("profilePath", typeof(string), null, ConfigurationPropertyOptions.None);
			_propRegistrationPath = new ConfigurationProperty("registrationPath", typeof(string), null, ConfigurationPropertyOptions.None);
			_propConfirmationPath = new ConfigurationProperty("confirmationPath", typeof(string), null, ConfigurationPropertyOptions.None);
			_propErrorPath = new ConfigurationProperty("errorPath", typeof(string), null, ConfigurationPropertyOptions.None);

			_propInvitationCodeDuration = new ConfigurationProperty("invitationCodeDuration", typeof(TimeSpan?), null, ConfigurationPropertyOptions.None);

			_propRequiredLevel = new ConfigurationProperty("requiredLevel", typeof(AttributeRequiredLevel), AttributeRequiredLevel.ApplicationRequired, ConfigurationPropertyOptions.None);

			_propSignUpAttributes = new ConfigurationProperty(SignUpAttributeElementCollection.Name, typeof(SignUpAttributeElementCollection), new SignUpAttributeElementCollection(), ConfigurationPropertyOptions.None);

			_properties = new ConfigurationPropertyCollection
			{
				_propPortalName,
				_propEnabled,

				_propRequiresInvitation,
				_propRequiresChallengeAnswer,
				_propRequiresConfirmation,

				_propAttributeMapInvitationCode,
				_propAttributeMapInvitationCodeExpiryDate,
				_propAttributeMapChallengeAnswer,
				_propAttributeMapLogonEnabled,
				_propAttributeMapEmail,
				_propAttributeMapDisplayName,
				_propAttributeMapLastSuccessfulLogon,
				_propAttributeMapIdentityProvider,

				_propEmailClaimType,
				_propDisplayNameClaimType,

				_propReturnUrlKey,
				_propInvitationCodeKey,
				_propChallengeAnswerKey,
				_propResultCodeKey,

				_propDefaultReturnPath,
				_propProfilePath,
				_propRegistrationPath,
				_propConfirmationPath,
				_propErrorPath,

				_propInvitationCodeDuration,

				_propRequiredLevel,

				_propSignUpAttributes,
			};
		}

		protected override ConfigurationPropertyCollection Properties
		{
			get { return _properties; }
		}

		public override bool IsReadOnly()
		{
			return false;
		}

		/// <summary>
		/// The name of the associated portal context for retrieving portal related settings.
		/// </summary>
		public string PortalName
		{
			get { return (string)base[_propPortalName]; }
			set { base[_propPortalName] = value; }
		}

		/// <summary>
		/// Allows all user registration processing to be globally enabled or disabled.
		/// </summary>
		public bool Enabled
		{
			get { return (bool)base[_propEnabled]; }
			set { base[_propEnabled] = value; }
		}

		/// <summary>
		/// Forces an invitation code to be provided before a user is registered.
		/// </summary>
		public bool RequiresInvitation
		{
			get { return (bool)base[_propRequiresInvitation]; }
			set { base[_propRequiresInvitation] = value; }
		}

		/// <summary>
		/// Forces a challenge answer to be provided in addition to an invitation code before a user is registered.
		/// </summary>
		public bool RequiresChallengeAnswer
		{
			get { return (bool)base[_propRequiresChallengeAnswer]; }
			set { base[_propRequiresChallengeAnswer] = value; }
		}

		/// <summary>
		/// Enables an email workflow to be triggered to confirm that the registering user possesses a valid email address. Enabling this prevents open registration from occurring.
		/// </summary>
		public bool RequiresConfirmation
		{
			get { return (bool)base[_propRequiresConfirmation]; }
			set { base[_propRequiresConfirmation] = value; }
		}

		/// <summary>
		/// The logical name of the invitation code attribute.
		/// </summary>
		public string AttributeMapInvitationCode
		{
			get { return (string)base[_propAttributeMapInvitationCode]; }
			set { base[_propAttributeMapInvitationCode] = value; }
		}

		/// <summary>
		/// The logical name of the invitation code expiry date attribute.
		/// </summary>
		public string AttributeMapInvitationCodeExpiryDate
		{
			get { return (string)base[_propAttributeMapInvitationCodeExpiryDate]; }
			set { base[_propAttributeMapInvitationCodeExpiryDate] = value; }
		}

		/// <summary>
		/// The logical name of the challenge answer attribute.
		/// </summary>
		public string AttributeMapChallengeAnswer
		{
			get { return (string)base[_propAttributeMapChallengeAnswer]; }
			set { base[_propAttributeMapChallengeAnswer] = value; }
		}

		/// <summary>
		/// The logical name of the logon enabled attribute.
		/// </summary>
		public string AttributeMapLogonEnabled
		{
			get { return (string)base[_propAttributeMapLogonEnabled]; }
			set { base[_propAttributeMapLogonEnabled] = value; }
		}

		/// <summary>
		/// The logical name of the email attribute.
		/// </summary>
		public string AttributeMapEmail
		{
			get { return (string)base[_propAttributeMapEmail]; }
			set { base[_propAttributeMapEmail] = value; }
		}

		/// <summary>
		/// The logical name of the display name attribute.
		/// </summary>
		public string AttributeMapDisplayName
		{
			get { return (string)base[_propAttributeMapDisplayName]; }
			set { base[_propAttributeMapDisplayName] = value; }
		}

		/// <summary>
		/// The logical name of the last successful logon attribute.
		/// </summary>
		public string AttributeMapLastSuccessfulLogon
		{
			get { return (string)base[_propAttributeMapLastSuccessfulLogon]; }
			set { base[_propAttributeMapLastSuccessfulLogon] = value; }
		}

		/// <summary>
		/// The logical name of the identity provider attribute.
		/// </summary>
		public string AttributeMapIdentityProvider
		{
			get { return (string)base[_propAttributeMapIdentityProvider]; }
			set { base[_propAttributeMapIdentityProvider] = value; }
		}

		/// <summary>
		/// The claim type of the 'email' claim.
		/// </summary>
		public string EmailClaimType
		{
			get { return (string)base[_propEmailClaimType]; }
			set { base[_propEmailClaimType] = value; }
		}

		/// <summary>
		/// The claim type of the 'name' claim.
		/// </summary>
		public string DisplayNameClaimType
		{
			get { return (string)base[_propDisplayNameClaimType]; }
			set { base[_propDisplayNameClaimType] = value; }
		}

		/// <summary>
		/// The query string name for the invitation code.
		/// </summary>
		public string InvitationCodeKey
		{
			get { return (string)base[_propInvitationCodeKey]; }
			set { base[_propInvitationCodeKey] = value; }
		}

		/// <summary>
		/// The query string name for the challenge answer.
		/// </summary>
		public string ChallengeAnswerKey
		{
			get { return (string)base[_propChallengeAnswerKey]; }
			set { base[_propChallengeAnswerKey] = value; }
		}

		/// <summary>
		/// The query string name for the return URL.
		/// </summary>
		public string ReturnUrlKey
		{
			get { return (string)base[_propReturnUrlKey]; }
			set { base[_propReturnUrlKey] = value; }
		}

		/// <summary>
		/// The query string name for the result code.
		/// </summary>
		public string ResultCodeKey
		{
			get { return (string)base[_propResultCodeKey]; }
			set { base[_propResultCodeKey] = value; }
		}

		/// <summary>
		/// The return path, used by the federation authentication handler, when no explicit path is provided.
		/// </summary>
		public string DefaultReturnPath
		{
			get { return (string)base[_propDefaultReturnPath]; }
			set { base[_propDefaultReturnPath] = value; }
		}

		/// <summary>
		/// The location the user is redirected to after a new contact registration is created. If a contact record is missing values for required fields and the 'requiredLevel' attribute is set to the "ApplicationRequired" value, then the user is redirected to this page after every sign-in. This redirect indicates a successful registration or sign-in. If no value is provided, the user is redirected to the 'defaultReturnPath'.
		/// </summary>
		public string ProfilePath
		{
			get { return (string)base[_propProfilePath]; }
			set { base[_propProfilePath] = value; }
		}

		/// <summary>
		/// The path to the registration page that renders the form for providing invitation code, challenge question/answer, and open registration details. A user is redirected here if he or she is signing into the portal but is not yet registered. In this case, the sign-in attempt has either failed or is still in progress. If no value is provided, the user is redirected to the 'defaultReturnPath'.
		/// </summary>
		public string RegistrationPath
		{
			get { return (string)base[_propRegistrationPath]; }
			set { base[_propRegistrationPath] = value; }
		}

		/// <summary>
		/// The location the user is redirected to after a email confirmation workflow is triggered to provide further instructions on completing the registration. If no value is provided, the user is redirected to the 'defaultReturnPath'.
		/// </summary>
		public string ConfirmationPath
		{
			get { return (string)base[_propConfirmationPath]; }
			set { base[_propConfirmationPath] = value; }
		}

		/// <summary>
		/// The location the user is redirected to when the federation authentication handler throws an exception. If no value is provided, the user is redirected to the 'defaultReturnPath'.
		/// </summary>
		public string ErrorPath
		{
			get { return (string)base[_propErrorPath]; }
			set { base[_propErrorPath] = value; }
		}

		/// <summary>
		/// When a confirmation email workflow is generated, this value specifies the duration that the invitation code is considered valid.
		/// </summary>
		public TimeSpan? InvitationCodeDuration
		{
			get { return (TimeSpan?)base[_propInvitationCodeDuration]; }
			set { base[_propInvitationCodeDuration] = value; }
		}

		/// <summary>
		/// Controls the level of validation when determining if a contact entity contains all the attribute values deemed required.
		/// </summary>
		public AttributeRequiredLevel RequiredLevel
		{
			get { return (AttributeRequiredLevel)base[_propRequiredLevel]; }
			set { base[_propRequiredLevel] = value; }
		}

		/// <summary>
		/// A filter for the set of attributes to be accepted as user sign-up attributes.
		/// </summary>
		public SignUpAttributeElementCollection SignUpAttributes
		{
			get { return (SignUpAttributeElementCollection)base[_propSignUpAttributes]; }
			set { base[_propSignUpAttributes] = value; }
		}

		/// <summary>
		/// A filter for the set of attributes to be accepted as user sign-up attributes.
		/// </summary>
		IEnumerable<string> IUserRegistrationSettings.SignUpAttributes
		{
			get { return SignUpAttributes.Cast<SignUpAttributeElement>().Select(attrib => attrib.LogicalName); }
		}
	}
}
