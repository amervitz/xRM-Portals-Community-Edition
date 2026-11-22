/*
  Copyright (c) Microsoft Corporation. All rights reserved.
  Licensed under the MIT License. See License.txt in the project root for license information.
*/

using System;
using System.Collections.Concurrent;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Adxstudio.Xrm.Configuration;
using Microsoft.Identity.Client;
using Microsoft.SharePoint.Client;

namespace Adxstudio.SharePoint
{
	/// <summary>
	/// A <see cref="ClientContext"/> generator with support for SharePoint Online connections.
	/// </summary>
	public class ClientFactory
	{
		private class SharePointWebClient : WebClient
		{
			private readonly Action<WebRequest> _onGetWebRequest;

			public SharePointWebClient(Action<WebRequest> onGetWebRequest)
			{
				if (onGetWebRequest == null) throw new ArgumentNullException("onGetWebRequest");

				_onGetWebRequest = onGetWebRequest;
			}

			protected override WebRequest GetWebRequest(Uri address)
			{
				var request = base.GetWebRequest(address);

				_onGetWebRequest(request);

				return request;
			}
		}

		private static readonly ConcurrentDictionary<(string Authority, string ClientId, string Thumbprint), Lazy<IConfidentialClientApplication>> OnlineApplications =
			new ConcurrentDictionary<(string Authority, string ClientId, string Thumbprint), Lazy<IConfidentialClientApplication>>();

		/// <summary>
		/// Creates a context based on connection settings.
		/// </summary>
		public virtual ClientContext CreateClientContext(SharePointConnection connection)
		{
			if (connection == null) throw new ArgumentNullException("connection");

			var online = IsOnline(connection);
			var context = connection.CreateClientContext(useConnectionCredentials: !online);
			try
			{
				if (online) UpdateClientContextForOnline(connection, context);
				return context;
			}
			catch
			{
				context.Dispose();
				throw;
			}
		}

		/// <summary>
		/// Creates a web client based on connection settings.
		/// </summary>
		public virtual WebClient CreateWebClient(SharePointConnection connection)
		{
			if (connection == null) throw new ArgumentNullException("connection");

			var client = new SharePointWebClient(request => UpdateWebRequest(connection, request))
			{
				BaseAddress = connection.Url.OriginalString + (!connection.Url.OriginalString.EndsWith("/") ? "/" : string.Empty)
			};

			return client;
		}

		/// <summary>
		/// Creates a web request based on connection settings.
		/// </summary>
		public virtual WebRequest CreateHttpWebRequest(SharePointConnection connection, Uri uri)
		{
			if (connection == null) throw new ArgumentNullException("connection");

			var request = WebRequest.Create(uri);

			UpdateWebRequest(connection, request);

			return request;
		}

		protected virtual bool IsOnline(SharePointConnection connection)
		{
			return connection.UseEntraAppOnly;
		}

		protected virtual void UpdateClientContextForOnline(SharePointConnection connection, ClientContext context)
		{
			context.ExecutingWebRequest += (sender, args) =>
				UpdateHttpWebRequestForOnline(connection, args.WebRequestExecutor.WebRequest);
		}

		internal HttpWebRequest CreateFileDownloadRequest(SharePointConnection connection, Uri fileUrl)
		{
			if (IsOnline(connection))
			{
				EnsureOnlineRequestOrigin(connection.Url, fileUrl);
				// SharePoint REST serves file content to OAuth clients.
				var decodedPath = Uri.UnescapeDataString(fileUrl.AbsolutePath).Replace("'", "''");
				var siteUrl = connection.Url.GetLeftPart(UriPartial.Path).TrimEnd('/');
				fileUrl = new Uri(siteUrl + "/_api/web/GetFileByServerRelativePath(decodedUrl='" +
					Uri.EscapeDataString(decodedPath) + "')/$value");
			}

			return (HttpWebRequest)CreateHttpWebRequest(connection, fileUrl);
		}

		protected virtual void UpdateWebRequest(SharePointConnection connection, WebRequest request)
		{
			if (connection.RequestTimeout != null) request.Timeout = connection.RequestTimeout.Value;

			var httpRequest = request as HttpWebRequest;

			if (httpRequest == null) return;

			if (IsOnline(connection))
			{
				UpdateHttpWebRequestForOnline(connection, httpRequest);
			}
			else
			{
				httpRequest.Credentials = connection.Credentials;

				// Prevent a forms sign-in redirect when downloading with Windows credentials.
				httpRequest.Headers.Add("X-FORMS_BASED_AUTH_ACCEPTED", "f");
			}
		}

