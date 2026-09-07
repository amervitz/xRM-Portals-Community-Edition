/*
  Copyright (c) Microsoft Corporation. All rights reserved.
  Licensed under the MIT License. See License.txt in the project root for license information.
*/

using System;
using System.Collections.Concurrent;
using System.Collections.Specialized;
using System.Configuration;
using System.ServiceModel;
using System.ServiceModel.Description;
using System.ServiceModel.Security;
using System.Threading;
using Microsoft.Xrm.Client.Configuration;
using Microsoft.Xrm.Client.Runtime;
using Microsoft.Xrm.Client.Services.Samples;
using Microsoft.Xrm.Client.Threading;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Client;
using Microsoft.Xrm.Sdk.Query;

namespace Microsoft.Xrm.Client.Services
{
	/// <summary>
	/// An <see cref="IOrganizationService"/> that is constructed from a <see cref="CrmConnection"/>.
	/// </summary>
	public class OrganizationService : IDisposable, IOrganizationService, IInitializable
	{
		private static readonly string _organizationUri = "/XRMServices/2011/Organization.svc";
		private static IServiceConfiguration<IOrganizationService> _config;
		private static readonly ConcurrentDictionary<string, IServiceConfiguration<IOrganizationService>> _configLookup = new ConcurrentDictionary<string, IServiceConfiguration<IOrganizationService>>();
		private static readonly ConcurrentDictionary<string, SecurityTokenResponse> _userTokenLookup = new ConcurrentDictionary<string, SecurityTokenResponse>();
		private readonly InnerOrganizationService _service;

		internal static void Reset()
		{
			_config = null;
			_configLookup.Clear();
			_userTokenLookup.Clear();
		}

		/// <summary>
		/// The nested proxy service.
		/// </summary>
		public IOrganizationService InnerService
		{
			get { return _service.Value; }
		}

		public OrganizationService(string connectionStringName)
			: this(new CrmConnection(connectionStringName))
		{
		}

		public OrganizationService(CrmConnection connection)
		{
			_service = new InnerOrganizationService(error => ToOrganizationService(connection, error), connection.Timeout);
		}

		public OrganizationService(IOrganizationService service)
		{
			_service = new InnerOrganizationService(error => service, null);
		}

		/// <summary>
		/// Initializes custom settings.
		/// </summary>
		/// <param name="name"></param>
		/// <param name="config"></param>
		public virtual void Initialize(string name, NameValueCollection config)
		{
		}

		protected virtual IOrganizationService ToOrganizationService(CrmConnection connection)
		{
			return ToOrganizationService(connection, null);
		}

		protected virtual IOrganizationService ToOrganizationService(CrmConnection connection, Exception error)
		{
			var organizationServiceType = ConfigurationManager.AppSettings["OrganizationServiceType"];
			if (connection.ConnectionString == null
				|| string.IsNullOrWhiteSpace(organizationServiceType)
				|| organizationServiceType == "OrganizationServiceProxy")
			{
				return ToOrganizationServiceProxy(connection);
			}

			return OrganizationServiceClientFactory.Create(connection.ConnectionString, organizationServiceType);
		}

		protected virtual OrganizationServiceProxy ToOrganizationServiceProxy(CrmConnection connection)
		{
			connection.ThrowOnNull("connection");
			if (connection.ServiceUri == null) throw new ConfigurationErrorsException("The connection's 'ServiceUri' must be specified.");

			var clientCredentials = connection.ClientCredentials;

			OrganizationServiceProxy service;

			// retrieve the ServiceConfiguration from cache

			var config = GetServiceConfiguration(connection);

			var isClaimsMode = config.AuthenticationType == AuthenticationProviderType.Federation
				|| config.AuthenticationType == AuthenticationProviderType.OnlineFederation
				|| config.AuthenticationType == AuthenticationProviderType.LiveId;

			if (isClaimsMode && clientCredentials != null)
			{
				// get the user token for claims authentication

				var userTokenResponse = GetUserTokenResponse(connection, config);

				Assert(userTokenResponse != null && userTokenResponse.Token != null, "The user authentication failed!");

				service = new OrganizationServiceProxy(config, userTokenResponse);
			}
			else
			{
				// AD authentication

				service = new OrganizationServiceProxy(config, clientCredentials);
			}

			service.CallerId = connection.CallerId ?? Guid.Empty;

			if (connection.Timeout != null) service.Timeout = connection.Timeout.Value;

			return service;
		}

