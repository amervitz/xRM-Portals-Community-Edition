using System;
using System.Collections.Concurrent;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Adxstudio.Xrm.Configuration;
using Microsoft.Identity.Client;

namespace Adxstudio.Xrm.IdentityModel.ActiveDirectory
{
	/// <summary>Certificate-based token acquisition using MSAL.</summary>
	public static class AuthenticationExtensions
	{
		/// <summary>
		/// The confidential client applications, keyed by the identity they authenticate as.
		/// </summary>
		/// <remarks>
		/// ADAL's AuthenticationContext carried an in-memory token cache, so repeated acquisitions for
		/// the same identity were served from that cache rather than re-contacting the token endpoint.
		/// MSAL keeps its cache on the application object, so the applications must be retained for the
		/// cache to have any effect. Building a new one per acquisition would in particular re-redeem an
		/// authorization code that has already been spent, which Azure AD rejects as invalid_grant.
		/// </remarks>
		private static readonly ConcurrentDictionary<string, IConfidentialClientApplication> Clients =
			new ConcurrentDictionary<string, IConfidentialClientApplication>();

		/// <summary>
		/// The account identifiers produced by redeeming an authorization code, keyed by that code.
		/// </summary>
		/// <remarks>
		/// This only needs to outlive the retries of a single sign-in, so entries are dropped once the
		/// map grows past the number of sign-ins that could plausibly be in flight at the same time.
		/// </remarks>
		private static readonly ConcurrentDictionary<string, string> RedeemedAccounts =
			new ConcurrentDictionary<string, string>();

		/// <summary>
		/// The number of redeemed authorization codes to track before discarding them.
		/// </summary>
		private const int MaximumTrackedAuthorizationCodes = 1000;

		public static Task<AuthenticationResult> GetTokenAsync(this X509Certificate2 certificate, IAuthenticationSettings authenticationSettings, string resource)
		{
			return CreateClient(certificate, authenticationSettings)
				.AcquireTokenForClient(GetScopes(resource)).ExecuteAsync();
		}

		public static async Task<AuthenticationResult> GetTokenOnBehalfOfAsync(this X509Certificate2 certificate, IAuthenticationSettings authenticationSettings, string resource, string authorizationCode)
		{
			var client = CreateClient(certificate, authenticationSettings);
			var scopes = GetScopes(resource);

			// An authorization code may only be redeemed once. ADAL served a repeat acquisition for the
			// same code from its cache, which is what allowed callers to retry a failed Graph request by
			// discarding their token and asking for another. MSAL would instead redeem the spent code
			// again and Azure AD would reject it, so serve the retry from the account acquired the first
			// time and only fall back to redeeming the code when it has not been redeemed yet.
			var account = await GetRedeemedAccountAsync(client, authorizationCode).ConfigureAwait(false);

			if (account != null)
			{
				try
				{
					return await client.AcquireTokenSilent(scopes, account).ExecuteAsync().ConfigureAwait(false);
				}
				catch (MsalUiRequiredException)
				{
					// nothing cached for these scopes; fall through and redeem the code
				}
			}

			var result = await client.AcquireTokenByAuthorizationCode(scopes, authorizationCode).ExecuteAsync().ConfigureAwait(false);

			if (result != null && result.Account != null)
			{
				TrackRedeemedAccount(authorizationCode, result.Account.HomeAccountId?.Identifier);
			}

			return result;
		}

		/// <summary>
		/// Records the account that redeeming <paramref name="authorizationCode"/> produced.
		/// </summary>
		/// <param name="authorizationCode">The authorization code.</param>
		/// <param name="identifier">The resulting account identifier.</param>
		private static void TrackRedeemedAccount(string authorizationCode, string identifier)
		{
			// These entries are only consulted by the retries of the sign-in that produced them, so the
			// map is cleared wholesale rather than aged out once it grows beyond a plausible number of
			// concurrent sign-ins. Losing an entry costs a rejected retry, not a failed sign-in.
			if (RedeemedAccounts.Count >= MaximumTrackedAuthorizationCodes)
			{
				RedeemedAccounts.Clear();
			}

			RedeemedAccounts[authorizationCode] = identifier;
		}

		/// <summary>
		/// Retrieves the account that a previous redemption of <paramref name="authorizationCode"/> produced.
		/// </summary>
		/// <param name="client">The confidential client application.</param>
		/// <param name="authorizationCode">The authorization code.</param>
		/// <returns>The account, or null when the code has not been redeemed by this application.</returns>
		private static async Task<IAccount> GetRedeemedAccountAsync(IConfidentialClientApplication client, string authorizationCode)
		{
			string identifier;

			if (string.IsNullOrEmpty(authorizationCode) || !RedeemedAccounts.TryGetValue(authorizationCode, out identifier) || identifier == null)
			{
				return null;
			}

			return await client.GetAccountAsync(identifier).ConfigureAwait(false);
		}

		public static AuthenticationResult GetToken(this X509Certificate2 certificate, IAuthenticationSettings authenticationSettings, string resource)
		{
			return certificate.GetTokenAsync(authenticationSettings, resource).ConfigureAwait(false).GetAwaiter().GetResult();
		}

		public static AuthenticationResult GetTokenOnBehalfOf(this X509Certificate2 certificate, IAuthenticationSettings authenticationSettings, string resource, string authorizationCode)
		{
			return certificate.GetTokenOnBehalfOfAsync(authenticationSettings, resource, authorizationCode).ConfigureAwait(false).GetAwaiter().GetResult();
		}

		private static string[] GetScopes(string resource)
		{
			if (string.IsNullOrWhiteSpace(resource)) throw new ArgumentException("A token resource is required.", nameof(resource));
			return new[] { resource.TrimEnd('/') + "/.default" };
		}

		private static IConfidentialClientApplication CreateClient(X509Certificate2 certificate, IAuthenticationSettings settings)
		{
			if (certificate == null) throw new ArgumentNullException(nameof(certificate));
			if (settings == null) throw new ArgumentNullException(nameof(settings));
			var authority = settings.RootUrl.TrimEnd('/') + "/" + settings.TenantId;
			var key = string.Join("|", settings.ClientId, authority, certificate.Thumbprint, settings.RedirectUri);
			return Clients.GetOrAdd(key, _ =>
			{
				var builder = ConfidentialClientApplicationBuilder.Create(settings.ClientId)
					.WithAuthority(authority)
					.WithCertificate(certificate);
				if (!string.IsNullOrWhiteSpace(settings.RedirectUri)) builder = builder.WithRedirectUri(settings.RedirectUri);
				return builder.Build();
			});
		}
	}
}
