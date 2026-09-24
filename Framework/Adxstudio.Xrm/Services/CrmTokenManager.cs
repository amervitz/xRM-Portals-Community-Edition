/*
  Copyright (c) Microsoft Corporation. All rights reserved.
  Licensed under the MIT License. See License.txt in the project root for license information.
*/

namespace Adxstudio.Xrm.Services
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Security.Cryptography.X509Certificates;
	using System.Threading.Tasks;
	using Adxstudio.Xrm.Configuration;
	using Adxstudio.Xrm.IdentityModel.ActiveDirectory;
	using Adxstudio.Xrm.Performance;
	using Microsoft.Identity.Client;

	/// <summary>
	///  CrmTokenManager class
	/// </summary>
	public class CrmTokenManager : ICrmTokenManager
	{
		/// <summary>
		/// A record of <see cref="AuthenticationResult"/> and <see cref="Exception"/>.
		/// </summary>
		private struct AcquireTokenResult
		{
			/// <summary>
			/// The success result.
			/// </summary>
			public AuthenticationResult Token;

			/// <summary>
			/// The error result.
			/// </summary>
			public Exception Error;
		}

		/// <summary>
		/// Internal cache.
		/// </summary>
		private AuthenticationResult tokenCache;

		/// <summary>
		/// The authentication settings.
		/// </summary>
		public IAuthenticationSettings AuthenticationSettings { get; }

		/// <summary>
		/// The certificate settings.
		/// </summary>
		public ICertificateSettings CertificateSettings { get; }

		/// <summary>
		/// The resource.
		/// </summary>
		public string Resource { get; }

		/// <summary>
		///  Initializes a new instance of the <see cref="CrmTokenManager" /> class.
		/// </summary>
		/// <param name="authenticationSettings">The authentication settings.</param>
		/// <param name="certificateSettings">The certificat settings.</param>
		/// <param name="resource">The resource.</param>
		public CrmTokenManager(IAuthenticationSettings authenticationSettings, ICertificateSettings certificateSettings, string resource)
		{
			this.AuthenticationSettings = authenticationSettings;
			this.CertificateSettings = certificateSettings;
			this.Resource = resource;
		}

		/// <summary>
		/// Gets or refreshes token
		/// </summary>
		/// <param name="authorizationCode">Access code for user assertion.</param>
		/// <param name="test">An opportunity to check the validity of the token.</param>
		/// <returns>Type: Returns_String Access token</returns>
		public async Task<string> GetTokenAsync(string authorizationCode = null, Func<AuthenticationResult, Exception> test = null)
		{
			if (this.Resource == null)
			{
				return null;
			}

			var token = await this.GetAuthenticationResultAsync(authorizationCode, test);
			return token != null ? token.AccessToken : null;
		}

		/// <summary>
		/// Gets or refreshes token
		/// </summary>
		/// <param name="authorizationCode">Access code for user assertion.</param>
		/// <param name="test">An opportunity to check the validity of the token.</param>
		/// <returns>Type: Returns_String Access token</returns>
		public string GetToken(string authorizationCode = null, Func<AuthenticationResult, Exception> test = null)
		{
			if (this.Resource == null)
			{
				return null;
			}

			var token = this.GetAuthenticationResult(authorizationCode, test);
			return token != null ? token.AccessToken : null;
		}

		/// <summary>
		/// Resets the manager.
		/// </summary>
		public void Reset()
		{
			this.tokenCache = null;
		}

		/// <summary>
		/// Converts to an AcquireTokenResult.
		/// </summary>
		/// <param name="token">The authentication token.</param>
		/// <param name="test">An opportunity to check the validity of the token.</param>
		/// <param name="index">The current certificate index.</param>
		/// <param name="errors">The current errors.</param>
		/// <returns>The AcquireTokenResult.</returns>
		private static AcquireTokenResult GetAcquireTokenResult(AuthenticationResult token, Func<AuthenticationResult, Exception> test, int index, ICollection<Exception> errors)
		{
			if (token == null || string.IsNullOrWhiteSpace(token.AccessToken))
			{
				throw new PortalAdalServiceException("Token is empty.");
			}

			ADXTrace.Instance.TraceInfo(TraceCategory.Application, string.Format("Get token succeeded: index: {0}", index));

			if (test != null)
			{
				var testError = test(token);

				if (testError != null)
				{
					ADXTrace.Instance.TraceWarning(TraceCategory.Application, string.Format("Test token failed: index: {0}: {1}", index, testError));

					errors.Add(testError);
				}
			}

			// return the valid token and clear the error
			return new AcquireTokenResult { Token = token, Error = null };
		}

		/// <summary>
		/// Process errors.
		/// </summary>
		/// <param name="e">The current error.</param>
		/// <param name="index">The current certificate index.</param>
		/// <param name="errors">The current errors.</param>
		/// <param name="result">The result when returning early.</param>
		/// <returns>'true' to return the result early.</returns>
		private static bool TryHandleError(Exception e, int index, ICollection<Exception> errors, out AcquireTokenResult result)
		{
			// record the error of the failed certificate
			ADXTrace.Instance.TraceWarning(TraceCategory.Application, string.Format("Get token failed: index: {0}: {1}", index, e));

			var ase = e as MsalServiceException;

			if (ase != null)
			{
				if (ase.ErrorCode == "AADSTS50001")
				{
					// return fatal error immediately
					var aseError = new PortalAdalServiceException("CRM is disabled.", ase);
					result = new AcquireTokenResult { Token = null, Error = aseError };
					return true;
				}

				errors.Add(new PortalAdalServiceException("Error Connecting to CRM.", ase));
			}
			else
			{
				errors.Add(e);
			}

			result = default(AcquireTokenResult);
			return false;
		}

		/// <summary>
		/// Check if the current token is invalid.
		/// </summary>
		/// <returns>'true' if the token is invalid.</returns>
		private bool TokenIsInvalid()
		{
			return this.tokenCache == null || this.tokenCache.ExpiresOn.Subtract(this.AuthenticationSettings.TokenRefreshWindow) <= DateTimeOffset.UtcNow;
		}

		/// <summary>
		///  Gets or Refreshes the token
		/// </summary>
		/// <param name="authorizationCode">The access code for user assertion.</param>
		/// <param name="test">An opportunity to check the validity of the token.</param>
		/// <returns>Type: Return_AuthenticationResult</returns>
		private async Task<AuthenticationResult> GetAuthenticationResultAsync(string authorizationCode, Func<AuthenticationResult, Exception> test)
		{
			if (this.TokenIsInvalid())
			{
				ADXTrace.Instance.TraceInfo(TraceCategory.Application, string.Format("Get new token required due to: {0}", this.tokenCache == null ? "null token" : "expiring token"));

				using (PerformanceProfiler.Instance.StartMarker(PerformanceMarkerName.Services, PerformanceMarkerArea.Crm, PerformanceMarkerTagName.GetToken))
				{
					// filter out empty thumbprints for the certificate fallback logic to work correctly
					var certificates = this.CertificateSettings.FindCertificates().ToList();

					if (!certificates.Any())
					{
						throw new CertificateNotFoundException();
					}

					var result = await this.GetAuthenticationResultAsync(certificates, authorizationCode, test);

					this.ApplyAuthenticationResult(result);
				}
			}

			return this.tokenCache;
		}

		/// <summary>
		/// Retrieve a token.
		/// </summary>
		/// <param name="certificates">The certificates.</param>
		/// <param name="authorizationCode">Optional user's access code for user assertion.</param>
		/// <param name="test">An opportunity to check the validity of the token.</param>
		/// <returns>The token.</returns>
		private async Task<AcquireTokenResult> GetAuthenticationResultAsync(IEnumerable<X509Certificate2> certificates, string authorizationCode, Func<AuthenticationResult, Exception> test)
		{
			var index = 0;
			var errors = new List<Exception>();

			foreach (var certificate in certificates)
			{
				try
				{
					var token = string.IsNullOrEmpty(authorizationCode)
						? await certificate.GetTokenAsync(this.AuthenticationSettings, this.Resource)
						: await certificate.GetTokenOnBehalfOfAsync(this.AuthenticationSettings, this.Resource, authorizationCode);

					return GetAcquireTokenResult(token, test, index, errors);
				}
				catch (Exception e)
				{
					AcquireTokenResult result;

					if (TryHandleError(e, index, errors, out result))
					{
						return result;
					}
				}

				++index;
			}

			// all certificates are invalid
			var error = new AggregateException(errors);
			return new AcquireTokenResult { Token = null, Error = error };
		}

		/// <summary>
		///  Gets or Refreshes the token
		/// </summary>
		/// <param name="authorizationCode">The access code for user assertion.</param>
		/// <param name="test">An opportunity to check the validity of the token.</param>
		/// <returns>Type: Return_AuthenticationResult</returns>
		private AuthenticationResult GetAuthenticationResult(string authorizationCode, Func<AuthenticationResult, Exception> test)
		{
			if (this.TokenIsInvalid())
			{
				ADXTrace.Instance.TraceInfo(TraceCategory.Application, string.Format("Get new token required due to: {0}", this.tokenCache == null ? "null token" : "expiring token"));

				using (PerformanceProfiler.Instance.StartMarker(PerformanceMarkerName.Services, PerformanceMarkerArea.Crm, PerformanceMarkerTagName.GetToken))
				{
					// filter out empty thumbprints for the certificate fallback logic to work correctly
					var certificates = this.CertificateSettings.FindCertificates().ToList();

					if (!certificates.Any())
					{
						throw new CertificateNotFoundException();
					}

					var result = this.GetAuthenticationResult(certificates, authorizationCode, test);

					this.ApplyAuthenticationResult(result);
				}
			}

			return this.tokenCache;
		}

		/// <summary>
		/// Retrieve a token.
		/// </summary>
		/// <param name="certificates">The certificates.</param>
		/// <param name="authorizationCode">Optional user's access code for user assertion.</param>
		/// <param name="test">An opportunity to check the validity of the token.</param>
		/// <returns>The token.</returns>
		private AcquireTokenResult GetAuthenticationResult(IEnumerable<X509Certificate2> certificates, string authorizationCode, Func<AuthenticationResult, Exception> test)
		{
			var index = 0;
			var errors = new List<Exception>();

			foreach (var certificate in certificates)
			{
				try
				{
					var token = string.IsNullOrEmpty(authorizationCode)
						? certificate.GetToken(this.AuthenticationSettings, this.Resource)
						: certificate.GetTokenOnBehalfOf(this.AuthenticationSettings, this.Resource, authorizationCode);

					return GetAcquireTokenResult(token, test, index, errors);
				}
				catch (Exception e)
				{
					AcquireTokenResult result;

					if (TryHandleError(e, index, errors, out result))
					{
						return result;
					}
				}

				++index;
			}

			// all certificates are invalid
			var error = new AggregateException(errors);
			return new AcquireTokenResult { Token = null, Error = error };
		}

		/// <summary>
		/// Updates the cached token.
		/// </summary>
		/// <param name="result">The new token.</param>
		private void ApplyAuthenticationResult(AcquireTokenResult result)
		{
			this.tokenCache = result.Token;

			if (result.Error != null)
			{
				throw result.Error;
			}

			if (this.tokenCache != null)
			{
				var duration = this.tokenCache.ExpiresOn - DateTimeOffset.UtcNow;
				ADXTrace.Instance.TraceInfo(TraceCategory.Application, string.Format("New token expires on: {0}: after: {1}", this.tokenCache.ExpiresOn, duration));
			}
		}
	}
}
