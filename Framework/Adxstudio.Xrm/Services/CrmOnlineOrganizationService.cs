/*
  Copyright (c) Microsoft Corporation. All rights reserved.
  Licensed under the MIT License. See License.txt in the project root for license information.
*/

namespace Adxstudio.Xrm.Services
{
	using System;
	using System.Collections.Generic;
	using System.Collections.Specialized;
	using System.Linq;
	
	
	using Polly;
	using Adxstudio.Xrm.Threading;
	using Microsoft.Xrm.Client;
	using Microsoft.Xrm.Client.Services;
	using Microsoft.Xrm.Sdk;
	using Microsoft.Xrm.Sdk.Messages;
	using Microsoft.Xrm.Sdk.Query;

	/// <summary>
	/// An <see cref="IOrganizationService"/> that includes transient fault handling capabilities.
	/// </summary>
	/// <remarks>
	/// Configuration format. The 'retryStrategyName' is the name of a strategy defined by the Transient Fault Handling Application Block configuration.
	/// <code>
	/// <![CDATA[
	/// <configuration>
	/// 
	///  <configSections>
	///   <section name="microsoft.xrm.client" type="Microsoft.Xrm.Client.Configuration.CrmSection, Microsoft.Xrm.Client"/>
	///  </configSections>
	/// 
	///  <microsoft.xrm.client>
	///   <services>
	///    <add
	///     name="Xrm"
	///     type="Adxstudio.Xrm.Services.CrmOnlineOrganizationService, Adxstudio.Xrm"
	///     retryCount="3"
	///     retryInterval="00:00:00" [HH:MM:SS]
	///     retryStrategyName=""
	///     />
	///   </services>
	///  </microsoft.xrm.client>
	///  
	/// </configuration>
	/// ]]>
	/// </code>
	/// </remarks>
	/// <seealso cref="Microsoft.Xrm.Client.Configuration.CrmConfigurationManager"/>
	public class CrmOnlineOrganizationService : CachedOrganizationService
	{
		/// <summary>
		/// The <see cref="ResiliencePipeline"/> used to handle read request faults.
		/// </summary>
		public ResiliencePipeline ReadRetryPolicy { get; set; }

		/// <summary>
		/// The <see cref="ResiliencePipeline"/> used to handle non-read request faults.
		/// </summary>
		public ResiliencePipeline DefaultRetryPolicy { get; set; }

		public CrmOnlineOrganizationService(string connectionStringName) : base(connectionStringName)
		{
		}

		public CrmOnlineOrganizationService(CrmConnection connection) : base(connection)
		{
		}

		public CrmOnlineOrganizationService(IOrganizationService service) : base(service)
		{
		}

		public CrmOnlineOrganizationService(IOrganizationService service, string connectionId) : base(service, connectionId)
		{
		}

		public CrmOnlineOrganizationService(string connectionStringName, IOrganizationServiceCache cache) : base(connectionStringName, cache)
		{
		}

		public CrmOnlineOrganizationService(CrmConnection connection, IOrganizationServiceCache cache) : base(connection, cache)
		{
		}

		public CrmOnlineOrganizationService(IOrganizationService service, IOrganizationServiceCache cache) : base(service, cache)
		{
		}

		public override void Initialize(string name, NameValueCollection config)
		{
			base.Initialize(name, config);

			var retryStrategy = GetRetryStrategy(name, config);

			// get the read strategy

			var readDetectionStrategy = GetReadTransientErrorDetectionStrategy(name, config);
			var readRetryPolicy = GetRetryPolicy(readDetectionStrategy, retryStrategy);

			ReadRetryPolicy = readRetryPolicy;

			// get the write strategy

			var defaultDetectionStrategy = GetDefaultTransientErrorDetectionStrategy(name, config);
			var defaultRetryPolicy = GetRetryPolicy(defaultDetectionStrategy, retryStrategy);

			DefaultRetryPolicy = defaultRetryPolicy;
		}

		public override Guid Create(Entity entity)
		{
			var policy = DefaultRetryPolicy;

			return policy != null
				? policy.Execute(() => base.Create(entity))
				: base.Create(entity);
		}

		public override Entity Retrieve(string entityName, Guid id, ColumnSet columnSet)
		{
			var policy = ReadRetryPolicy;

			return policy != null
				? policy.Execute(() => base.Retrieve(entityName, id, columnSet))
				: base.Retrieve(entityName, id, columnSet);
		}

		public override void Update(Entity entity)
		{
			var policy = DefaultRetryPolicy;

			if (policy != null)
			{
				policy.Execute(() => base.Update(entity));
			}
			else
			{
				base.Update(entity);
			}
		}

