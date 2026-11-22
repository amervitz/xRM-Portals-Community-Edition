/*
  Copyright (c) Microsoft Corporation. All rights reserved.
  Licensed under the MIT License. See License.txt in the project root for license information.
*/

using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.Common;
using System.Net;
using Adxstudio.SharePoint.Collections.Generic;
using Adxstudio.SharePoint.Configuration;
using Adxstudio.Xrm.Resources;
using Microsoft.SharePoint.Client;
using Microsoft.Xrm.Client;
using Microsoft.Xrm.Client.Configuration;

namespace Adxstudio.SharePoint
{
	/// <summary>
	/// The connection settings for building an <see cref="ClientContext"/>.
	/// </summary>
	/// <remarks>
	/// <example>
	/// SharePoint Online with Microsoft Entra app-only authentication.
	/// <code>
	/// <![CDATA[
	/// <configuration>
	///   <connectionStrings>
	///     <add name="SharePoint"
	///          connectionString="Url=https://contoso.sharepoint.com/sites/Portal;TenantId=TENANT-GUID;ClientId=APP-GUID;CertificateThumbprint=CERTIFICATE-THUMBPRINT;" />
	///   </connectionStrings>
	/// </configuration>
	/// ]]>
	/// </code>
	/// </example>
	/// <example>
	/// On-premises SharePoint Server with Windows authentication.
	/// <code>
	/// <![CDATA[
	/// <configuration>
	///   <connectionStrings>
	///     <add name="SharePoint"
	///          connectionString="Url=https://sharepoint.contoso.com/sites/Portal;Domain=CONTOSO;UserName=portal-service;Password=YOUR_PASSWORD;" />
	///   </connectionStrings>
	/// </configuration>
	/// ]]>
	/// </code>
	/// </example>
	/// <example>
	/// Creating a connection by configuration.
	/// <code>
	/// var connection = new SharePointConnection("SharePoint");
	/// </code>
	/// </example>
	/// <example>
	/// Creating a connection in code.
	/// <code>
	/// var connection = SharePointConnection.Parse("Url=hostname; Domain=domain; UserName=username; Password=password;");
	/// </code>
	/// </example>
	/// </remarks>
	/// <seealso cref="ClientContext"/>
	public class SharePointConnection
	{
		/// <summary>
		/// Gets or sets the name of the runtime where the current client application is located.
		/// </summary>
		public string ApplicationName { get; set; }

		/// <summary>
		/// Uses Entra application authentication for SharePoint requests when enabled.
		/// </summary>
		public bool UseEntraAppOnly { get; set; }

		/// <summary>The Entra tenant ID for app-only authentication.</summary>
		public string TenantId { get; set; }

		/// <summary>The Entra application ID for app-only authentication.</summary>
		public string ClientId { get; set; }

		/// <summary>The Entra cloud base URL. Defaults to the public cloud.</summary>
		public string Authority { get; set; }

		/// <summary>The app certificate thumbprint in the local machine personal store.</summary>
		public string CertificateThumbprint { get; set; }

		/// <summary>
		/// Gets or sets the authentication information for the client context.
		/// </summary>
		public ICredentials Credentials { get; set; }

		/// <summary>
		/// Gets or sets the requested time-out value in milliseconds.
		/// </summary>
		public int? RequestTimeout { get; set; }

		/// <summary>
		/// Gets or sets the flag that indicates whether the client library needs to validate the method parameters on the client side.
		/// </summary>
		public bool? ValidateOnClient { get; set; }

		/// <summary>
		/// Gets the URL associated with the runtime context.
		/// </summary>
		public Uri Url { get; set; }

		public SharePointConnection()
			: this("SharePoint")
		{
		}

		public SharePointConnection(string connectionStringName)
			: this(GetConnectionStringSettings(connectionStringName))
		{
		}

		public SharePointConnection(ConnectionStringSettings connectionString)
			: this(SharePointConfigurationManager.CreateConnectionDictionary(connectionString),
				readEntraSettings: !string.Equals(connectionString.Name, "Xrm", StringComparison.OrdinalIgnoreCase))
		{
		}

