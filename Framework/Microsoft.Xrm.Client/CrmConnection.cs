/*
  Copyright (c) Microsoft Corporation. All rights reserved.
  Licensed under the MIT License. See License.txt in the project root for license information.
*/

using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.Common;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.ServiceModel.Description;
using System.Text;
using System.Web;
using Microsoft.Xrm.Client.Collections.Generic;
using Microsoft.Xrm.Client.Configuration;
using Microsoft.Xrm.Client.Services;
using Microsoft.Xrm.Sdk.Client;

namespace Microsoft.Xrm.Client
{
	/// <summary>
	/// The modes in which the <see cref="OrganizationService"/> instantiates <see cref="IServiceConfiguration{TService}"/> objects.
	/// </summary>
	public enum ServiceConfigurationInstanceMode
	{
		/// <summary>
		/// Create a static instance.
		/// </summary>
		Static,

		/// <summary>
		/// Create an instance for every web request.
		/// </summary>
		/// <remarks>
		/// In the absense of a <see cref="HttpContext"/>, this setting equals PerInstance.
		/// </remarks>
		PerRequest,

		/// <summary>
		/// Create an instance for each connection Id.
		/// </summary>
		PerName,

		/// <summary>
		/// Create an instance on every invocation.
		/// </summary>
		PerInstance
	}

	/// <summary>
	/// The connection settings for building an <see cref="OrganizationServiceProxy"/>.
	/// </summary>
	/// <remarks>
	/// <example>
	/// An example configuration. The connection values should be in the <see cref="DbConnectionStringBuilder"/> format.
	/// <code>
	/// <![CDATA[
	/// <configuration>
	///  <connectionStrings>
	///   <add name="Integrated" connectionString="Url=http://crm.contoso.com/xrmContoso"/>
	///   <add name="Premises" connectionString="Url=http://crm.contoso.com/xrmContoso; Domain=CONTOSO; Username=jsmith; Password=passcode"/>
	///   <add name="Online" connectionString="Url=https://contoso.crm.dynamics.com; Username=jsmith@live.com; Password=passcode; DeviceID=contoso-ba9f6b7b2e6d; DevicePassword=passcode"/>
	///   <add name="Usage" connectionString="Url=hostname; HomeRealmUri=hostname; Domain=CONTOSO; Username=jsmith; Password=passcode; DeviceID=contoso-ba9f6b7b2e6d; DevicePassword=passcode; Timeout=HH:MM:SS; ProxyTypesEnabled=false|true; ProxyTypesAssembly=assembly; CallerId=guid; ServiceConfigurationInstanceMode=Static|PerRequest|PerName|PerInstance; UserTokenExpiryWindow=HH:MM:SS;"/>
	///  </connectionStrings>
	/// </configuration>
	/// ]]>
	/// </code>
	/// </example>
	/// <example>
	/// Creating a connection by configuration.
	/// <code>
	/// var connection = new CrmConnection("Premises");
	/// </code>
	/// </example>
	/// <example>
	/// Creating a connection in code.
	/// <code>
	/// var connection1 = CrmConnection.Parse("Url=http://crm.contoso.com/xrmContoso; Domain=CONTOSO; Username=jsmith; Password=passcode");
	/// 
	/// var credentials = new ClientCredentials();
	/// credentials.Windows.ClientCredential = new NetworkCredential { Domain = "CONTOSO", UserName = "jsmith", Password = "passcode" };
	/// 
	/// var connection2 = new CrmConnection { ServiceUri = new Uri("http://crm.contoso.com/xrmContoso"), ClientCredentials = credentials };
	/// </code>
	/// </example>
	/// </remarks>
	public class CrmConnection
	{
		private static readonly bool _defaultProxyTypesEnabled = true;
		private static readonly ServiceConfigurationInstanceMode _defaultServiceConfigurationInstanceMode = ServiceConfigurationInstanceMode.PerName;

		/// <summary>
		/// The organization service URL.
		/// </summary>
		public Uri ServiceUri { get; set; }

		/// <summary>
		/// The uri of the cross realm STS metadata endpoint.
		/// </summary>
		public Uri HomeRealmUri { get; set; }

		/// <summary>
		/// The user credentials.
		/// </summary>
		public ClientCredentials ClientCredentials { get; set; }

