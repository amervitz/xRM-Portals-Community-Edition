/*
  Copyright (c) Microsoft Corporation. All rights reserved.
  Licensed under the MIT License. See License.txt in the project root for license information.
*/

using System;
using System.Configuration;
using Microsoft.Xrm.Client;
using Microsoft.Xrm.Portal.Configuration;

namespace Microsoft.Xrm.Portal.IdentityModel.Configuration
{
	/// <summary>
	/// Methods for retrieving dependencies from the configuration.
	/// </summary>
	/// <remarks>
	/// Format of configuration.
	/// <code>
	/// <![CDATA[
	/// <configuration>
	/// 
	///  <configSections>
	///   <section name="microsoft.identityModel" type="Microsoft.IdentityModel.Configuration.MicrosoftIdentityModelSection, Microsoft.IdentityModel, Version=3.5.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35" />
	///   <section name="microsoft.xrm.portal.identityModel" type="Microsoft.Xrm.Portal.IdentityModel.Configuration.IdentityModelSection, Microsoft.Xrm.Portal"/>
	///  </configSections>
	/// 
	///  <appSettings>
	///    <add key="FederationMetadataLocation" value="https://contoso.accesscontrol.windows.net/FederationMetadata/2007-06/FederationMetadata.xml" />
	///  </appSettings>
	/// 
	///  <system.web>
	/// 
	///   <compilation>
	///    <assemblies>
	///     <add assembly="Microsoft.IdentityModel, Version=3.5.0.0, Culture=neutral, PublicKeyToken=31BF3856AD364E35" />
	///    </assemblies>
	///   </compilation>
	/// 
	///   <authentication mode="None" />
	/// 
	///   <httpRuntime requestValidationType="Microsoft.Xrm.Portal.IdentityModel.Web.FederationRequestValidator, Microsoft.Xrm.Portal" />
	/// 
	///   <httpModules>
	///     <add name="SessionAuthenticationModule" type="Microsoft.IdentityModel.Web.SessionAuthenticationModule, Microsoft.IdentityModel, Version=3.5.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35" />
	///   </httpModules>
	/// 
	///   <httpHandlers>
	///    <add path="Federation.axd" verb="*" type="Microsoft.Xrm.Portal.IdentityModel.Web.Handlers.FederationAuthenticationHandler, Microsoft.Xrm.Portal" />
	///   </httpHandlers>
	/// 
	///   <pages>
	///    <controls>
	///     <add tagPrefix="wif" namespace="Microsoft.IdentityModel.Web.Controls" assembly="Microsoft.IdentityModel, Version=3.5.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35"/>
	///    </controls>
	///   </pages>
	/// 
	///  </system.web>
	/// 
	///  <system.webServer>
	/// 
	///   <modules runAllManagedModulesForAllRequests="true">
	///    <add name="SessionAuthenticationModule" type="Microsoft.IdentityModel.Web.SessionAuthenticationModule, Microsoft.IdentityModel, Version=3.5.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35" preCondition="managedHandler" />
	///   </modules>
	/// 
	///   <handlers>
	///    <add name="Federation" verb="*" path="Federation.axd" preCondition="integratedMode" type="Microsoft.Xrm.Portal.IdentityModel.Web.Handlers.FederationAuthenticationHandler, Microsoft.Xrm.Portal" />
	///   </handlers>
	/// 
	///  </system.webServer>
	/// 
	///  <microsoft.identityModel>
	///   <service>
	///    <audienceUris>
	///     <add value="http://contoso.cloudapp.net/" />
	///    </audienceUris>
	///    <federatedAuthentication>
	///     <wsFederation passiveRedirectEnabled="false" issuer="https://contoso.accesscontrol.windows.net/v2/wsfederation" realm="http://contoso.cloudapp.net/" requireHttps="false" />
	///     <cookieHandler requireSsl="false" />
	///    </federatedAuthentication>
	///    <applicationService>
	///     <claimTypeRequired>
	///      <claimType type="http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier" optional="true" />
	///      <claimType type="http://schemas.microsoft.com/accesscontrolservice/2010/07/claims/identityprovider" optional="true" />
	///     </claimTypeRequired>
	///    </applicationService>
	///    <issuerNameRegistry type="Microsoft.IdentityModel.Tokens.ConfigurationBasedIssuerNameRegistry, Microsoft.IdentityModel, Version=3.5.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35">
	///     <trustedIssuers>
	///      <add thumbprint="0000000000000000000000000000000000000000" name="https://contoso.accesscontrol.windows.net/" />
	///     </trustedIssuers>
	///    </issuerNameRegistry>
	///    <certificateValidation certificateValidationMode="None" />
	///    <serviceCertificate>
	///     <certificateReference x509FindType="FindByThumbprint" findValue="0000000000000000000000000000000000000000"/>
	///    </serviceCertificate>
	///    <securityTokenHandlers>
	///     <remove type="Microsoft.IdentityModel.Tokens.Saml11.Saml11SecurityTokenHandler, Microsoft.IdentityModel, Version=3.5.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35" />
	///      <add type="Microsoft.IdentityModel.Tokens.Saml11.Saml11SecurityTokenHandler, Microsoft.IdentityModel, Version=3.5.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35">
	///       <samlSecurityTokenRequirement>
	///        <nameClaimType value="http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier" />
	///       </samlSecurityTokenRequirement>
	///      </add>
	///      <remove type="Microsoft.IdentityModel.Tokens.Saml2.Saml2SecurityTokenHandler, Microsoft.IdentityModel, Version=3.5.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35" />
	///      <add type="Microsoft.IdentityModel.Tokens.Saml2.Saml2SecurityTokenHandler, Microsoft.IdentityModel, Version=3.5.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35">
	///       <samlSecurityTokenRequirement>
	///        <nameClaimType value="http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier" />
	///       </samlSecurityTokenRequirement>
	///      </add>
	///    </securityTokenHandlers>
	///   </service>
	///  </microsoft.identityModel>
	/// 
	///  <microsoft.xrm.portal.identityModel
	///   sectionProviderType="Microsoft.Xrm.Portal.IdentityModel.Configuration.FederationCrmConfigurationProvider, Microsoft.Xrm.Portal">
	///   <registration
	///    portalName="Xrm"
	///    enabled="true" [false|true]
	///    requiresInvitation="true" [false|true]
	///    requiresChallengeAnswer="true" [false|true]
	///    requiresConfirmation="true" [false|true]
	///    invitationCodeDuration="" [dd.HH:MM:SS]
	///    requiredLevel="ApplicationRequired" [None|Recommended|ApplicationRequired|SystemRequired]
	///    attributeMapInvitationCode="adx_invitationcode"
	///    attributeMapInvitationCodeExpiryDate="adx_invitationcodeexpirydate"
	///    attributeMapChallengeAnswer="adx_passwordanswer"
	///    attributeMapLogonEnabled="adx_logonenabled"
	///    attributeMapEmail="emailaddress1"
	///    attributeMapDisplayName="lastname"
	///    attributeMapLastSuccessfulLogon="adx_lastsuccessfullogon"
	///    attributeMapIdentityProvider="adx_identityprovider"
	///    emailClaimType="http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress"
	///    displayNameClaimType="http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name"
	///    returnUrlKey="returnurl"
	///    invitationCodeKey="invitation"
	///    challengeAnswerKey="answer"
	///    resultCodeKey="result-code"
	///    defaultReturnPath="~/"
	///    profilePath="" [~/profile]
	///    registrationPath="" [~/register]
	///    confirmationPath="" [~/register?result-code=confirm]
	///    errorPath=""
	///    />
	///  </microsoft.xrm.portal.identityModel>
	/// 
	/// </configuration>
	/// ]]>
	/// </code>
	/// The following are examples of various user registration scenarios.
	/// <example>
	/// Requiring invitation code and challenge question/answer:
	/// In this scenario, a contact entity is created with an invitation code specified but the username and logon enabled flag is left empty. The invitation code expiry may be optionally set. The invitation code is sent to the user through email. A value for the challenge password and answer must also be provided. The answer is a value that the user either implicitly possesses or is given through a channel separate from the invitation code delivery. If a user is missing a value for the challenge answer, the federation authentication handle will throw an exception and redirect to the error page.
	/// <code>
	/// <![CDATA[
	/// <configuration>
	/// 
	///  <microsoft.xrm.portal.identityModel>
	///   <registration
	///    requiresInvitation="true"
	///    requiresChallengeAnswer="true"
	///    profilePath="~/profile"
	///    registrationPath="~/register"
	///    />
	///  </microsoft.xrm.portal.identityModel>
	/// 
	/// </configuration>
	/// ]]>
	/// </code>
	/// </example>
	/// <example>
	/// Email confirmation:
	/// These scenarios do not require a contact entity to be created prior to user registration. The user initiates the registration by providing an email address to the portal. The portal creates a contact entity and generates an invitation code. The invitation code is sent to the user manually or through a workflow. At this point, the contact entity has a username and invitation code but the logon enabled flag remains disabled.
	/// <code>
	/// <![CDATA[
	/// <configuration>
	/// 
	///  <microsoft.xrm.portal.identityModel>
	///   <registration
	///    requiresInvitation="false"
	///    requiresChallengeAnswer="false"
	///    requiresConfirmation="true"
	///    profilePath="~/profile"
	///    registrationPath="~/register"
	///    />
	///  </microsoft.xrm.portal.identityModel>
	/// 
	/// </configuration>
	/// ]]>
	/// </code>
	/// </example>
	/// <example>
	/// Open registration:
	/// This scenario does not perform validation checks and allows all incoming user accounts to be automatically registered. A contact entity is created with the provided username and the logon enabled flag is enabled. If a display name and email address is provided by the identity provider, then the contact's last name and email are populated as well.
	/// <code>
	/// <![CDATA[
	/// <configuration>
	/// 
	///  <microsoft.xrm.portal.identityModel>
	///   <registration
	///    requiresInvitation="false"
	///    requiresChallengeAnswer="false"
	///    requiresConfirmation="false"
	///    profilePath="~/profile"
	///    registrationPath="~/register"
	///    />
	///  </microsoft.xrm.portal.identityModel>
	/// 
	/// </configuration>
	/// ]]>
	/// </code>
	/// </example>
	/// </remarks>
	/// <seealso cref="IUserRegistrationSettings"/>
	public static class FederationCrmConfigurationManager
	{
		private static Lazy<FederationCrmConfigurationProvider> _provider = new Lazy<FederationCrmConfigurationProvider>(CreateProvider);

