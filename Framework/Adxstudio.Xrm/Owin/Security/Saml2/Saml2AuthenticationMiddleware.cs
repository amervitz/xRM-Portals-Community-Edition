/*
  Copyright (c) Microsoft Corporation. All rights reserved.
  Licensed under the MIT License. See License.txt in the project root for license information.
*/

using System;
using System.Net.Http;
using Adxstudio.Xrm.Resources;
using ITfoxtec.Identity.Saml2.Tokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.WsFederation;
using Microsoft.Owin;
using Microsoft.Owin.Logging;
using Microsoft.Owin.Security.Infrastructure;
using Microsoft.Owin.Security.WsFederation;
using Owin;

namespace Adxstudio.Xrm.Owin.Security.Saml2
{
	/// <summary>
	/// OWIN middleware for obtaining identities using SAML protocol.
	/// </summary>
	public class Saml2AuthenticationMiddleware : WsFederationAuthenticationMiddleware
	{
		private readonly ILogger _logger;

		public Saml2AuthenticationMiddleware(OwinMiddleware next, IAppBuilder app, Saml2AuthenticationOptions options)
			: base(next, app, options)
		{
			_logger = app.CreateLogger<Saml2AuthenticationMiddleware>();

			var configurationManager = options.ConfigurationManager as ConfigurationManager<WsFederationConfiguration>;

			if (configurationManager != null)
			{
				var httpClient = new HttpClient(ResolveHttpMessageHandler(options))
				{
					Timeout = Options.BackchannelTimeout,
					MaxResponseContentBufferSize = 1024 * 1024 * 10
				};

				options.ConfigurationManager = new Saml2ConfigurationManager(options.MetadataAddress, httpClient);

				ADXTrace.Instance.TraceInfo(TraceCategory.Application, string.Format("MetadataAddress={0}", options.MetadataAddress));
			}
		}

		protected override AuthenticationHandler<WsFederationAuthenticationOptions> CreateHandler()
		{
			return new Saml2AuthenticationHandler(_logger);
		}

		private static HttpMessageHandler ResolveHttpMessageHandler(Saml2AuthenticationOptions options)
		{
			var handler = options.BackchannelHttpHandler ?? new WebRequestHandler();

			if (options.BackchannelCertificateValidator != null)
			{
				var webRequestHandler = handler as WebRequestHandler;

				if (webRequestHandler == null)
				{
					throw new InvalidOperationException("An ICertificateValidator cannot be specified at the same time as an HttpMessageHandler unless it is a WebRequestHandler.");
				}

				webRequestHandler.ServerCertificateValidationCallback = options.BackchannelCertificateValidator.Validate;
			}

			return handler;
		}
	}
}
