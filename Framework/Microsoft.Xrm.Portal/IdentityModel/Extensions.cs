/*
  Copyright (c) Microsoft Corporation. All rights reserved.
  Licensed under the MIT License. See License.txt in the project root for license information.
*/

using System.Collections.Generic;
using System.IdentityModel;
using System.Security.Claims;
using System.IdentityModel.Configuration;
using System.IdentityModel.Tokens;
using System.IdentityModel.Services;
using System.IdentityModel.Services.Configuration;

namespace Microsoft.Xrm.Portal.IdentityModel
{
	public static class Extensions
	{
		/// <summary>
		/// Reconfigures the service to use a custom service certificate for cookie tranformation instead of using DPAPI.
		/// Reconfigures the service to use the <see cref="ClaimTypes.NameIdentifier"/> claim as the default identity claim.
		/// </summary>
		public static void OnFederationConfigurationCreated(object sender, FederationConfigurationCreatedEventArgs args)
		{
			ConfigureServiceCertificateCookieTransform(sender, args);
			ConfigureNameIdentifierSecurityTokenHandlers(sender, args);
		}

		/// <summary>
		/// Reconfigures the service to use a custom service certificate for cookie tranformation instead of using DPAPI.
		/// </summary>
		public static void ConfigureServiceCertificateCookieTransform(object sender, FederationConfigurationCreatedEventArgs args)
		{
			ConfigureServiceCertificateCookieTransform(args.FederationConfiguration);
		}

		/// <summary>
		/// Reconfigures the service to use a custom service certificate for cookie tranformation instead of using DPAPI.
		/// </summary>
		public static void ConfigureServiceCertificateCookieTransform(this FederationConfiguration config)
		{
			if (config.ServiceCertificate != null)
			{
				// Use the <serviceCertificate> to protect the cookies that are sent to the client.

				var sessionTransforms = new List<CookieTransform>(new CookieTransform[]
				{
					new DeflateCookieTransform(),
					new RsaEncryptionCookieTransform(config.ServiceCertificate),
					new RsaSignatureCookieTransform(config.ServiceCertificate)
				});

				var sessionHandler = new System.IdentityModel.Tokens.SessionSecurityTokenHandler(sessionTransforms.AsReadOnly());

				config.IdentityConfiguration.SecurityTokenHandlers.AddOrReplace(sessionHandler);
			}
		}

		/// <summary>
		/// Reconfigures the service to use the <see cref="ClaimTypes.NameIdentifier"/> claim as the default identity claim.
		/// </summary>
		public static void ConfigureNameIdentifierSecurityTokenHandlers(object sender, FederationConfigurationCreatedEventArgs args)
		{
			ConfigureNameIdentifierSecurityTokenHandlers(args.FederationConfiguration);
		}

		/// <summary>
		/// Reconfigures the service to use the <see cref="ClaimTypes.NameIdentifier"/> claim as the default identity claim.
		/// </summary>
		public static void ConfigureNameIdentifierSecurityTokenHandlers(this FederationConfiguration config)
		{
			// configure the token handlers to use the NameIdentifier claim instead of the Name claim

			var saml11Handler = config.IdentityConfiguration.SecurityTokenHandlers[typeof(SamlSecurityToken)] as SamlSecurityTokenHandler;
			var saml2Handler = config.IdentityConfiguration.SecurityTokenHandlers[typeof(System.IdentityModel.Tokens.Saml2SecurityToken)] as System.IdentityModel.Tokens.Saml2SecurityTokenHandler;
			if (saml11Handler != null) saml11Handler.SamlSecurityTokenRequirement.NameClaimType = ClaimTypes.NameIdentifier;
			if (saml2Handler != null) saml2Handler.SamlSecurityTokenRequirement.NameClaimType = ClaimTypes.NameIdentifier;
		}
	}
}
