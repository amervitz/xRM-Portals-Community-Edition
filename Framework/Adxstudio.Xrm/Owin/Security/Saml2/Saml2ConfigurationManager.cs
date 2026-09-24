/*
  Copyright (c) Microsoft Corporation. All rights reserved.
  Licensed under the MIT License. See License.txt in the project root for license information.
*/

using System.IdentityModel.Metadata;
using System.IdentityModel.Tokens;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.ServiceModel.Security;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Adxstudio.Xrm.AspNet;
using ITfoxtec.Identity.Saml2.Schemas;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.WsFederation;

namespace Adxstudio.Xrm.Owin.Security.Saml2
{
	/// <summary>
	/// Manages the retrieval of SAML 2.0 configuration data.
	/// </summary>
	public class Saml2ConfigurationManager : IConfigurationManager<WsFederationConfiguration>
	{
		private class Saml2DocumentRetriever : IDocumentRetriever
		{
			private readonly HttpClient _httpClient;

			public Saml2DocumentRetriever(HttpClient httpClient)
			{
				_httpClient = httpClient;
			}

			public async Task<string> GetDocumentAsync(string address, CancellationToken cancel)
			{
				var response = await _httpClient.GetAsync(address, cancel).WithCurrentCulture();
				response.EnsureSuccessStatusCode();

				return await response.Content.ReadAsStringAsync().WithCurrentCulture();
			}
		}

		private static readonly XmlReaderSettings _settings = new XmlReaderSettings { XmlResolver = null, DtdProcessing = DtdProcessing.Prohibit, ValidationType = ValidationType.None };
		private readonly string _metadataAddress;
		private readonly IDocumentRetriever _documentRetriever;
		private WsFederationConfiguration _configuration;
		private readonly SemaphoreSlim _refreshLock;
		private bool _refreshRequested;

		public Saml2ConfigurationManager(string metadataAddress, HttpClient httpClient)
			: this(metadataAddress, new Saml2DocumentRetriever(httpClient))
		{
		}

		public Saml2ConfigurationManager(string metadataAddress, IDocumentRetriever documentRetriever)
		{
			_metadataAddress = metadataAddress;
			_documentRetriever = documentRetriever;
			_refreshLock = new SemaphoreSlim(1);
		}

		public async Task<WsFederationConfiguration> GetConfigurationAsync(CancellationToken cancel)
		{
			if (_configuration != null && !_refreshRequested) return _configuration;

			await _refreshLock.WaitAsync(cancel);

			try
			{
				_configuration = await GetConfigurationAsync(_metadataAddress, _documentRetriever, CancellationToken.None);
				_refreshRequested = false;

				return _configuration;
			}
			finally
			{
				_refreshLock.Release();
			}
		}

		public void RequestRefresh()
		{
			_refreshRequested = true;
		}

		private static async Task<WsFederationConfiguration> GetConfigurationAsync(string address, IDocumentRetriever retriever, CancellationToken cancel)
		{
			var document = await retriever.GetDocumentAsync(address, cancel);
			var configuration = new WsFederationConfiguration();

			using (var sr = new StringReader(document))
			using (var xr = XmlReader.Create(sr, _settings))
			{
				var serializer = new MetadataSerializer { CertificateValidationMode = System.ServiceModel.Security.X509CertificateValidationMode.None };
				var entityDescriptor = serializer.ReadMetadata(xr) as EntityDescriptor;

				if (entityDescriptor != null)
				{
					configuration.Issuer = entityDescriptor.EntityId.Id;

					var idpssod = entityDescriptor.RoleDescriptors.OfType<IdentityProviderSingleSignOnDescriptor>().FirstOrDefault();

					if (idpssod != null)
					{
						var redirectBinding = idpssod.SingleSignOnServices.FirstOrDefault(ssos => ssos.Binding == ProtocolBindings.HttpRedirect);

						if (redirectBinding != null)
						{
							configuration.TokenEndpoint = redirectBinding.Location.OriginalString;
						}

						var keys = idpssod.Keys
							.Where(key => key.KeyInfo != null && (key.Use == KeyType.Signing || key.Use == KeyType.Unspecified))
							.SelectMany(key => key.KeyInfo.OfType<X509RawDataKeyIdentifierClause>())
							.Select(clause => new Microsoft.IdentityModel.Tokens.X509SecurityKey(new X509Certificate2(clause.GetX509RawData())));

						foreach (var key in keys)
						{
							configuration.SigningKeys.Add(key);
						}
					}
				}
			}

			return configuration;
		}
	}
}
