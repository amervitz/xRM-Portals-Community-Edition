/*
  Copyright (c) Microsoft Corporation. All rights reserved.
  Licensed under the MIT License. See License.txt in the project root for license information.
*/

namespace Adxstudio.Xrm.Services
{
	using System;
	using System.Diagnostics;
	using System.Diagnostics.Tracing;
	using System.Net;
	using System.ServiceModel;
	using System.ServiceModel.Channels;
	using System.ServiceModel.Description;
	using System.ServiceModel.Dispatcher;
	using Adxstudio.Xrm.AspNet.Cms;
	using Adxstudio.Xrm.Diagnostics.Metrics;
	using Adxstudio.Xrm.Diagnostics.Trace;
	using Adxstudio.Xrm.Performance;
	using Adxstudio.Xrm.Web;
	using Microsoft.Xrm.Client;
	using Microsoft.Xrm.Client.Configuration;
	using Microsoft.Xrm.Client.Services;
	using Microsoft.Xrm.Sdk;
	using Microsoft.Xrm.Sdk.Client;
	using Microsoft.Xrm.Sdk.Messages;
	using Microsoft.Xrm.Sdk.Query;

	public class CachedOrganizationService : OrganizationService, IOrganizationServiceCacheContainer
	{
		private class PortalClientMessageInspector : IClientMessageInspector
		{
			public object BeforeSendRequest(ref Message request, IClientChannel channel)
			{
				var site = HashPii.ComputeHashPiiSha256(WebAppSettings.Instance.SiteName);
				var instance = HashPii.ComputeHashPiiSha256(WebAppSettings.Instance.InstanceId);
				var userAgent = $"Portals (Site={site}; Instance={instance}; ActivityId={EventSource.CurrentThreadActivityId})";

				var property = new HttpRequestMessageProperty
				{
					Headers = { { HttpRequestHeader.UserAgent, userAgent } }
				};

				request.Properties.Add(HttpRequestMessageProperty.Name, property);

				return null;
			}

			public void AfterReceiveReply(ref Message reply, object correlationState)
			{
			}
		}

		private class PortalEndpointBehavior : IEndpointBehavior
		{
			public void Validate(ServiceEndpoint endpoint)
			{
			}

			public void AddBindingParameters(ServiceEndpoint endpoint, BindingParameterCollection bindingParameters)
			{
			}

			public void ApplyDispatchBehavior(ServiceEndpoint endpoint, EndpointDispatcher endpointDispatcher)
			{
			}

			public void ApplyClientBehavior(ServiceEndpoint endpoint, ClientRuntime clientRuntime)
			{
				clientRuntime.ClientMessageInspectors.Add(new PortalClientMessageInspector());
			}
		}

		/// <summary>
		/// The cache.
		/// </summary>
		public IOrganizationServiceCache Cache { get; private set; }

		public CachedOrganizationService(string connectionStringName)
			: this(new CrmConnection(connectionStringName))
		{
		}

		public CachedOrganizationService(CrmConnection connection)
			: this(connection, CreateOrganizationServiceCache(connection.GetConnectionId()))
		{
		}

		public CachedOrganizationService(IOrganizationService service)
			: this(service, (string)null)
		{
		}

		public CachedOrganizationService(IOrganizationService service, string connectionId)
			: this(service, CreateOrganizationServiceCache(connectionId))
		{
		}

		public CachedOrganizationService(string connectionStringName, IOrganizationServiceCache cache)
			: this(new CrmConnection(connectionStringName), cache)
		{
		}

		public CachedOrganizationService(CrmConnection connection, IOrganizationServiceCache cache)
			: base(connection)
		{
			this.Cache = cache;
		}

		public CachedOrganizationService(IOrganizationService service, IOrganizationServiceCache cache)
			: base(service)
		{
			this.Cache = cache;
		}

		private static IOrganizationServiceCache CreateOrganizationServiceCache(string connectionId)
		{
			return CrmConfigurationManager.CreateServiceCache(null, connectionId, true);
		}

		public override Guid Create(Entity entity)
		{
			Guid guid;
			var stopwatch = Stopwatch.StartNew();

			try
			{
				using (PerformanceProfiler.Instance.StartMarker(PerformanceMarkerName.Services, PerformanceMarkerArea.Crm, PerformanceMarkerTagName.CreateEntity))
				{
					guid = base.Create(entity);
				}
			}
			finally
			{
				stopwatch.Stop();

				MdmMetrics.CrmOrganizationRequestExecutionTimeMetric.LogValue(stopwatch.ElapsedMilliseconds);
				ServicesEventSource.Log.Create(entity, stopwatch.ElapsedMilliseconds);
			}

			if (this.Cache != null)
			{
				this.Cache.Remove(entity);
			}

			return guid;
		}

