using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Graph;
using Microsoft.Kiota.Abstractions.Authentication;

namespace Adxstudio.Xrm.IdentityModel.ActiveDirectory
{
	public static class PortalGraphClient
	{
		public static GraphServiceClient Create(string graphRoot, Func<Task<string>> acquireToken)
		{
			var root = new Uri(graphRoot.TrimEnd('/') + "/v1.0");
			if (root.Scheme != Uri.UriSchemeHttps) throw new ArgumentException("Microsoft Graph requires HTTPS.", nameof(graphRoot));
			return new GraphServiceClient(new BaseBearerTokenAuthenticationProvider(new PortalTokenProvider(root.Host, acquireToken)), root.AbsoluteUri);
		}

		private sealed class PortalTokenProvider : IAccessTokenProvider
		{
			private readonly Func<Task<string>> acquireToken;
			public AllowedHostsValidator AllowedHostsValidator { get; }

			public PortalTokenProvider(string host, Func<Task<string>> acquireToken)
			{
				this.acquireToken = acquireToken ?? throw new ArgumentNullException(nameof(acquireToken));
				AllowedHostsValidator = new AllowedHostsValidator(new[] { host });
			}

			public Task<string> GetAuthorizationTokenAsync(Uri uri, Dictionary<string, object> additionalAuthenticationContext = null, CancellationToken cancellationToken = default(CancellationToken))
			{
				cancellationToken.ThrowIfCancellationRequested();
				return AllowedHostsValidator.IsUrlHostValid(uri) && uri.Scheme == Uri.UriSchemeHttps
					? acquireToken() : Task.FromResult(string.Empty);
			}
		}
	}
}