		protected virtual void UpdateHttpWebRequestForOnline(SharePointConnection connection, HttpWebRequest request)
		{
			EnsureOnlineRequestOrigin(connection.Url, request.RequestUri);
			request.AllowAutoRedirect = false;
			request.Headers[HttpRequestHeader.Authorization] = "Bearer " + GetOnlineAccessToken(connection);
		}
		private static void EnsureOnlineRequestOrigin(Uri siteUrl, Uri requestUrl)
		{
			if (siteUrl.Scheme != Uri.UriSchemeHttps || requestUrl.Scheme != Uri.UriSchemeHttps ||
				!siteUrl.IdnHost.Equals(requestUrl.IdnHost, StringComparison.OrdinalIgnoreCase) ||
				siteUrl.Port != requestUrl.Port || siteUrl.UserInfo.Length != 0 || requestUrl.UserInfo.Length != 0)
			{
				throw new ConfigurationErrorsException("SharePoint Online requests must use the configured SharePoint site's HTTPS origin.");
			}
		}

		/// <summary>Gets an application access token for the configured SharePoint Online origin.</summary>
		protected virtual string GetOnlineAccessToken(SharePointConnection connection)
		{
			var tenantId = ReadAppId(connection.TenantId, "TenantId");
			var clientId = ReadAppId(connection.ClientId, "ClientId");
			var authority = GetOnlineAuthority(connection.Authority, tenantId);
			if (string.IsNullOrWhiteSpace(connection.CertificateThumbprint))
			{
				throw new ConfigurationErrorsException("Set CertificateThumbprint in the SharePoint connection string.");
			}

			var appKey = (authority, clientId, connection.CertificateThumbprint.Trim().ToUpperInvariant());
			var application = OnlineApplications.GetOrAdd(appKey, key =>
				new Lazy<IConfidentialClientApplication>(() => CreateOnlineApplication(key)));
			var scopes = new[] { connection.Url.GetLeftPart(UriPartial.Authority) + "/.default" };
			// The CSOM request event and the download handler are synchronous. Start MSAL off
			// the ASP.NET request context so continuation work cannot block on that context.
			return Task.Run(async () =>
			{
				var result = await application.Value.AcquireTokenForClient(scopes).ExecuteAsync().ConfigureAwait(false);
				return result.AccessToken;
			}).GetAwaiter().GetResult();
		}

		private static IConfidentialClientApplication CreateOnlineApplication((string Authority, string ClientId, string Thumbprint) key)
		{
			try
			{
				var condition = new X509FindCondition(X509FindType.FindByThumbprint, key.Thumbprint);
				var certificate = new[] { condition }.FindCertificates(StoreLocation.LocalMachine)
					.Cast<X509Certificate2>()
					.FirstOrDefault(cert => cert.HasPrivateKey && cert.NotBefore <= DateTime.Now && cert.NotAfter > DateTime.Now);
				if (certificate == null)
				{
					throw new ConfigurationErrorsException("The SharePoint Online certificate must be valid and have a private key in LocalMachine/My.");
				}

				return ConfidentialClientApplicationBuilder.Create(key.ClientId)
					.WithAuthority(key.Authority)
					.WithCertificate(certificate)
					.Build();
			}
			catch
			{
				// Only the cached Lazy's initializer can remove this entry.
				OnlineApplications.TryRemove(key, out _);
				throw;
			}
		}

		private static string GetOnlineAuthority(string configuredAuthority, string tenantId)
		{
			const string publicAuthority = "https://login.microsoftonline.com";
			var authority = string.IsNullOrWhiteSpace(configuredAuthority)
				? publicAuthority
				: configuredAuthority.Trim().TrimEnd('/').ToLowerInvariant();

			switch (authority)
			{
				case publicAuthority:
				case "https://login.microsoftonline.us":
				case "https://login.partner.microsoftonline.cn":
					return authority + "/" + tenantId;
				default:
					throw new ConfigurationErrorsException(
						"Set Authority in the SharePoint connection string to a Microsoft Entra cloud base URL.");
			}
		}

		private static string ReadAppId(string value, string key)
		{
			Guid id;
			if (!Guid.TryParse(value, out id) || id == Guid.Empty)
			{
				throw new ConfigurationErrorsException("Set " + key + " to a valid GUID in the SharePoint connection string.");
			}
			return id.ToString();
		}
	}
}