		public override void Update(Entity entity)
		{
			var stopwatch = Stopwatch.StartNew();

			try
			{
				using (PerformanceProfiler.Instance.StartMarker(PerformanceMarkerName.Services, PerformanceMarkerArea.Crm, PerformanceMarkerTagName.UpdateEntity))
				{
					base.Update(entity);
				}
			}
			finally
			{
				stopwatch.Stop();

				MdmMetrics.CrmOrganizationRequestExecutionTimeMetric.LogValue(stopwatch.ElapsedMilliseconds);
				ServicesEventSource.Log.Update(entity, stopwatch.ElapsedMilliseconds);
			}

			if (this.Cache != null)
			{
				this.Cache.Remove(entity);
			}
		}

		public override void Delete(string entityName, Guid id)
		{
			var stopwatch = Stopwatch.StartNew();

			try
			{
				using (PerformanceProfiler.Instance.StartMarker(PerformanceMarkerName.Services, PerformanceMarkerArea.Crm, PerformanceMarkerTagName.DeleteEntity))
				{
					base.Delete(entityName, id);
				}
			}
			finally
			{
				stopwatch.Stop();

				MdmMetrics.CrmOrganizationRequestExecutionTimeMetric.LogValue(stopwatch.ElapsedMilliseconds);
				ServicesEventSource.Log.Delete(entityName, id, stopwatch.ElapsedMilliseconds);
			}

			if (this.Cache != null)
			{
				this.Cache.Remove(entityName, id);
			}
		}

		public override void Associate(string entityName, Guid entityId, Relationship relationship, EntityReferenceCollection relatedEntities)
		{
			var stopwatch = Stopwatch.StartNew();

			try
			{
				using (PerformanceProfiler.Instance.StartMarker(PerformanceMarkerName.Services, PerformanceMarkerArea.Crm, PerformanceMarkerTagName.AssociateEntity))
				{
					base.Associate(entityName, entityId, relationship, relatedEntities);
				}
			}
			finally
			{
				stopwatch.Stop();

				MdmMetrics.CrmOrganizationRequestExecutionTimeMetric.LogValue(stopwatch.ElapsedMilliseconds);
				ServicesEventSource.Log.Associate(entityName, entityId, relationship, relatedEntities, stopwatch.ElapsedMilliseconds);
			}
		}

		public override void Disassociate(string entityName, Guid entityId, Relationship relationship, EntityReferenceCollection relatedEntities)
		{
			var stopwatch = Stopwatch.StartNew();

			try
			{
				using (PerformanceProfiler.Instance.StartMarker(PerformanceMarkerName.Services, PerformanceMarkerArea.Crm, PerformanceMarkerTagName.DisassociateEntity))
				{
					base.Disassociate(entityName, entityId, relationship, relatedEntities);
				}
			}
			finally
			{
				stopwatch.Stop();

				MdmMetrics.CrmOrganizationRequestExecutionTimeMetric.LogValue(stopwatch.ElapsedMilliseconds);
				ServicesEventSource.Log.Disassociate(entityName, entityId, relationship, relatedEntities, stopwatch.ElapsedMilliseconds);
			}
		}

		public override Entity Retrieve(string entityName, Guid id, ColumnSet columnSet)
		{
			// telemetry will be handled during InnerExecute for uncached requests

			var request = new RetrieveRequest
			{
				Target = new EntityReference(entityName, id),
				ColumnSet = columnSet
			};

			var response = this.Execute<RetrieveResponse>(request);

			return response != null ? response.Entity : null;
		}

		public override EntityCollection RetrieveMultiple(QueryBase query)
		{
			// telemetry will be handled during InnerExecute for uncached requests

			var request = new RetrieveMultipleRequest { Query = query };
			var response = this.Execute<RetrieveMultipleResponse>(request);
			return response != null ? response.EntityCollection : null;
		}

		public override OrganizationResponse Execute(OrganizationRequest request)
		{
			// telemetry will be handled during InnerExecute for uncached requests

			return this.Execute<OrganizationResponse>(request);
		}

		protected override IServiceConfiguration<IOrganizationService> CreateServiceConfiguration(CrmConnection connection)
		{
			using (PerformanceProfiler.Instance.StartMarker(PerformanceMarkerName.Services, PerformanceMarkerArea.Crm, PerformanceMarkerTagName.CreateServiceConfiguration))
			{
				var config = base.CreateServiceConfiguration(connection);

				if (connection.ProxyTypesEnabled)
				{
					// configure the data contract surrogate at configuration time instead of at runtime

					var behavior = config.CurrentServiceEndpoint.Behaviors.Remove<ProxyTypesBehavior>() as IEndpointBehavior;

					if (behavior != null)
					{
						behavior.ApplyClientBehavior(config.CurrentServiceEndpoint, null);
					}
				}

				var portalBehavior = config.CurrentServiceEndpoint.Behaviors.Find<PortalEndpointBehavior>();

				if (portalBehavior == null)
				{
					config.CurrentServiceEndpoint.Behaviors.Add(new PortalEndpointBehavior());
				}

				return config;
			}
		}

