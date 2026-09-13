/*
  Copyright (c) Microsoft Corporation. All rights reserved.
  Licensed under the MIT License. See License.txt in the project root for license information.
*/

using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using ITfoxtec.Identity.Saml2.Schemas;
using Microsoft.Owin;
using Microsoft.Owin.Security;
using Microsoft.Owin.Security.WsFederation;

namespace Adxstudio.Xrm.Owin.Security.Saml2
{
	/// <summary>
	/// Configuration options for <see cref="Saml2AuthenticationMiddleware"/>
	/// </summary>
	public class Saml2AuthenticationOptions : WsFederationAuthenticationOptions
	{
		/// <summary>
		/// Requires users to reauthenticate at the identity provider and ignore any existing session.
		/// </summary>
		public bool? ForceAuthn { get; set; }

		/// <summary>
		/// Constraints on the name identifier to be used to represent the requested subject.
		/// </summary>
		public NameIdPolicy NameIdPolicy { get; set; }

		/// <summary>
		/// Specifies the comparison method used to evaluate the requested context classes or statements.
		/// </summary>
		public AuthnContextComparisonTypes? Comparison { get; set; }

		/// <summary>
		/// Specifies one or more URI references identifying authentication context classes or declarations.
		/// </summary>
		public IEnumerable<string> AuthnContextClassRef { get; set; }

		/// <summary>
		/// The endpoint for handling external logins which is required for IdP initiated login.
		/// </summary>
		public PathString ExternalLoginCallbackPath { get; set; }

		/// <summary>
		/// The reply path for handling logout requests from the IdP.
		/// </summary>
		public PathString SingleLogoutServiceRequestPath { get; set; }

		/// <summary>
		/// The reply path for handling logout responses from the IdP.
		/// </summary>
		public PathString SingleLogoutServiceResponsePath { get; set; }

		/// <summary>
		/// The certificate used to sign requests originating from the service provider and sent to the IdP. Must include a private key.
		/// </summary>
		public X509Certificate2 SigningCertificate { get; set; }

		public Saml2AuthenticationOptions()
			: base("Saml2Federation")
		{
			AuthenticationMode = AuthenticationMode.Passive;
			Caption = "SAML";
			NameIdPolicy = new NameIdPolicy { AllowCreate = true, Format = "urn:oasis:names:tc:SAML:2.0:nameid-format:persistent" };
			AuthnContextClassRef = new[] { AuthnContextClassTypes.PasswordProtectedTransport.OriginalString };
		}
	}
}