		/// <summary>
		/// The Windows Live ID device credentials.
		/// </summary>
		public ClientCredentials DeviceCredentials { get; set; }

		/// <summary>
		/// The service timeout value.
		/// </summary>
		public TimeSpan? Timeout { get; set; }

		/// <summary>
		/// Determines if the strong proxy types should be returned by the service.
		/// </summary>
		public bool ProxyTypesEnabled { get; set; }

		/// <summary>
		/// The assembly containing the strong proxy types.
		/// </summary>
		public Assembly ProxyTypesAssembly { get; set; }

		/// <summary>
		/// The ID of the system user to impersonate.
		/// </summary>
		public Guid? CallerId { get; set; }

		/// <summary>
		/// The mode for instantiating the service configuration.
		/// </summary>
		public ServiceConfigurationInstanceMode ServiceConfigurationInstanceMode { get; set; }

		/// <summary>
		/// The time offset prior to the user token expiration when the user token should be refreshed.
		/// </summary>
		public TimeSpan? UserTokenExpiryWindow { get; set; }

		internal string ConnectionString { get; private set; }

		public CrmConnection()
		{
			ProxyTypesEnabled = _defaultProxyTypesEnabled;
			ServiceConfigurationInstanceMode = ServiceConfigurationInstanceMode.PerName;
		}

		public CrmConnection(string connectionStringName)
			: this(GetConnectionStringSettings(connectionStringName))
		{
		}

		public CrmConnection(ConnectionStringSettings connectionString)
			: this(CrmConfigurationManager.CreateConnectionDictionary(connectionString))
		{
			ConnectionString = connectionString.ConnectionString;
		}

		private CrmConnection(IDictionary<string, string> connection)
			: this(
				connection.FirstNotNullOrEmpty("ServiceUri", "Service Uri", "Url", "Server"),
				connection.FirstNotNullOrEmpty("HomeRealmUri", "Home Realm Uri"),
				connection.FirstNotNullOrEmpty("Domain"),
				connection.FirstNotNullOrEmpty("UserName", "User Name", "UserId", "User Id"),
				connection.FirstNotNullOrEmpty("Password"),
				connection.FirstNotNullOrEmpty("DeviceId", "Device Id", "DeviceUserName", "Device User Name"),
				connection.FirstNotNullOrEmpty("DevicePassword", "Device Password"),
				connection.FirstNotNullOrEmpty("Timeout"),
				connection.FirstNotNullOrEmpty("ProxyTypesEnabled", "Proxy Types Enabled"),
				connection.FirstNotNullOrEmpty("ProxyTypesAssembly", "Proxy Types Assembly"),
				connection.FirstNotNullOrEmpty("CallerId", "Caller Id"),
				connection.FirstNotNullOrEmpty("ServiceConfigurationInstanceMode", "Service Configuration Instance Mode"),
				connection.FirstNotNullOrEmpty("UserTokenExpiryWindow", "User Token Expiry Window"))
		{
		}