		protected virtual IServiceConfiguration<IOrganizationService> GetServiceConfiguration(CrmConnection connection)
		{
			var mode = connection.ServiceConfigurationInstanceMode;

			if (mode == ServiceConfigurationInstanceMode.Static)
			{
				return _config ?? (_config = CreateServiceConfiguration(connection));
			}

			if (mode == ServiceConfigurationInstanceMode.PerName)
			{
				var key = connection.GetConnectionId();

				if (!_configLookup.ContainsKey(key))
				{
					_configLookup[key] = CreateServiceConfiguration(connection);
				}

				return _configLookup[key];
			}

			if (mode == ServiceConfigurationInstanceMode.PerRequest && HttpSingleton<IServiceConfiguration<IOrganizationService>>.Enabled)
			{
				var key = connection.GetConnectionId();

				return HttpSingleton<IServiceConfiguration<IOrganizationService>>.GetInstance(key, () => CreateServiceConfiguration(connection));
			}

			var config = CreateServiceConfiguration(connection);
			return config;
		}

		protected virtual IServiceConfiguration<IOrganizationService> CreateServiceConfiguration(CrmConnection connection)
		{
			var uri = connection.ServiceUri;
			var fullServiceUri = uri.AbsolutePath.EndsWith(_organizationUri, StringComparison.OrdinalIgnoreCase)
				? uri
				: new Uri(uri, uri.AbsolutePath.TrimEnd('/') + _organizationUri);

			var proxyTypesEnabled = connection.ProxyTypesEnabled;
			var proxyTypesAssembly = connection.ProxyTypesAssembly;

			var config = ServiceConfigurationFactory.CreateConfiguration<IOrganizationService>(fullServiceUri);

			if (proxyTypesEnabled)
			{
				// configure the data contract surrogate at configuration time instead of at runtime

				var behavior = new ProxyTypesBehavior(proxyTypesAssembly) as IEndpointBehavior;

				behavior.ApplyClientBehavior(config.CurrentServiceEndpoint, null);
			}

			return config;
		}

		private static void Assert(bool condition, string message)
		{
			if (!condition)
			{
				throw new InvalidOperationException(message);
			}
		}

		protected virtual SecurityTokenResponse GetUserTokenResponse(CrmConnection connection, IServiceConfiguration<IOrganizationService> config)
		{
			var expiryWindow = connection.UserTokenExpiryWindow;

			if (expiryWindow != TimeSpan.Zero)
			{
				var key = connection.GetConnectionId();
				SecurityTokenResponse token;

				// check if the token has expired

				if (!_userTokenLookup.TryGetValue(key, out token) || CheckIfTokenIsExpired(token, expiryWindow))
				{
					token = CreateUserTokenResponse(connection, config);
					_userTokenLookup[key] = token;
				}

				return token;
			}

			var userTokenResponse = CreateUserTokenResponse(connection, config);
			return userTokenResponse;
		}

		protected bool CheckIfTokenIsExpired(SecurityTokenResponse token, TimeSpan? expiryWindow)
		{
			var now = DateTime.UtcNow;
			var duration = token.Token.ValidTo - token.Token.ValidFrom;

			if (expiryWindow == null || duration < expiryWindow.Value) return now >= token.Token.ValidTo;

			var expired = (now + expiryWindow.Value) > token.Token.ValidTo;

			return expired;
		}

