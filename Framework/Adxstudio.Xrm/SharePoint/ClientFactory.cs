/*
  Copyright (c) Microsoft Corporation. All rights reserved.
  Licensed under the MIT License. See License.txt in the project root for license information.
*/

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IdentityModel.Tokens;
using System.Linq;
using System.Net;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Security;
using System.Text;
using Adxstudio.Xrm.Resources;
using System.IdentityModel.Protocols.WSTrust;
using Microsoft.SharePoint.Client;
using Microsoft.Xrm.Portal.Configuration;
using Adxstudio.Xrm.Cms;

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

		private static readonly ConcurrentDictionary<string, CookieContainer> _cookiesLookup = new ConcurrentDictionary<string, CookieContainer>();

		/// <summary>
		/// Creates a context based on connection settings.
		/// </summary>
		public virtual ClientContext CreateClientContext(SharePointConnection connection)
		{
			if (connection == null) throw new ArgumentNullException("connection");

			var context = connection.CreateClientContext();

			if (IsOnline(connection))
			{
				UpdateClientContextForOnline(connection, context);
			}

			return context;
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
			return GetOnlineDomainsSetting().Any(domain => connection.Url.Host.EndsWith(domain, StringComparison.OrdinalIgnoreCase));
		}


		private static IEnumerable<string> GetOnlineDomainsSetting()
		{
			var portalContext = PortalCrmConfigurationManager.CreatePortalContext();
			var onlineDomains = portalContext.ServiceContext.GetSiteSettingValueByName(portalContext.Website, _onlineDomainsSettingKey);
			var domains = (onlineDomains != null) ? onlineDomains.Split(';').ToList() : new List<string>();
			domains.AddRange(_onlineDomains);
			return domains.Distinct();
		}
		protected virtual void UpdateClientContextForOnline(SharePointConnection connection, ClientContext context)
		{
			context.ExecutingWebRequest += (sender, args) =>
			{
				args.WebRequestExecutor.WebRequest.CookieContainer = CreateCookies(connection);
			};
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

				// if this context is using default Windows authentication add a WebRequest Header to stop forms auth from potentially interfering.
				if (connection.AuthenticationMode.GetValueOrDefault() == ClientAuthenticationMode.Default)
				{
					httpRequest.Headers.Add("X-FORMS_BASED_AUTH_ACCEPTED", "f");
				}
			}
		}

		protected virtual void UpdateHttpWebRequestForOnline(SharePointConnection connection, HttpWebRequest request)
		{
			request.CookieContainer = CreateCookies(connection);
		}

		protected virtual CookieContainer CreateCookies(SharePointConnection connection)
		{
			var expiryWindow = connection.CookieExpiryWindow;

			if (expiryWindow != TimeSpan.Zero)
			{
				CookieContainer container;
				var key = connection.GetConnectionId();

				if (!_cookiesLookup.TryGetValue(key, out container) || CheckIfAnyCookieIsExpired(container, connection.Url, expiryWindow))
				{
					container = CreateCookieContainer(connection);
					_cookiesLookup[key] = container;
				}

				return container;
			}

			var cookies = CreateCookieContainer(connection);
			return cookies;
		}

		private static readonly string _onlineDomainsSettingKey = "OnlineDomains";
		private static readonly IEnumerable<string> _onlineDomains = new[] { "sharepoint.com", "microsoftonline.com" };
		private const string _stsUrl = "https://login.microsoftonline.com/extSTS.srf";
		private const string _signInPath = "/_forms/default.aspx?wa=wsignin1.0";
		private const string _userAgent = "Mozilla/5.0 (compatible; MSIE 9.0; Windows NT 6.1; WOW64; Trident/5.0)";

		private static bool CheckIfAnyCookieIsExpired(CookieContainer container, Uri url, TimeSpan? expiryWindow)
		{
			var now = DateTime.UtcNow;
			var expires = container.GetCookies(url).Cast<Cookie>().Min(cookie => cookie.Expires);

			if (expiryWindow == null) return now >= expires;

			var expired = (now + expiryWindow.Value) > expires;

			return expired;
		}

		private static CookieContainer CreateCookieContainer(SharePointConnection connection)
		{
			if (connection.Credentials == null) throw new NullReferenceException("The Credentials property of the connection is required.");

			var authType = connection.AuthenticationMode != null ? connection.AuthenticationMode.Value : ClientAuthenticationMode.Default;
			var credentials = connection.Credentials.GetCredential(connection.Url, authType.ToString());

			var container = CreateCookieContainer(connection.Url, credentials.UserName, credentials.Password, connection.RequestTimeout);

			return container;
		}

		private static CookieContainer CreateCookieContainer(Uri spSiteUrl, string username, string password, int? timeout)
		{
			var signInUrl = new Uri(spSiteUrl.GetLeftPart(UriPartial.Authority) + _signInPath);

			var securityToken = GetSecurityToken(spSiteUrl, username, password);

			var expires = securityToken.ValidTo;
			var token = securityToken.TokenXml.InnerText;
			var data = Encoding.UTF8.GetBytes(token);

			return CreateCookieContainer(signInUrl, expires, data, timeout);
		}

		private static CookieContainer CreateCookieContainer(Uri uri, DateTime expires, byte[] data, int? timeout)
		{
			var request = CreateRequest(uri, timeout);

			using (var reqStream = request.GetRequestStream())
			{
				reqStream.Write(data, 0, data.Length);
				reqStream.Close();

				using (var response = request.GetResponse() as HttpWebResponse)
				{
					if (response.StatusCode == HttpStatusCode.MovedPermanently)
					{
						var location = response.Headers["Location"];

						if (!string.IsNullOrWhiteSpace(location))
						{
							var check = new Uri(location, UriKind.RelativeOrAbsolute);
							var locationUrl = check.IsAbsoluteUri ? check : new Uri(uri, check);

							return CreateCookieContainer(locationUrl, expires, data, timeout);
						}
					}

					return CreateCookieContainer(expires, request.RequestUri, response.Cookies);
				}
			}
		}

		private static CookieContainer CreateCookieContainer(DateTime expires, Uri host, CookieCollection cookies)
		{
			var fedAuth = CreateCookie("FedAuth", cookies["FedAuth"].Value, expires, host);
			var rtFa = CreateCookie("rtFA", cookies["rtFA"].Value, expires, host);

			var container = new CookieContainer();
			container.Add(fedAuth);
			container.Add(rtFa);

			return container;
		}

		private static Cookie CreateCookie(string name, string value, DateTime expires, Uri host)
		{
			var cookie = new Cookie(name, value)
			{
				Expires = expires,
				Path = "/",
				Secure = string.Equals(host.Scheme, "https", StringComparison.OrdinalIgnoreCase),
				HttpOnly = true,
				Domain = host.Host,
			};

			return cookie;
		}

		private static GenericXmlSecurityToken GetSecurityToken(Uri spSiteUrl, string username, string password)
		{
			var wsHttpBinding = new WSHttpBinding(SecurityMode.TransportWithMessageCredential);
			wsHttpBinding.Security.Message.ClientCredentialType = MessageCredentialType.UserName;
			wsHttpBinding.Security.Message.EstablishSecurityContext = false;
			wsHttpBinding.Security.Message.NegotiateServiceCredential = false;

			// WIF's UserNameWSTrustBinding issued the WSSecurity10/WSTrustFeb2005 profile that the
			// Microsoft Online STS expects, whereas WSHttpBinding defaults to WSSecurity11/WSTrust13.
			// The default has to be overridden on the security binding element, which is only reachable
			// by rebuilding the binding as a CustomBinding.
			var binding = new CustomBinding(wsHttpBinding);
			var security = binding.Elements.Find<SecurityBindingElement>();

			if (security != null)
			{
				security.MessageSecurityVersion =
					MessageSecurityVersion.WSSecurity10WSTrustFebruary2005WSSecureConversationFebruary2005WSSecurityPolicy11BasicSecurityProfile10;
			}

			var address = new EndpointAddress(_stsUrl);

			using (var factory = new WSTrustChannelFactory(binding, address) { TrustVersion = TrustVersion.WSTrustFeb2005 })
			{
				factory.Credentials.UserName.UserName = username;
				factory.Credentials.UserName.Password = password;

				var channel = factory.CreateChannel();

				var rst = new RequestSecurityToken
				{
					RequestType = RequestTypes.Issue,
					KeyType = KeyTypes.Bearer,
					TokenType = "http://docs.oasis-open.org/wss/oasis-wss-saml-token-profile-1.1#SAMLV1.1",
					AppliesTo = new EndpointReference(spSiteUrl.AbsoluteUri),
				};

				var genericToken = channel.Issue(rst) as GenericXmlSecurityToken;

				return genericToken;
			}
		}

		private static HttpWebRequest CreateRequest(Uri uri, int? timeout)
		{
			var request = WebRequest.Create(uri) as HttpWebRequest;
			request.Method = "POST";
			request.ContentType = "application/x-www-form-urlencoded";
			request.CookieContainer = new CookieContainer();
			request.AllowAutoRedirect = false;
			request.UserAgent = _userAgent;

			if (timeout != null) request.Timeout = timeout.Value;

			return request;
		}
	}
}