		public override void Delete(string entityName, Guid id)
		{
			var policy = DefaultRetryPolicy;

			if (policy != null)
			{
				policy.Execute(() => base.Delete(entityName, id));
			}
			else
			{
				base.Delete(entityName, id);
			}
		}

		public override OrganizationResponse Execute(OrganizationRequest request)
		{
			var policy = IsReadRequest(request) ? ReadRetryPolicy : DefaultRetryPolicy;

			return policy != null
				? policy.Execute(() => base.Execute(request))
				: base.Execute(request);
		}

		public override void Associate(string entityName, Guid entityId, Relationship relationship, EntityReferenceCollection relatedEntities)
		{
			var policy = DefaultRetryPolicy;

			if (policy != null)
			{
				policy.Execute(() => base.Associate(entityName, entityId, relationship, relatedEntities));
			}
			else
			{
				base.Associate(entityName, entityId, relationship, relatedEntities);
			}
		}

		public override void Disassociate(string entityName, Guid entityId, Relationship relationship, EntityReferenceCollection relatedEntities)
		{
			var policy = DefaultRetryPolicy;

			if (policy != null)
			{
				policy.Execute(() => base.Disassociate(entityName, entityId, relationship, relatedEntities));
			}
			else
			{
				base.Disassociate(entityName, entityId, relationship, relatedEntities);
			}
		}

		public override EntityCollection RetrieveMultiple(QueryBase query)
		{
			var policy = ReadRetryPolicy;

			return policy != null
				? policy.Execute(() => base.RetrieveMultiple(query))
				: base.RetrieveMultiple(query);
		}

		protected virtual Func<Exception, bool> GetReadTransientErrorDetectionStrategy(string name, NameValueCollection config)
		{
			return new CrmOnlineReadTransientErrorDetectionStrategy().IsTransient;
		}

		protected virtual Func<Exception, bool> GetDefaultTransientErrorDetectionStrategy(string name, NameValueCollection config)
		{
			return new CrmOnlineTransientErrorDetectionStrategy().IsTransient;
		}

		protected virtual IEnumerable<TimeSpan> GetRetryStrategy(string name, NameValueCollection config)
		{
			var retryStrategyName = config["retryStrategyName"];

			if (!string.IsNullOrWhiteSpace(retryStrategyName))
			{
				throw new System.Configuration.ConfigurationErrorsException("Enterprise Library named retry strategies have been retired. Configure retryCount and retryInterval on the organization service instead.");
			}

			int count;
			var retryCount = int.TryParse(config["retryCount"], out count) ? count : 3;

			TimeSpan interval;
			var retryInterval = TimeSpan.TryParse(config["retryInterval"], out interval) ? interval : TimeSpan.Zero;

			// Enterprise Library's FixedInterval delayed every retry, including the first, which is what
			// backs off from a throttling organization service.
			return RetryPolicies.FixedInterval(retryCount, retryInterval);
		}

		protected virtual ResiliencePipeline GetRetryPolicy(Func<Exception, bool> detectionStrategy, IEnumerable<TimeSpan> retryStrategy)
		{
			return RetryPolicies.Create(detectionStrategy, retryStrategy);
		}

		protected virtual bool IsReadRequest(object request)
		{
			return request != null && Array.BinarySearch(_cachedRequestsSorted, request.GetType().GetHashCode()) >= 0;
		}

		private static readonly IEnumerable<Type> _cachedRequestsContent = new[]
		{
			typeof(KeyedRequest),
			typeof(RetrieveRequest),
			typeof(RetrieveMultipleRequest),
			typeof(RetrieveSingleRequest),
		};

		private static readonly IEnumerable<Type> _cachedRequestsMetadata = new[]
		{
			typeof(RetrieveAllEntitiesRequest),
			typeof(RetrieveAllOptionSetsRequest),
			typeof(RetrieveAllManagedPropertiesRequest),
			typeof(RetrieveAttributeRequest),
			typeof(RetrieveEntityRequest),
			typeof(RetrieveRelationshipRequest),
			typeof(RetrieveTimestampRequest),
			typeof(RetrieveOptionSetRequest),
			typeof(RetrieveManagedPropertyRequest),
		};

		private static readonly IEnumerable<Type> _cachedRequests = _cachedRequestsContent.Concat(_cachedRequestsMetadata);

		private static readonly int[] _cachedRequestsSorted = _cachedRequests.Select(t => t.GetHashCode()).OrderBy(t => t).ToArray();
	}
}