		protected virtual SecurityTokenResponse CreateUserTokenResponse(CrmConnection connection, IServiceConfiguration<IOrganizationService> config)
		{
			var homeRealmUri = connection.HomeRealmUri;
			var clientCredentials = connection.ClientCredentials;
			var deviceCredentials = connection.DeviceCredentials;

			if (clientCredentials == null) throw new ConfigurationErrorsException("The connection's user credentials must be specified.");

			SecurityTokenResponse userTokenResponse;

			if (config.AuthenticationType == AuthenticationProviderType.LiveId)
			{
				if (deviceCredentials == null || deviceCredentials.UserName == null)
				{
					throw new ConfigurationErrorsException("The connection's device credentials must be specified.");
				}

				var deviceUserName = deviceCredentials.UserName.UserName;
				var devicePassword = deviceCredentials.UserName.Password;

				if (string.IsNullOrWhiteSpace(deviceUserName)) throw new ConfigurationErrorsException("The connection's device Id must be specified.");
				if (string.IsNullOrWhiteSpace(devicePassword)) throw new ConfigurationErrorsException("The connection's device password must be specified.");
				if (devicePassword.Length < 6) throw new ConfigurationErrorsException("The connection's device password must be at least 6 characters.");

				// prepend the DevicePrefix to the device Id

				var extendedDeviceCredentials = new ClientCredentials();
				extendedDeviceCredentials.UserName.UserName = DeviceIdManager.DevicePrefix + deviceCredentials.UserName.UserName;
				extendedDeviceCredentials.UserName.Password = deviceCredentials.UserName.Password;

				SecurityTokenResponse deviceTokenResponse;

				try
				{
					deviceTokenResponse = config.AuthenticateDevice(extendedDeviceCredentials);
				}
				catch (MessageSecurityException)
				{
					// try register the device credentials

					deviceTokenResponse = RegisterDeviceCredentials(deviceCredentials)
						// try re-authenticate
						? config.AuthenticateDevice(extendedDeviceCredentials)
						: null;
				}

				Assert(deviceTokenResponse != null && deviceTokenResponse.Token != null, "The device authentication failed!");

				userTokenResponse = config.Authenticate(clientCredentials, deviceTokenResponse);
			}
			else
			{
				if (homeRealmUri != null)
				{
					var appliesTo = config.PolicyConfiguration.SecureTokenServiceIdentifier;
					var homeRealmSecurityTokenResponse = config.AuthenticateCrossRealm(clientCredentials, appliesTo, homeRealmUri);

					Assert(homeRealmSecurityTokenResponse != null && homeRealmSecurityTokenResponse.Token != null, "The user authentication failed!");

					userTokenResponse = config.Authenticate(homeRealmSecurityTokenResponse.Token);
				}
				else
				{
					userTokenResponse = config.Authenticate(clientCredentials);
				}
			}

			return userTokenResponse;
		}

		protected bool RegisterDeviceCredentials(ClientCredentials deviceCredentials)
		{
			var response = DeviceIdManager.RegisterDevice(deviceCredentials);

			return response.IsSuccess;
		}

		#region IOrganizationService Members

		public virtual Guid Create(Entity entity)
		{
			return _service.UsingService(s => s.Create(entity));
		}

		public virtual Entity Retrieve(string entityName, Guid id, ColumnSet columnSet)
		{
			return _service.UsingService(s => s.Retrieve(entityName, id, columnSet));
		}

		public virtual void Update(Entity entity)
		{
			_service.UsingService(s => s.Update(entity));
		}

		public virtual void Delete(string entityName, Guid id)
		{
			_service.UsingService(s => s.Delete(entityName, id));
		}

		public virtual OrganizationResponse Execute(OrganizationRequest request)
		{
			return _service.UsingService(s => s.Execute(request));
		}

		public virtual void Associate(string entityName, Guid entityId, Relationship relationship, EntityReferenceCollection relatedEntities)
		{
			_service.UsingService(s => s.Associate(entityName, entityId, relationship, relatedEntities));
		}

		public virtual void Disassociate(string entityName, Guid entityId, Relationship relationship, EntityReferenceCollection relatedEntities)
		{
			_service.UsingService(s => s.Disassociate(entityName, entityId, relationship, relatedEntities));
		}

		public virtual EntityCollection RetrieveMultiple(QueryBase query)
		{
			return _service.UsingService(s => s.RetrieveMultiple(query));
		}

		#endregion

		#region IDisposable Members