		private static FederationCrmConfigurationProvider CreateProvider()
		{
			var section = ConfigurationManager.GetSection(IdentityModelSection.SectionName) as IdentityModelSection ?? new IdentityModelSection();

			if (!string.IsNullOrWhiteSpace(section.ConfigurationProviderType))
			{
				var typeName = section.ConfigurationProviderType;
				var type = TypeExtensions.GetType(typeName);

				if (type == null || !type.IsA<FederationCrmConfigurationProvider>())
				{
					throw new ConfigurationErrorsException("The value '{0}' is not recognized as a valid type or is not of the type '{1}'.".FormatWith(typeName, typeof(FederationCrmConfigurationProvider)));
				}

				return Activator.CreateInstance(type) as FederationCrmConfigurationProvider;
			}

			return new FederationCrmConfigurationProvider();
		}

		/// <summary>
		/// Resets the cached dependencies.
		/// </summary>
		public static void Reset()
		{
			_provider = new Lazy<FederationCrmConfigurationProvider>(CreateProvider);
		}

		/// <summary>
		/// Retrieves the configured user registration settings.
		/// </summary>
		/// <param name="portalName"></param>
		/// <returns></returns>
		public static IUserRegistrationSettings GetUserRegistrationSettings(string portalName = null)
		{
			return _provider.Value.GetUserRegistrationSettings(portalName);
		}

		/// <summary>
		/// Retrieves the metadata values needed to retrieve a user entity.
		/// </summary>
		/// <param name="portalName"></param>
		/// <returns></returns>
		public static IUserResolutionSettings GetUserResolutionSettings(string portalName = null)
		{
			return _provider.Value.GetUserResolutionSettings(portalName);
		}
	}
}
