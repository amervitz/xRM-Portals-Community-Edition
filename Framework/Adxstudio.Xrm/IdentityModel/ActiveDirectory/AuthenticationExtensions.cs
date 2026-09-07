/*
  Copyright (c) Microsoft Corporation. All rights reserved.
  Licensed under the MIT License. See License.txt in the project root for license information.
*/

namespace Adxstudio.Xrm.IdentityModel.ActiveDirectory
{
	using System;
	using System.Security.Cryptography.X509Certificates;
	using System.Threading.Tasks;
	using Adxstudio.Xrm.Configuration;
	using Microsoft.IdentityModel.Clients.ActiveDirectory;

	/// <summary>
	/// Helpers related to token management.
	/// </summary>
	public static class AuthenticationExtensions
	{
		/// <summary>
		/// Retrieves a token from the certificate.
		/// </summary>
		/// <param name="certificate">The certificate.</param>
		/// <param name="authenticationSettings">The authentication settings.</param>
		/// <param name="resource">The target resource.</param>
		/// <returns>The token.</returns>
		public static async Task<AuthenticationResult> GetTokenAsync(this X509Certificate2 certificate, IAuthenticationSettings authenticationSettings, string resource)
		{
			var authenticationContext = GetAuthenticationContext(authenticationSettings);

			// Then create the certificate credential.
			var certificateCredential = new ClientAssertionCertificate(authenticationSettings.ClientId, certificate);

			// ADAL includes an in memory cache, so this call will only send a message to the server if the cached token is expired.
			var authResult = await authenticationContext.AcquireTokenAsync(resource, certificateCredential);

			return authResult;
		}

		/// <summary>
		/// Retrieves a token from the certificate for delegated auth
		/// </summary>
		/// <param name="certificate">The application's certificate</param>
		/// <param name="authenticationSettings">Authentication settings</param>
		/// <param name="resource">Requested resource</param>
		/// <param name="authorizationCode">Access code for user assertion</param>
		/// <returns>Authentication result including token</returns>
		public static async Task<AuthenticationResult> GetTokenOnBehalfOfAsync(this X509Certificate2 certificate, IAuthenticationSettings authenticationSettings, string resource, string authorizationCode)
		{
			var authenticationContext = GetAuthenticationContext(authenticationSettings);

			// Then create the certificate credential and user assertion.
			var certificateCredential = new ClientAssertionCertificate(authenticationSettings.ClientId, certificate);
			
			// ADAL includes an in memory cache, so this call will only send a message to the server if the cached token is expired.
			var authResult = await authenticationContext.AcquireTokenByAuthorizationCodeAsync(
				authorizationCode,
				new Uri(authenticationSettings.RedirectUri),
				certificateCredential,
				resource);

			return authResult;
		}

				/// <summary>
		/// Retrieves a token from the certificate.
		/// </summary>
		/// <param name="certificate">The certificate.</param>
		/// <param name="authenticationSettings">The authentication settings.</param>
		/// <param name="resource">The target resource.</param>
		/// <returns>The token.</returns>
		public static AuthenticationResult GetToken(this X509Certificate2 certificate, IAuthenticationSettings authenticationSettings, string resource)
		{
			var authenticationContext = GetAuthenticationContext(authenticationSettings);

			// Then create the certificate credential.
			var certificateCredential = new ClientAssertionCertificate(authenticationSettings.ClientId, certificate);

			// ADAL includes an in memory cache, so this call will only send a message to the server if the cached token is expired.
			var authResult = authenticationContext.AcquireTokenAsync(resource, certificateCredential).GetAwaiter().GetResult();

			return authResult;
		}

		/// <summary>
		/// Retrieves a token from the certificate for delegated auth
		/// </summary>
		/// <param name="certificate">The application's certificate</param>
		/// <param name="authenticationSettings">Authentication settings</param>
		/// <param name="resource">Requested resource</param>
		/// <param name="authorizationCode">Access code for user assertion</param>
		/// <returns>Authentication result including token</returns>
		public static AuthenticationResult GetTokenOnBehalfOf(this X509Certificate2 certificate, IAuthenticationSettings authenticationSettings, string resource, string authorizationCode)
		{
			var authenticationContext = GetAuthenticationContext(authenticationSettings);

			// Then create the certificate credential and user assertion.
			var certificateCredential = new ClientAssertionCertificate(authenticationSettings.ClientId, certificate);
			
			// ADAL includes an in memory cache, so this call will only send a message to the server if the cached token is expired.
			var authResult = authenticationContext
				.AcquireTokenByAuthorizationCodeAsync(
					authorizationCode,
					new Uri(authenticationSettings.RedirectUri),
					certificateCredential,
					resource)
				.GetAwaiter()
				.GetResult();

			return authResult;
		}

		/// <summary>
		/// Creates an authentication context.
		/// </summary>
		/// <param name="authenticationSettings">The authentication settings.</param>
		/// <returns>The context.</returns>
		private static AuthenticationContext GetAuthenticationContext(IAuthenticationSettings authenticationSettings)
		{
			return new AuthenticationContext(string.Format("{0}/{1}", authenticationSettings.RootUrl, authenticationSettings.TenantId));
		}
	}
}