		public virtual void Dispose()
		{
			var service = _service.Value as IDisposable;

			if (service != null)
			{
				service.Dispose();
			}
		}

		#endregion

		private class InnerOrganizationService
		{
			private readonly Func<Exception, IOrganizationService> _serviceFactory;
			private readonly ReaderWriterLockSlim _serviceLock = new ReaderWriterLockSlim();
			private readonly TimeSpan _serviceLockTimeout;

			private Lazy<IOrganizationService> _service;

			public InnerOrganizationService(Func<Exception, IOrganizationService> serviceFactory, TimeSpan? serviceLockTimeout)
			{
				if (serviceFactory == null)
				{
					throw new ArgumentNullException("serviceFactory");
				}

				_serviceFactory = serviceFactory;
				_service = new Lazy<IOrganizationService>(() => _serviceFactory(null));
				_serviceLockTimeout = serviceLockTimeout ?? TimeSpan.FromSeconds(30);
			}

			public IOrganizationService Value
			{
				get
				{
					if (!_serviceLock.TryEnterReadLock(_serviceLockTimeout))
					{
						throw new TimeoutException("Failed to acquire read lock on inner service.");
					}

					try
					{
						return _service.Value;
					}
					finally
					{
						_serviceLock.ExitReadLock();
					}
				}
			}

			public void UsingService(Action<IOrganizationService> action)
			{
				if (!_serviceLock.TryEnterReadLock(_serviceLockTimeout))
				{
					throw new TimeoutException("Failed to acquire read lock on inner service.");
				}

				try
				{
					try
					{
						action(_service.Value);
					}
					finally
					{
						_serviceLock.ExitReadLock();
					}
				}
				catch (FaultException<OrganizationServiceFault> e) when (e.Detail.ErrorCode == -2147176347)
				{
					ResetService(e, action);
				}
				catch (EndpointNotFoundException e)
				{
					ResetService(e, action);
				}
				catch (TimeoutException e)
				{
					ResetService(e, action);
				}
				catch (MessageSecurityException)
				{
					ResetService(null, action);
				}
			}

			public TResult UsingService<TResult>(Func<IOrganizationService, TResult> action)
			{
				if (!_serviceLock.TryEnterReadLock(_serviceLockTimeout))
				{
					throw new TimeoutException("Failed to acquire read lock on inner service.");
				}

				try
				{
					try
					{
						return action(_service.Value);
					}
					finally
					{
						_serviceLock.ExitReadLock();
					}
				}
				catch (FaultException<OrganizationServiceFault> e) when (e.Detail.ErrorCode == -2147176347)
				{
					return ResetService(e, action);
				}
				catch (EndpointNotFoundException e)
				{
					return ResetService(e, action);
				}
				catch (TimeoutException e)
				{
					return ResetService(e, action);
				}
				catch (MessageSecurityException)
				{
					return ResetService(null, action);
				}
			}

			private void ResetService(Exception e, Action<IOrganizationService> action)
			{
				ResetService(e);

				if (!_serviceLock.TryEnterReadLock(_serviceLockTimeout))
				{
					throw new TimeoutException("Failed to acquire read lock on inner service.");
				}

				try
				{
					action(_service.Value);
				}
				finally
				{
					_serviceLock.ExitReadLock();
				}
			}

			private TResult ResetService<TResult>(Exception e, Func<IOrganizationService, TResult> action)
			{
				ResetService(e);

				if (!_serviceLock.TryEnterReadLock(_serviceLockTimeout))
				{
					throw new TimeoutException("Failed to acquire read lock on inner service.");
				}

				try
				{
					return action(_service.Value);
				}
				finally
				{
					_serviceLock.ExitReadLock();
				}
			}

			private void ResetService(Exception e)
			{
				if (!_serviceLock.TryEnterWriteLock(_serviceLockTimeout))
				{
					throw new TimeoutException("Failed to acquire write lock on inner service.");
				}

				try
				{
					_service = new Lazy<IOrganizationService>(() => _serviceFactory(e));
				}
				finally
				{
					_serviceLock.ExitWriteLock();
				}
			}
		}
	}
}
