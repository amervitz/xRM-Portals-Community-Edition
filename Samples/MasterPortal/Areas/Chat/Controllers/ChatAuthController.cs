/*
  Copyright (c) Microsoft Corporation. All rights reserved.
  Licensed under the MIT License. See License.txt in the project root for license information.
*/

// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ChatAuthController.cs" company="Microsoft">
//   Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace Site.Areas.Chat.Controllers
{
	using System;
	using System.Collections.Generic;
	using Microsoft.IdentityModel.Tokens;
	using System.IdentityModel.Tokens.Jwt;
	using System.IO;
	using System.IO.Compression;
	using System.Linq;
	using System.Net;
	using System.Security.Claims;
	using System.Security.Cryptography;
	using System.Text;
	using System.Web.Mvc;
	using Adxstudio.Xrm;
	using Adxstudio.Xrm.Configuration;
	using Microsoft.Xrm.Portal.Configuration;
	using Microsoft.Xrm.Sdk;
	using Models;
	using Models.LivePerson;
	using Newtonsoft.Json;
	using Org.BouncyCastle.OpenSsl;

	/// <summary>
	///     The chat auth controller.
	/// </summary>
	public class ChatAuthController : Controller
	{
		/// <summary>
		/// Retrieve Portal Public Key
		/// </summary>
		/// <returns>Encoded public key</returns>
		[HttpGet]
		[AllowAnonymous]
		public ActionResult PublicKey()
		{
			using (var cryptoServiceProvider = ChatAuthController.GetCryptoProvider(false))
			{
				var stringWriter = new StringWriter();
				ChatAuthController.ExportPublicKey(cryptoServiceProvider, stringWriter);
				return this.Content(stringWriter.ToString(), "text/plain");
			}
		}

		/// <summary>
		/// Return JWT Auth token for current user to provide authenticated data to Chat
		/// </summary>
		/// <returns>JWT Auth token for current user</returns>
		[HttpGet]
		public ActionResult Token()
		{
			IList<Claim> claims;

			if (this.HttpContext.User.Identity.IsAuthenticated)
			{
				claims = this.GetUserClaims();
			}
			else
			{
				ADXTrace.Instance.TraceInfo(TraceCategory.Application, "User not logged in");

				return new HttpStatusCodeResult(HttpStatusCode.Unauthorized);
			}

			var tokenString = GetTokenString(claims);

			return this.Content(tokenString, "application/jwt");
			
		}


		/// <summary>
		/// Endpoint for 3rd-party chat provider to use to get JWT auth token. May require user to login.
		/// </summary>
		/// <param name="response_type">sresponse type</param>
		/// <param name="client_id">client id</param>
		/// <param name="redirect_uri">redirect uri</param>
		/// <param name="scope">OAuth scope</param>
		/// <param name="state">OAuth state</param>
		/// <param name="nonce">nonce value</param>
		/// <returns>redirect with JWT</returns>
		[AllowAnonymous]
		public ActionResult Authorize(string response_type, string client_id, string redirect_uri, string scope,
			string state, string nonce)
		{
			if (string.IsNullOrEmpty(response_type) ||
				response_type.Split(' ')
					.All(s => string.Compare("token", s, StringComparison.InvariantCultureIgnoreCase) != 0))
			{
				return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
			}

			if (this.HttpContext.User.Identity.IsAuthenticated)
			{
				ADXTrace.Instance.TraceInfo(TraceCategory.Application, "Authenticated User, returning token");

				var claims = this.GetUserClaims();
				if (!string.IsNullOrEmpty(nonce))
				{
					claims.Add(new Claim("nonce", nonce));
				}

				var token = GetTokenString(claims);

				var url = new UriBuilder(redirect_uri);
				var qs = !string.IsNullOrEmpty(url.Query) && url.Query.Length > 1 ? url.Query.Substring(1) + "&" : string.Empty;
				qs += "token=" + token; // token is already encoded

				url.Query = qs;

				return this.Redirect(url.ToString());
			}
			else
			{
				ADXTrace.Instance.TraceInfo(TraceCategory.Application, "Unauthenticated User, triggering authentication");

				var urlState = EncodeState(Request.Url.AbsoluteUri);
				var url = Url.Action("AuthForce", new { state = urlState });

				return this.Redirect(url);
			}
		}

		/// <summary>
		/// Force authentication and redirect based on compressed url in state
		/// </summary>
		/// <param name="state">compressed url</param>
		/// <returns>redirect to token generator</returns>
		public ActionResult AuthForce(string state)
		{
			if (this.HttpContext.User.Identity.IsAuthenticated)
			{
				if (!string.IsNullOrEmpty(state))
				{
					var returnUrl = DecodeState(state);
					return this.Redirect(returnUrl);
				}

				return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
			}
			else
			{
				return new HttpStatusCodeResult(HttpStatusCode.Unauthorized);
			}
		}

		/// <summary>
		/// Compress and encode string for url
		/// </summary>
		/// <param name="state">string to encode</param>
		/// <returns>encoded string</returns>
		private static string EncodeState(string state)
		{
			var bytes = Encoding.UTF8.GetBytes(state);
			using (var input = new MemoryStream(bytes))
			using (var output = new MemoryStream())
			{
				using (var zip = new GZipStream(output, CompressionMode.Compress))
				{
					input.CopyTo(zip);
				}

				return Base64UrlEncoder.Encode(output.ToArray());
			}
		}

		/// <summary>
		/// Decompress and decode string from url
		/// </summary>
		/// <param name="encodedState">encoded string</param>
		/// <returns>decoded string</returns>
		private static string DecodeState(string encodedState)
		{
			var bytes = Base64UrlEncoder.DecodeBytes(encodedState);
			using (var input = new MemoryStream(bytes))
			using (var output = new MemoryStream())
			{
				using (var zip = new GZipStream(input, CompressionMode.Decompress))
				{
					zip.CopyTo(output);
				}

				return Encoding.UTF8.GetString(output.ToArray());
			}
		}

		/// <summary>
		/// Export a public key in PEM format
		/// </summary>
		/// <param name="csp">Crypto Service Provider with key to export</param>
		/// <param name="outputStream">output stream to write key to</param>
		private static void ExportPublicKey(RSACryptoServiceProvider csp, TextWriter outputStream)
		{
			var keyParams = csp.ExportParameters(false);
			var publicKey = Org.BouncyCastle.Security.DotNetUtilities.GetRsaPublicKey(keyParams);
			PemWriter pemWriter = new PemWriter(outputStream);
			pemWriter.WriteObject(publicKey);
		}

		/// <summary>
		/// Get a crypto service provider for portal keys
		/// </summary>
		/// <param name="includePrivateKey">flag indicating whether to include private key in provider</param>
		/// <returns>RSACryptoServiceProvider initialized with appropriate keys</returns>
		private static RSACryptoServiceProvider GetCryptoProvider(bool includePrivateKey)
		{
			var cert = PortalSettings.Instance.Certificate.FindCertificates().FirstOrDefault();

			if (cert == null)
			{
				ADXTrace.Instance.TraceError(TraceCategory.Application, "Unable to find a valid certificate");
				throw new CertificateNotFoundException();
			}

			var csp = includePrivateKey
				? (RSACryptoServiceProvider)cert.PrivateKey
				: (RSACryptoServiceProvider)cert.PublicKey.Key;

			if (includePrivateKey)
			{
				// need to move key into enhanced crypto provider without exporting key
				var rsa256Csp = new RSACryptoServiceProvider().CspKeyContainerInfo;
				var cspParams = new CspParameters(rsa256Csp.ProviderType, rsa256Csp.ProviderName,
					csp.CspKeyContainerInfo.KeyContainerName);
				return new RSACryptoServiceProvider(2048, cspParams) { PersistKeyInCsp = true };
			}
			else
			{
				return csp;
			}
		}

		/// <summary>
		/// Get encoded JWT token for given claims
		/// </summary>
		/// <param name="claims">list of claims to include in token</param>
		/// <returns>string representation of JWT token</returns>
		private static string GetTokenString(IList<Claim> claims)
		{
			string tokenString = null;
			using (var cryptoServiceProvider = GetCryptoProvider(true))
			{
				string issuer = PortalSettings.Instance.DomainName;
				string audience = string.Empty;
				DateTime notBefore = DateTime.Now;
				DateTime expires = notBefore.AddHours(1);

				var tokenHandler = new JwtSecurityTokenHandler();
				var signingCredentials = new SigningCredentials(new RsaSecurityKey(cryptoServiceProvider),
					SecurityAlgorithms.RsaSha256Signature, SecurityAlgorithms.Sha256Digest);

				// need to explicitly add "iat" claim
				DateTime unixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
				var iat = Convert.ToInt64((TimeZoneInfo.ConvertTimeToUtc(notBefore) - unixEpoch).TotalSeconds - 1);
				claims.Add(new Claim("iat", iat.ToString(), ClaimValueTypes.Integer));

				var header = new JwtHeader(signingCredentials);
				var payload = new JwtPayload(issuer, audience, claims, notBefore, expires);

				// Need to adjust this because Claim class ignores value type
				payload["iat"] = Convert.ToInt64(payload["iat"]);

				var jwtToken = new JwtSecurityToken(header, payload);

				tokenString = tokenHandler.WriteToken(jwtToken);
			}
			return tokenString;
		}

		/// <summary>
		/// The details of the customer.
		/// </summary>
		/// <returns>
		/// The <see cref="ChatUserModel"/>.
		/// </returns>
		private ChatUserModel GetChatUserData()
		{
			var portalContext = PortalCrmConfigurationManager.CreatePortalContext();

			if (portalContext.User == null)
			{
				ADXTrace.Instance.TraceWarning(TraceCategory.Application, "portalContext.User is null");
				return null;
			}

			var result = new ChatUserModel
			{
				Username = portalContext.User.GetAttributeValue<string>("adx_identity_username"),
				Id = portalContext.User.GetAttributeValue<Guid>("contactid"),
				FirstName = portalContext.User.GetAttributeValue<string>("firstname"),
				LastName = portalContext.User.GetAttributeValue<string>("lastname"),
				Email = portalContext.User.GetAttributeValue<string>("emailaddress1"),
				Phone = portalContext.User.GetAttributeValue<string>("telephone1")
			};

			var customerType = portalContext.User.GetAttributeValue<OptionSetValue>("customertypecode");
			if (customerType != null)
			{
				result.CustomerType = customerType.Value;
			}
			else
			{
				ADXTrace.Instance.TraceError(TraceCategory.Application, "customertypecode not set");
				throw new NullReferenceException("customertypecode");
			}

			return result;
		}

		/// <summary>
		/// Get list of claims 
		/// </summary>
		/// <returns>list of claims for user</returns>
		private IList<Claim> GetUserClaims()
		{
			var userData = this.GetChatUserData();

			IList<Claim> claims = new List<Claim>
			{
				new Claim("sub", userData.Id.ToString()),
				new Claim("preferred_username", userData.Username ?? string.Empty),
				new Claim("phone_number", userData.Phone ?? string.Empty),
				new Claim("given_name", userData.FirstName ?? string.Empty),
				new Claim("family_name", userData.LastName ?? string.Empty),
				new Claim("email", userData.Email ?? string.Empty)
			};

			var lpSdes = new LivePersonSdesCustomerInfo()
			{
				CustomerInfo = new LivePersonCustomerInfo()
				{
					Type = "contact",
					Id = userData.Id.ToString(),
					Imei = userData.Phone ?? string.Empty,
					UserName = userData.Username ?? string.Empty
				}
			};

			var custInfo = JsonConvert.SerializeObject(new object[] { lpSdes });
			claims.Add(new Claim("lp_sdes", custInfo, "JSON_ARRAY"));
			return claims;
		}
	}
}