		private SharePointConnection(IDictionary<string, string> connection, bool readEntraSettings = true)
			: this(
				connection.FirstNotNullOrEmpty("Url"),
				connection.FirstNotNullOrEmpty("Domain"),
				connection.FirstNotNullOrEmpty("UserName", "User Name", "UserId", "User Id"),
				connection.FirstNotNullOrEmpty("Password"),
				connection.FirstNotNullOrEmpty("ApplicationName", "Application Name"),
				connection.FirstNotNullOrEmpty("RequestTimeout", "Request Timeout", "Timeout"),
				connection.FirstNotNullOrEmpty("ValidateOnClient", "Validate On Client"))
		{
			// The Xrm fallback may carry Entra keys for CRM itself.
			if (readEntraSettings)
			{
				UseEntraAppOnly = connection.ContainsKey("TenantId") || connection.ContainsKey("ClientId")
					|| connection.ContainsKey("CertificateThumbprint") || connection.ContainsKey("Authority");
				TenantId = connection.FirstNotNullOrEmpty("TenantId");
				ClientId = connection.FirstNotNullOrEmpty("ClientId");
				Authority = connection.FirstNotNullOrEmpty("Authority");
				CertificateThumbprint = connection.FirstNotNullOrEmpty("CertificateThumbprint");
			}
		}

		private SharePointConnection(
			string url,
			string domain, string userName, string password,
			string applicationName,
			string requestTimeout,
			string validateOnClient)
		{
			Url = new Uri(url);
			ApplicationName = applicationName;

			if (!string.IsNullOrWhiteSpace(userName) && !string.IsNullOrWhiteSpace(password))
			{
				Credentials = new NetworkCredential(userName, password, domain);
			}
			else if (!(string.IsNullOrWhiteSpace(userName) && string.IsNullOrWhiteSpace(password)))
			{
				throw new ConfigurationErrorsException("The specified user credentials are invalid.");
			}

			if (!string.IsNullOrWhiteSpace(requestTimeout))
			{
				RequestTimeout = int.Parse(requestTimeout);
			}

			if (!string.IsNullOrWhiteSpace(validateOnClient))
			{
				ValidateOnClient = bool.Parse(validateOnClient);
			}
		}

		private static ConnectionStringSettings GetConnectionStringSettings(string connectionStringName)
		{
			var settings = ConfigurationManager.ConnectionStrings[connectionStringName];

			if (settings == null)
			{
				if (connectionStringName == "SharePoint")
				{
					// Try to use the CRM connection string "Xrm"
					if (ConfigurationManager.ConnectionStrings.Count != 0)
					{
						settings = ConfigurationManager.ConnectionStrings["Xrm"];
					}
					else if (CrmConfigurationManager.GetCrmSection().ConnectionStrings.Count != 0)
					{
						settings = CrmConfigurationManager.GetCrmSection().ConnectionStrings["Xrm"];
					}
				}

				if (settings == null)
				{
					throw new ConfigurationErrorsException("Unable to find a connection string with the name {0}.".FormatWith(connectionStringName));
				}
			}

			return settings;
		}

		/// <summary>
		/// Parses a string in the <see cref="DbConnectionStringBuilder"/> format.
		/// </summary>
		/// <param name="connectionString"></param>
		/// <returns></returns>
		public static SharePointConnection Parse(string connectionString)
		{
			return new SharePointConnection(connectionString.ToDictionary());
		}

		/// <summary>
		/// Creates a client context from the current connection.
		/// </summary>
		/// <returns></returns>
		public ClientContext CreateClientContext()
		{
			return UseEntraAppOnly ? new ClientFactory().CreateClientContext(this) : CreateClientContext(useConnectionCredentials: true);
		}

		internal ClientContext CreateClientContext(bool useConnectionCredentials)
		{
			var context = new ClientContext(Url);

			if (ApplicationName != null) context.ApplicationName = ApplicationName;
			context.AuthenticationMode = useConnectionCredentials ? ClientAuthenticationMode.Default : ClientAuthenticationMode.Anonymous;
			if (useConnectionCredentials && Credentials != null) context.Credentials = Credentials;
			if (RequestTimeout != null) context.RequestTimeout = RequestTimeout.Value;
			if (ValidateOnClient != null) context.ValidateOnClient = ValidateOnClient.Value;

			// Tell SharePoint to use Windows authentication rather than redirecting to a sign-in form.
			if (useConnectionCredentials)
			{
				context.ExecutingWebRequest += ClientContext_ExecutingWebRequest;
			}

			return context;
		}

		/// <summary>
		/// Adds a WebRequest Header to disable forms auth.
		/// </summary>
		private static void ClientContext_ExecutingWebRequest(object sender, WebRequestEventArgs e)
		{
			e.WebRequestExecutor.WebRequest.Headers.Add("X-FORMS_BASED_AUTH_ACCEPTED", "f");
		}
	}
}