		private CrmConnection(
			string serviceUri,
			string homeRealmUri,
			string domain, string userName, string password,
			string deviceId, string devicePassword,
			string timeout,
			string proxyTypesEnabled,
			string proxyTypesAssembly,
			string callerId,
			string serviceConfigurationInstanceMode,
			string userTokenExpiryWindow)
		{
			ServiceUri = !string.IsNullOrWhiteSpace(serviceUri) ? new Uri(serviceUri) : null;
			HomeRealmUri = !string.IsNullOrWhiteSpace(homeRealmUri) ? new Uri(homeRealmUri) : null;

			if (!string.IsNullOrWhiteSpace(userName) && !string.IsNullOrWhiteSpace(password))
			{
				var credentials = new ClientCredentials();

				if (!string.IsNullOrWhiteSpace(domain))
				{
					credentials.Windows.ClientCredential = new NetworkCredential(userName, password, domain);
				}
				else
				{
					credentials.UserName.UserName = userName;
					credentials.UserName.Password = password;
				}

				ClientCredentials = credentials;
			}
			else if (!(string.IsNullOrWhiteSpace(userName) && string.IsNullOrWhiteSpace(password)))
			{
				throw new ConfigurationErrorsException("The specified user credentials are invalid.");
			}

			if (!string.IsNullOrWhiteSpace(deviceId) && !string.IsNullOrWhiteSpace(devicePassword))
			{
				var credentials = new ClientCredentials();
				credentials.UserName.UserName = deviceId;
				credentials.UserName.Password = devicePassword;

				DeviceCredentials = credentials;
			}
			else if (!(string.IsNullOrWhiteSpace(deviceId) && string.IsNullOrWhiteSpace(devicePassword)))
			{
				throw new ConfigurationErrorsException("The specified device credentials are invalid.");
			}

			Timeout = !string.IsNullOrWhiteSpace(timeout) ? TimeSpan.Parse(timeout) as TimeSpan? : null;
			UserTokenExpiryWindow = !string.IsNullOrWhiteSpace(userTokenExpiryWindow) ? TimeSpan.Parse(userTokenExpiryWindow) as TimeSpan? : null;

			bool tempProxyTypesEnabled;

			if (!bool.TryParse(proxyTypesEnabled, out tempProxyTypesEnabled))
			{
				tempProxyTypesEnabled = _defaultProxyTypesEnabled;
			}

			ProxyTypesEnabled = tempProxyTypesEnabled;

			ProxyTypesAssembly = !string.IsNullOrWhiteSpace(proxyTypesAssembly)
				? Assembly.Load(proxyTypesAssembly)
				: null;

			if (!string.IsNullOrWhiteSpace(callerId))
			{
				CallerId = new Guid(callerId);
			}

			ServiceConfigurationInstanceMode = !string.IsNullOrWhiteSpace(serviceConfigurationInstanceMode)
				? serviceConfigurationInstanceMode.ToEnum<ServiceConfigurationInstanceMode>()
				: _defaultServiceConfigurationInstanceMode;
		}

		private static ConnectionStringSettings GetConnectionStringSettings(string connectionStringName)
		{
			var settings = CrmConfigurationManager.CreateConnectionStringSettings(connectionStringName);

			if (settings == null) throw new ConfigurationErrorsException("Unable to find a connection string with the name '{0}'.".FormatWith(connectionStringName));

			return settings;
		}

		/// <summary>
		/// Parses a string in the <see cref="DbConnectionStringBuilder"/> format.
		/// </summary>
		/// <param name="connectionString"></param>
		/// <returns></returns>
		public static CrmConnection Parse(string connectionString)
		{
			return new CrmConnection(connectionString.ToDictionary());
		}

		/// <summary>
		/// Builds a text value that is unique to the connection values.
		/// </summary>
		/// <returns></returns>
		public string GetConnectionId()
		{
			if (ConnectionString != null)
			{
				var value = Encoding.UTF8.GetBytes(ConnectionString);
				using (var algorithm = SHA256.Create())
				{
					return Convert.ToBase64String(algorithm.ComputeHash(value));
				}
			}

			var client = GetUserName(ClientCredentials);
			var device = GetUserName(DeviceCredentials);
			var proxy = ProxyTypesAssembly != null ? ProxyTypesAssembly.ToString() : null;
			var text = "ServiceUri={0};HomeRealmUri={1};UserName={2};DeviceId={3};ProxyTypesEnabled={4};ProxyTypesAssembly={5};CallerId={6};ServiceConfigurationInstanceMode={7};UserTokenExpiryWindow={8};".FormatWith(
				ServiceUri, HomeRealmUri, client, device, ProxyTypesEnabled, proxy, CallerId, ServiceConfigurationInstanceMode, UserTokenExpiryWindow);
			return text;
		}

		private static string GetUserName(ClientCredentials credentials)
		{
			if (credentials == null) return null;

			if (credentials.UserName != null && credentials.UserName.UserName != null)
			{
				return credentials.UserName.UserName;
			}

			if (credentials.Windows != null && credentials.Windows.ClientCredential.UserName != null)
			{
				return credentials.Windows.ClientCredential.UserName + "@" + credentials.Windows.ClientCredential.Domain;
			}

			if (credentials.HttpDigest != null && credentials.HttpDigest.ClientCredential.UserName != null)
			{
				return credentials.HttpDigest.ClientCredential.UserName + "@" + credentials.HttpDigest.ClientCredential.Domain;
			}

			return null;
		}
	}
}