		private T Execute<T>(OrganizationRequest request) where T : OrganizationResponse
		{
			return this.Execute(request, response => response as T, null);
		}

		private T Execute<T>(OrganizationRequest request, Func<OrganizationResponse, T> selector, string selectorCacheKey)
		{
			var execute = this.Cache != null
				? this.Cache.Execute
				: (Func<OrganizationRequest, Func<OrganizationRequest, OrganizationResponse>, Func<OrganizationResponse, T>, string, T>)null;

			return this.Execute(request, execute, selector, selectorCacheKey);
		}

		private T Execute<T>(OrganizationRequest request, Func<OrganizationRequest, Func<OrganizationRequest, OrganizationResponse>, Func<OrganizationResponse, T>, string, T> execute, Func<OrganizationResponse, T> selector, string selectorCacheKey)
		{
			return execute == null
				? selector(this.InnerExecute(request))
				: execute(ToCachedOrganizationRequest(request), this.InnerExecute, selector, selectorCacheKey);
		}

		private static CachedOrganizationRequest ToCachedOrganizationRequest(OrganizationRequest request)
		{
			// wrap the request in a container with telemetry
			var cachedRequest = request as CachedOrganizationRequest;
			return cachedRequest ?? new CachedOrganizationRequest(request, default(Caller));
		}

		/// <summary>
		/// Execute an uncached request.
		/// </summary>
		/// <param name="request">The request.</param>
		/// <returns>The response.</returns>
		private OrganizationResponse InnerExecute(OrganizationRequest request)
		{
			var stopwatch = Stopwatch.StartNew();

			// unwrap the original request from the container
			var cached = request as CachedOrganizationRequest;
			var innerRequest = cached != null ? cached.Request : request;

			try
			{
				using (PerformanceProfiler.Instance.StartMarker(PerformanceMarkerName.Services, PerformanceMarkerArea.Crm, PerformanceMarkerTagName.ExecuteOrganizationService))
				{
					var keyed = innerRequest as KeyedRequest;
					var keyedInner = keyed != null ? keyed.Request : innerRequest;

					// Keep portal wrappers for caching/telemetry, but pass a typed request to
					// SDK clients whose execution and retry paths cast RetrieveMultiple requests.
					var sdkRequest = keyedInner;
					if (keyedInner is FetchMultipleRequest || keyedInner is RetrieveSingleRequest)
					{
						sdkRequest = new RetrieveMultipleRequest
						{
							Parameters = keyedInner.Parameters,
							RequestId = keyedInner.RequestId,
							ExtensionData = keyedInner.ExtensionData
						};
					}

					var response = base.Execute(sdkRequest);

					var rsr = keyedInner as RetrieveSingleRequest;

					if (rsr != null)
					{
						var rmr = response as RetrieveMultipleResponse;

						if (rmr != null)
						{
							return new RetrieveSingleResponse(rsr, rmr);
						}
					}

					return response;
				}
			}
			catch (Exception e) when (LogGenericWarningException(e, cached))
			{
				throw;
			}
			finally
			{
				stopwatch.Stop();

				MdmMetrics.CrmOrganizationRequestExecutionTimeMetric.LogValue(stopwatch.ElapsedMilliseconds);
				ServicesEventSource.Log.OrganizationRequest(innerRequest, stopwatch.ElapsedMilliseconds, false);

				if (cached != null && cached.Telemetry != null)
				{
					cached.Telemetry.Duration = stopwatch.Elapsed;
				}
			}
		}

		private static bool LogGenericWarningException(
			Exception e,
			CachedOrganizationRequest request,
			[System.Runtime.CompilerServices.CallerMemberName] string memberName = "",
			[System.Runtime.CompilerServices.CallerFilePath] string sourceFilePath = "",
			[System.Runtime.CompilerServices.CallerLineNumber] int sourceLineNumber = 0)
		{
			// use caller's debug arguments if available
			var telemetry = request?.Telemetry;

			if (telemetry != null)
			{
				WebEventSource.Log.GenericWarningException(e, null, telemetry.Caller.MemberName, telemetry.Caller.SourceFilePath, telemetry.Caller.SourceLineNumber);
			}
			else
			{
				WebEventSource.Log.GenericWarningException(e, null, memberName, sourceFilePath, sourceLineNumber);
			}

			return false;
		}
	}
}
