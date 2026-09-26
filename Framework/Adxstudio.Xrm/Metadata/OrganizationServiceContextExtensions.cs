/*
  Copyright (c) Microsoft Corporation. All rights reserved.
  Licensed under the MIT License. See License.txt in the project root for license information.
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using Adxstudio.Xrm.Globalization;
using Adxstudio.Xrm.Performance;
using Adxstudio.Xrm.Resources;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Client;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk.Metadata.Query;
using Microsoft.Xrm.Sdk.Query;

namespace Adxstudio.Xrm.Metadata
{
	/// <summary>
	/// Helper methods for getting metadata
	/// </summary>
	public static class OrganizationServiceContextExtensions
	{
		/// <summary>
		/// Retrieve <see cref="EntityMetadata"/>
		/// </summary>
		/// <param name="context"><see cref="OrganizationServiceContext" /></param>
		/// <param name="logicalName">Logical name of the entity</param>
		/// <param name="entityFilters"><see cref="EntityFilters"/></param>
		/// <returns><see cref="EntityMetadata"/></returns>
		public static EntityMetadata GetEntityMetadata(this OrganizationServiceContext context, string logicalName, EntityFilters entityFilters)
		{
			RetrieveEntityResponse response;
			using (PerformanceProfiler.Instance.StartMarker(PerformanceMarkerName.Metadata, PerformanceMarkerArea.Crm, PerformanceMarkerTagName.GetEntityMetadata))
			{
				response = context.Execute(new RetrieveEntityRequest { LogicalName = logicalName, EntityFilters = entityFilters }) as RetrieveEntityResponse;
			}

			if (response == null)
			{
				throw new InvalidOperationException("Unable to retrieve the entity metadata for {0}.".FormatWith(logicalName));
			}

			return response.EntityMetadata;
		}

		/// <summary>
		/// Retrieve <see cref="EntityMetadata"/> with entity information plus attributes
		/// </summary>
		/// <param name="context"><see cref="OrganizationServiceContext" /></param>
		/// <param name="logicalName">Logical name of the entity</param>
		/// <returns><see cref="EntityMetadata"/></returns>
		public static EntityMetadata GetEntityMetadata(this OrganizationServiceContext context, string logicalName)
		{
			return GetEntityMetadata(context, logicalName, EntityFilters.Attributes);
		}
		
		/// <summary>
		/// Retrieve <see cref="EntityMetadata"/>
		/// </summary>
		/// <param name="context"><see cref="OrganizationServiceContext" /></param>
		/// <param name="logicalName">Logical name of the entity</param>
		/// <param name="metadataCache">Cache dictionary</param>
		/// <returns><see cref="EntityMetadata"/></returns>
		public static EntityMetadata GetEntityMetadata(this OrganizationServiceContext context, string logicalName, IDictionary<string, EntityMetadata> metadataCache)
		{
			EntityMetadata cachedMetadata;

			if (metadataCache.TryGetValue(logicalName, out cachedMetadata))
			{
				return cachedMetadata;
			}

			var entityMetadata = GetEntityMetadata(context, logicalName);

			metadataCache[logicalName] = entityMetadata;

			return entityMetadata;
		}

		/// <summary>
		/// Determines whether an entity exists.
		/// </summary>
		/// <param name="serviceContext"><see cref="OrganizationServiceContext" /></param>
		/// <param name="logicalName">Logical name of the entity</param>
		/// <returns>True if the entity exists</returns>
		/// <remarks>
		/// A <see cref="RetrieveEntityRequest"/> for a missing entity throws a fault, which the organization service cache doesn't
		/// keep, so every check would go to Dataverse. <see cref="RetrieveMetadataChangesRequest"/> returns no metadata instead,
		/// and its response is cached until the next metadata change or publish.
		/// </remarks>
		internal static bool EntityExists(this OrganizationServiceContext serviceContext, string logicalName)
		{
			if (serviceContext == null) throw new ArgumentNullException("serviceContext");
			if (logicalName == null) throw new ArgumentNullException("logicalName");

			var entityFilter = new MetadataFilterExpression(LogicalOperator.And);
			entityFilter.Conditions.Add(new MetadataConditionExpression("LogicalName", MetadataConditionOperator.Equals, logicalName));

			var response = (RetrieveMetadataChangesResponse)serviceContext.Execute(new RetrieveMetadataChangesRequest
			{
				Query = new EntityQueryExpression
				{
					Criteria = entityFilter,
					Properties = new MetadataPropertiesExpression("LogicalName")
				}
			});

			return response.EntityMetadata.Any();
		}

		internal static string GetEntityPrimaryName(this OrganizationServiceContext serviceContext, Entity entity)
		{
			if (serviceContext == null) throw new ArgumentNullException("serviceContext");
			if (entity == null) throw new ArgumentNullException("entity");

			var entityFilter = new MetadataFilterExpression(LogicalOperator.And);
			entityFilter.Conditions.Add(new MetadataConditionExpression("LogicalName", MetadataConditionOperator.Equals, entity.LogicalName));

			try
			{
				EntityMetadata entityMetadata;
				using (PerformanceProfiler.Instance.StartMarker(PerformanceMarkerName.Metadata, PerformanceMarkerArea.Crm, PerformanceMarkerTagName.GetEntityPrimaryName))
				{
					var response = (RetrieveMetadataChangesResponse)serviceContext.Execute(new RetrieveMetadataChangesRequest
					{
						Query = new EntityQueryExpression
						{
							Criteria = entityFilter,
							Properties = new MetadataPropertiesExpression("PrimaryNameAttribute")
						}
					});

					entityMetadata = response.EntityMetadata.FirstOrDefault();
				}

				return entityMetadata == null
					? null
					: entity.GetAttributeValue<string>(entityMetadata.PrimaryNameAttribute);
			}
			catch (FaultException<OrganizationServiceFault>)
			{
				return null;
			}
		}

	    public static DataCollection<Entity> GetMultipleSystemFormsWithAllLabels(FilterExpression filterExpression, OrganizationServiceContext context)
	    {
            QueryExpression qExpression = new QueryExpression();
            qExpression.ColumnSet.AllColumns = true;
            qExpression.Criteria = filterExpression;
            qExpression.EntityName = "systemform";

            OrganizationRequest req = new OrganizationRequest();
            req.RequestName = "RetrieveMultipleSystemFormsWithAllLabels";
            req.Parameters.Add("Query", qExpression);

			EntityCollection entities;
			using (PerformanceProfiler.Instance.StartMarker(PerformanceMarkerName.Metadata, PerformanceMarkerArea.Crm, PerformanceMarkerTagName.GetSystemFormEntityWithAllLabels))
			{
				var response = context.Execute(req);
				entities = (EntityCollection)response.Results.Values.First();
			}

		    return entities.Entities;

	    }

		internal static string GetEntityPrimaryNameWithAttributeLabel(this OrganizationServiceContext serviceContext, Entity entity, string attributeLogicalName)
		{
			if (serviceContext == null) throw new ArgumentNullException("serviceContext");
			if (entity == null) throw new ArgumentNullException("entity");
			if (attributeLogicalName == null) throw new ArgumentNullException("attributeLogicalName");

			var entityFilter = new MetadataFilterExpression(LogicalOperator.And);
			entityFilter.Conditions.Add(new MetadataConditionExpression("LogicalName", MetadataConditionOperator.Equals, entity.LogicalName));

			var attributeFilter = new MetadataFilterExpression(LogicalOperator.Or);
			attributeFilter.Conditions.Add(new MetadataConditionExpression("LogicalName", MetadataConditionOperator.Equals, attributeLogicalName));

			try
			{
				AttributeMetadata attributeMetadata;
				string entityName;
				using (PerformanceProfiler.Instance.StartMarker(PerformanceMarkerName.Metadata, PerformanceMarkerArea.Crm, PerformanceMarkerTagName.GetEntityPrimaryNameWithAttributeLabel))
				{
					var response = (RetrieveMetadataChangesResponse)serviceContext.Execute(new RetrieveMetadataChangesRequest
					{
						Query = new EntityQueryExpression
						{
							Criteria = entityFilter,
							Properties = new MetadataPropertiesExpression("PrimaryNameAttribute", "Attributes"),
							AttributeQuery = new AttributeQueryExpression
							{
								Criteria = attributeFilter,
								Properties = new MetadataPropertiesExpression("LogicalName", "DisplayName")
							}
						}
					});

					var entityMetadata = response.EntityMetadata.FirstOrDefault();

					if (entityMetadata == null)
					{
						return null;
					}

					attributeMetadata = entityMetadata.Attributes.FirstOrDefault(e => string.Equals(e.LogicalName, attributeLogicalName, StringComparison.InvariantCultureIgnoreCase));

					if (attributeMetadata == null)
					{
						return null;
					}

					entityName = entity.GetAttributeValue<string>(entityMetadata.PrimaryNameAttribute);
				}

				return string.IsNullOrWhiteSpace(entityName)
					? attributeMetadata.DisplayName.GetLocalizedLabelString()
					: string.Format("{0} ({1})", attributeMetadata.DisplayName.GetLocalizedLabelString(), entityName);
			}
			catch (FaultException<OrganizationServiceFault>)
			{
				return null;
			}
		}

		/// <summary>
		/// Executes the <see cref="RetrieveLocLabelsRequest"/> to retrieve localized label for a limited set of entity attributes.
		/// This message only supports the following entity attributes or localized property values
		/// DynamicProperty.Name
		/// DynamicPropertyOptionSetItem.DynamicPropertyOptionName
		/// DynamicPropertyOptionSetItem.DynamicPropertyOptionDescription
		/// Product.Name
		/// SavedQuery.Description
		/// SavedQuery.Name
		/// SavedQueryVisualization.Description
		/// SavedQueryVisualization.Name
		/// SystemForm.Description
		/// SystemForm.Name
		/// TimeZoneDefinition.UserInterfaceName
		/// TimeZoneDefinition.StandardName
		/// TimeZoneLocalizedName.UserInterfaceName
		/// </summary>
		/// <param name="serviceContext">The <see cref="OrganizationServiceContext"/> used to execute the <see cref="RetrieveLocLabelsRequest"/>.</param>
		/// <param name="entityMoniker">An <see cref="EntityReference"/> of the record to retrieve the label from.</param>
		/// <param name="attributeLogicalName">The logicalname of the attribute to retrieve the localized label.</param>
		/// <param name="languageCode">The locale of the label to retrieve.</param>
		/// <param name="includeUnpublished">Indicates whether to include unpublished labels.</param>
		/// <returns>The localized label for the locale specified by the languageCode.</returns>
		internal static string RetrieveLocalizedLabel(this OrganizationServiceContext serviceContext, EntityReference entityMoniker, string attributeLogicalName, int languageCode, bool includeUnpublished = false)
		{
			if (entityMoniker == null || string.IsNullOrWhiteSpace(attributeLogicalName))
			{
				return string.Empty;
			}

			var response = (RetrieveLocLabelsResponse)serviceContext.Execute(new RetrieveLocLabelsRequest
			{
				EntityMoniker = entityMoniker,
				AttributeName = attributeLogicalName,
				IncludeUnpublished = includeUnpublished
			});

			if (response == null || response.Label == null)
			{
				return string.Empty;
			}

			return GetLocalizedLabel(response.Label, languageCode);
		}

		internal static string GetLocalizedLabel(Label label, int languageCode)
		{
			var localizedLabel = label.LocalizedLabels.FirstOrDefault(l => l.LanguageCode == languageCode);

			if (localizedLabel != null)
			{
				return localizedLabel.Label;
			}

			return label.UserLocalizedLabel != null ? label.UserLocalizedLabel.Label : null;
		}
	}
}
