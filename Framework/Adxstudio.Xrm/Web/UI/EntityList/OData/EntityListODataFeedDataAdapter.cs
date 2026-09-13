/*
  Copyright (c) Microsoft Corporation. All rights reserved.
  Licensed under the MIT License. See License.txt in the project root for license information.
*/

namespace Adxstudio.Xrm.Web.UI.EntityList.OData
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Net.Http;
	using System.ServiceModel.Security;
	using System.Web;
	using Microsoft.AspNet.OData;
	using Microsoft.AspNet.OData.Extensions;
	using Adxstudio.Xrm.Globalization;
	using Adxstudio.Xrm.Resources;
	using Adxstudio.Xrm.Security;
	using Adxstudio.Xrm.Services.Query;
	using Adxstudio.Xrm.Web.Http.OData;
	using Adxstudio.Xrm.Web.Http.OData.FetchXml;
	using Microsoft.OData.Edm;
	using Microsoft.OData;
	using Microsoft.Xrm.Client.Diagnostics;
	using Microsoft.Xrm.Sdk;
	using Microsoft.Xrm.Sdk.Metadata;
	using Microsoft.Xrm.Sdk.Query;

	/// <summary>
	/// Data adapter regarding OData feeds enabled for Entity List.
	/// </summary>
	public class EntityListODataFeedDataAdapter : IEntityListODataFeedDataAdapter
	{
		private enum StateCode
		{
			Active = 0,
			Inactive = 1
		}

		private const string DefaultNamespaceName = "Xrm";
		private const string DefaultContainerName = "ODataFeed";
		private const int DefaultPageSize = 30;
		
		/// <summary>
		/// EntityListODataFeedDataAdapter constructor
		/// </summary>
		/// <param name="dependencies"><see cref="PortalConfigurationDataAdapterDependencies"/></param>
		/// <param name="namespaceName">Name of the namespace of the feed's model</param>
		/// <param name="containerName">Name of container of the entities in the feed</param>
		/// <param name="languageCode">Language Code used to retrieve localized labels</param>
		/// <exception cref="ArgumentNullException"></exception>
		public EntityListODataFeedDataAdapter(PortalConfigurationDataAdapterDependencies dependencies, string namespaceName = DefaultNamespaceName, string containerName = DefaultContainerName, int? languageCode = 0)
		{
			if (dependencies == null) throw new ArgumentNullException("dependencies");

			Dependencies = dependencies;
			NamespaceName = string.IsNullOrWhiteSpace(namespaceName) ? DefaultNamespaceName : namespaceName;
			ContainerName = string.IsNullOrWhiteSpace(containerName) ? DefaultContainerName : containerName;

			if ((languageCode == null || languageCode == 0) && HttpContext.Current != null)
			{
				var languageContext = HttpContext.Current.GetContextLanguageInfo();
				this.LanguageCode = languageContext.IsCrmMultiLanguageEnabled
										? languageContext.ContextLanguage.CrmLcid
										: (languageCode ?? 0);
			}
			else
			{
				LanguageCode = languageCode ?? 0;
			}
		}

		protected string ContainerName { get; private set; }

		protected DataAdapterDependencies Dependencies { get; private set; }

		/// <summary>
		/// Language code used to return labels for the specified local.
		/// </summary>
		public int LanguageCode { get; private set; }

		/// <summary>
		/// Name used to specify the Namespace for the model.
		/// </summary>
		public string NamespaceName { get; private set; }

		protected Entity GetSavedQuery(Guid id)
		{
			var serviceContext = Dependencies.GetServiceContext();
			var savedQuery = serviceContext.CreateQuery("savedquery").FirstOrDefault(e => e.GetAttributeValue<Guid>("savedqueryid") == id);
			
			if (savedQuery == null)
			{
				throw new ApplicationException(string.Format("Could not find an View (savedquery) record where savedqueryid equals {0}.", id));
			}

			return savedQuery;
		}

		/// <summary>
		/// Get the view
		/// </summary>
		/// <param name="savedQueryId">Unique identifier of the savedquery entity</param>
		/// <returns><see cref="SavedQueryView"/></returns>
		public virtual SavedQueryView GetView(Guid savedQueryId)
		{
			return GetView(GetSavedQuery(savedQueryId));
		}

		/// <summary>
		/// Get the view
		/// </summary>
		/// <param name="savedQuery">savedquery entity</param>
		/// <returns><see cref="SavedQueryView"/></returns>
		public virtual SavedQueryView GetView(Entity savedQuery)
		{
			var serviceContext = Dependencies.GetServiceContext();
			return new SavedQueryView(serviceContext, savedQuery, LanguageCode);
		}

		/// <summary>
		/// Get the entity list records for a given website that will be used to get the view definitions
		/// </summary>
		/// <param name="website">Website <see cref="EntityReference"/></param>
		public virtual List<Entity> GetEntityLists(EntityReference website)
		{
			var serviceContext = Dependencies.GetServiceContext();
			List<Entity> entitylists;

			if (website != null)
			{
				entitylists = (from el in serviceContext.CreateQuery("adx_entitylist")
							   join wp in serviceContext.CreateQuery("adx_webpage") on el.GetAttributeValue<Guid>("adx_entitylistid") equals
								   wp.GetAttributeValue<EntityReference>("adx_entitylist").Id
							   where
								   el.GetAttributeValue<OptionSetValue>("statecode") != null && el.GetAttributeValue<OptionSetValue>("statecode").Value == (int)StateCode.Active &&
								   el.GetAttributeValue<bool?>("adx_odata_enabled").GetValueOrDefault(false)
							   where
								   wp.GetAttributeValue<EntityReference>("adx_entitylist") != null &&
								   wp.GetAttributeValue<EntityReference>("adx_websiteid") == website
							   orderby el.GetAttributeValue<string>("adx_odata_entitysetname")
							   select el).ToList();
			}
			else
			{
				entitylists =
					serviceContext.CreateQuery("adx_entitylist")
								.Where(
									el =>
									el.GetAttributeValue<OptionSetValue>("statecode") != null && el.GetAttributeValue<OptionSetValue>("statecode").Value == (int)StateCode.Active &&
									el.GetAttributeValue<bool?>("adx_odata_enabled").GetValueOrDefault(false))
								.OrderBy(el => el.GetAttributeValue<string>("adx_odata_entitysetname"))
								.ToList();
			}
			return entitylists;
		}

		/// <summary>
		/// Get's the entity list associated with the entity set name in the model
		/// </summary>
		/// <param name="model"><see cref="IEdmModel"/></param>
		/// <param name="entitySetName">Name of the entity set</param>
		/// <returns><see cref="Entity"/></returns>
		public virtual Entity GetEntityList(IEdmModel model, string entitySetName)
		{
			var edmEntitySchemaType = model.EntityContainer.FindEntitySet(entitySetName).EntityType();
			var edmEntityType = edmEntitySchemaType as IEdmEntityType;
			var entityListIdProperty = edmEntityType.FindProperty("list_id") as IEdmStructuralProperty;
			var entityListIdString = entityListIdProperty.DefaultValueString;
			Guid id;
			Guid.TryParse(entityListIdString, out id);
			var serviceContext = Dependencies.GetServiceContext();
			var entityList = serviceContext.CreateQuery("adx_entitylist").FirstOrDefault(el => el.GetAttributeValue<Guid>("adx_entitylistid") == id);
			return entityList;
		}

		/// <summary>
		/// Get's the page size defined on the entity list associated with the entity set name in the model.
		/// </summary>
		/// <param name="model"><see cref="IEdmModel"/></param>
		/// <param name="entitySetName">Name of the entity set</param>
		/// <returns>page size</returns>
		public virtual int GetPageSize(IEdmModel model, string entitySetName)
		{
			var entityList = GetEntityList(model, entitySetName);
			return entityList == null ? DefaultPageSize : entityList.GetAttributeValue<int?>("adx_pagesize").GetValueOrDefault(DefaultPageSize);
		}

		/// <summary>
		/// Get EdmModel
		/// </summary>
		/// <returns><see cref="IEdmModel"/></returns>
		public virtual IEdmModel GetEdmModel()
		{
			var serviceContext = Dependencies.GetServiceContext();
			var website = Dependencies.GetWebsite();
			var model = new EdmModel();
			var container = new EdmEntityContainer(NamespaceName, ContainerName);
			model.AddElement(container);

			var entitylists = GetEntityLists(website);

			if (!entitylists.Any())
			{
				return model;
			}

			var entityReferenceType = new EdmComplexType(NamespaceName, "EntityReference");
			entityReferenceType.AddStructuralProperty("Id", EdmPrimitiveTypeKind.Guid);
			entityReferenceType.AddStructuralProperty("Name", EdmPrimitiveTypeKind.String);
			
			var picklistType = new EdmComplexType(NamespaceName, "OptionSet");
			picklistType.AddStructuralProperty("Name", EdmPrimitiveTypeKind.String);
			picklistType.AddStructuralProperty("Value", EdmPrimitiveTypeKind.Int32);

			model.AddElement(entityReferenceType);
			model.AddElement(picklistType);

			var entitySetNames = new List<string>();

			foreach (var entitylist in entitylists)
			{
				var entityListId = entitylist.GetAttributeValue<Guid>("adx_entitylistid");
				var entityListEntityName = entitylist.GetAttributeValue<string>("adx_entityname");
				var entityListEntityTypeName = entitylist.GetAttributeValue<string>("adx_odata_entitytypename");
				var entityListEntitySetName = entitylist.GetAttributeValue<string>("adx_odata_entitysetname");
				var entityTypeName = string.IsNullOrWhiteSpace(entityListEntityTypeName) ? entityListEntityName : entityListEntityTypeName;
				var entitySetName = string.IsNullOrWhiteSpace(entityListEntitySetName) ? entityListEntityName : entityListEntitySetName;
				var entityPermissionsEnabled = entitylist.GetAttributeValue<bool>("adx_entitypermissionsenabled");
				if (entitySetNames.Contains(entitySetName))
				{
                    ADXTrace.Instance.TraceError(TraceCategory.Application, string.Format(string.Format("An Entity Set has already been defined with the name '{0}'. Entity Set could not added to the model. You must not have multiple Entity List records with OData enabled and the same Entity Name specified, otherwise specifiy a unique Entity Set Name and Entity Type Name in the Entity List's OData Settings in CRM.", entitySetName)));
                    continue;
				}
				entitySetNames.Add(entitySetName);
				var entityType = new EdmEntityType(NamespaceName, entityTypeName);
				var viewIdString = entitylist.GetAttributeValue<string>("adx_odata_view");

				if (string.IsNullOrWhiteSpace(viewIdString))
				{
					continue;
				}

				Guid viewId;

				Guid.TryParse(viewIdString, out viewId);

				var savedQuery = GetSavedQuery(viewId);

				var view = new SavedQueryView(serviceContext, savedQuery, LanguageCode);

				var columns = view.Columns;

				var key = entityType.AddStructuralProperty(view.PrimaryKeyLogicalName, EdmPrimitiveTypeKind.Guid, false);
				entityType.AddKeys(key);

				foreach (var column in columns)
				{
					if (column.LogicalName == view.PrimaryKeyLogicalName) continue;
					var attributeMetadata = column.Metadata;
					var propertyName = column.LogicalName;
					if (propertyName.Contains('.'))
					{
						propertyName = string.Format("{0}_{1}", column.Metadata.EntityLogicalName, column.Metadata.LogicalName);
					}
					var edmPrimitiveTypeKind = MetadataHelpers.GetEdmPrimitiveTypeKindFromAttributeMetadata(attributeMetadata);
					if (edmPrimitiveTypeKind != null)
					{
						entityType.AddStructuralProperty(propertyName, edmPrimitiveTypeKind.GetValueOrDefault(EdmPrimitiveTypeKind.None));
					}
					else
					{
						switch (attributeMetadata.AttributeType)
						{
							case AttributeTypeCode.Customer:
							case AttributeTypeCode.Lookup:
							case AttributeTypeCode.Owner:
								entityType.AddStructuralProperty(propertyName, new EdmComplexTypeReference(entityReferenceType, true));
								break;
							case AttributeTypeCode.Picklist:
							case AttributeTypeCode.State:
							case AttributeTypeCode.Status:
								entityType.AddStructuralProperty(propertyName, new EdmComplexTypeReference(picklistType, true));
								break;
						}
					}
				}

				entityType.AddProperty(new EdmStructuralProperty(entityType, "list_id", EdmCoreModel.Instance.GetPrimitive(EdmPrimitiveTypeKind.String, true), entityListId.ToString()));
				entityType.AddProperty(new EdmStructuralProperty(entityType, "view_id", EdmCoreModel.Instance.GetPrimitive(EdmPrimitiveTypeKind.String, true), viewId.ToString()));
				entityType.AddProperty(new EdmStructuralProperty(entityType, "entity_permissions_enabled", EdmCoreModel.Instance.GetPrimitive(EdmPrimitiveTypeKind.String, true), entityPermissionsEnabled.ToString()));

				model.AddElement(entityType);
				container.AddEntitySet(entitySetName, entityType);
			}

			return model;
		}

		/// <summary>
		/// Gets the collection of records for the requested entity set and specified OData query options.
		/// </summary>
		/// <param name="model"><see cref="IEdmModel"/></param>
		/// <param name="entitySetName">Name of the entity set</param>
		/// <param name="queryOptions"><see cref="Microsoft.AspNet.OData.Query.ODataQueryOptions"/></param>
		/// <param name="querySettings"><see cref="Microsoft.AspNet.OData.Query.ODataQuerySettings"/></param>
		/// <param name="request"><see cref="HttpRequestMessage"/></param>
		/// <returns><see cref="EdmEntityObjectCollection"/></returns>
		public virtual EdmEntityObjectCollection SelectMultiple(IEdmModel model, string entitySetName, Microsoft.AspNet.OData.Query.ODataQueryOptions queryOptions, Microsoft.AspNet.OData.Query.ODataQuerySettings querySettings, HttpRequestMessage request)
		{
			var edmEntitySchemaType = model.EntityContainer.FindEntitySet(entitySetName).EntityType();
			var edmEntityType = edmEntitySchemaType as IEdmEntityType;
			var entityListIdProperty = edmEntityType.FindProperty("list_id") as IEdmStructuralProperty;
			var entityListIdString = entityListIdProperty.DefaultValueString;
			var viewIdProperty = edmEntityType.FindProperty("view_id") as IEdmStructuralProperty;
			var viewIdString = viewIdProperty.DefaultValueString;
			var entityPermissionEnabledProperty =
				edmEntityType.FindProperty("entity_permissions_enabled") as IEdmStructuralProperty;
			bool entityPermissionsEnabled;
			bool.TryParse(entityPermissionEnabledProperty.DefaultValueString, out entityPermissionsEnabled);

			Guid viewId;
			Guid.TryParse(viewIdString, out viewId);
			var view = GetView(viewId);
			var viewColumns = view.Columns.ToList();
			var fetch = Fetch.Parse(view.FetchXml);
			queryOptions.ApplyTo(querySettings, fetch);
			var serviceContext = this.Dependencies.GetServiceContext();

			// If Entity Permissions on the view was enabled then restrict add entity Permissions to Fetch
			if (entityPermissionsEnabled)
			{
				var crmEntityPermissionProvider = new CrmEntityPermissionProvider();
				var perm = crmEntityPermissionProvider.TryApplyRecordLevelFiltersToFetch(
					serviceContext,
					CrmEntityPermissionRight.Read,
					fetch);

				// Ensure the user has permissions to request read access to the entity.
				if (!perm.PermissionGranted && !perm.GlobalPermissionGranted)
				{
					ADXTrace.Instance.TraceWarning(
						TraceCategory.Exception,
						string.Format(
							"Access to oData, with the entity set name of '{0}', has been denied for the user with the following webroles: '{1}' due to entity permissions. Grant the webroles read access to the entityset if this was not an error.",
							entitySetName,
							string.Join("', '", crmEntityPermissionProvider.CurrentUserRoleNames)));
					throw new SecurityAccessDeniedException();
				}
			}

			var dataSchemaType = model.EntityContainer.FindEntitySet(entitySetName).EntityType();
			var dataEntityType = dataSchemaType as IEdmEntityType;
			var dataEntityTypeReference = new EdmEntityTypeReference(dataEntityType, true);
			var collection = new EdmEntityObjectCollection(new EdmCollectionTypeReference(new EdmCollectionType(dataEntityTypeReference)));
			var entityReferenceSchemaType = model.FindDeclaredType(string.Format("{0}.{1}", NamespaceName, "EntityReference"));
			var entityReferenceComplexType = entityReferenceSchemaType as IEdmComplexType;
			var entityReferenceComplexTypeReference = new EdmComplexTypeReference(entityReferenceComplexType, true);
			var optionSetSchemaType = model.FindDeclaredType(string.Format("{0}.{1}", NamespaceName, "OptionSet"));
			var optionSetComplexType = optionSetSchemaType as IEdmComplexType;
			var optionSetComplexTypeReference = new EdmComplexTypeReference(optionSetComplexType, true);
			var entityCollection = fetch.Execute(serviceContext as IOrganizationService);
			
			if (entityCollection == null)
			{
				return collection;
			}
			
			var records = entityCollection.Entities;

			foreach (var record in records)
			{
				var entityObject = BuildEdmEntityObject(record, view, viewColumns, dataEntityTypeReference, entityReferenceComplexTypeReference, optionSetComplexTypeReference, entityListIdString, viewIdString);
				collection.Add(entityObject);
			}

			if (entityCollection.MoreRecords && querySettings.PageSize.HasValue && querySettings.PageSize > 0)
			{
				var nextPageLink = ODataQueryOptionExtensions.GetNextPageLink(request, querySettings.PageSize.Value);
				request.ODataProperties().NextLink = nextPageLink;
			}

			if (queryOptions.Count != null && queryOptions.Count.Value && entityCollection.TotalRecordCount >= 0)
			{
				request.ODataProperties().TotalCount = entityCollection.TotalRecordCount;
			}

			return collection;
		}

		/// <summary>
		/// Get a single record for the specified entity set where the object's key matches the id provided.
		/// </summary>
		/// <param name="model"><see cref="IEdmModel"/></param>
		/// <param name="entitySetName">Name of the entity set</param>
		/// <param name="id">Unique Identifier key of the record</param>
		/// <returns>A single <see cref="IEdmEntityObject"/></returns>
		public virtual IEdmEntityObject Select(IEdmModel model, string entitySetName, Guid id)
		{
			var edmEntitySchemaType = model.EntityContainer.FindEntitySet(entitySetName).EntityType();
			var edmEntityType = edmEntitySchemaType as IEdmEntityType;
			var entityListIdProperty = edmEntityType.FindProperty("list_id") as IEdmStructuralProperty;
			var entityListIdString = entityListIdProperty.DefaultValueString;
			var viewIdProperty = edmEntityType.FindProperty("view_id") as IEdmStructuralProperty;
			var viewIdString = viewIdProperty.DefaultValueString;
			Guid viewId;
			Guid.TryParse(viewIdString, out viewId);
			var view = GetView(viewId);
			var viewColumns = view.Columns.ToList();
			var fetch = Fetch.Parse(view.FetchXml);

			fetch.Orders.Clear();
			fetch.Entity.Orders.Clear();
			fetch.Entity.Filters.Clear();

			var filter = new Filter { Type = LogicalOperator.And };
			var conditions = new List<Condition>();
			var condition = new Condition
								{
									Attribute = view.PrimaryKeyLogicalName,
									Operator = ConditionOperator.Equal,
									Value = id
								};
			conditions.Add(condition);
			filter.Conditions = conditions;
			fetch.Entity.Filters.Add(filter);
			
			var serviceContext = Dependencies.GetServiceContext();
			var entityCollection = fetch.Execute(serviceContext as IOrganizationService);

			if (entityCollection == null)
			{
				return null;
			}

			var records = entityCollection.Entities;

			if (records == null)
			{
				return null;
			}

			var record = records.FirstOrDefault();

			if (record == null)
			{
				return null;
			}

			var dataSchemaType = model.EntityContainer.FindEntitySet(entitySetName).EntityType();
			var dataEntityType = dataSchemaType as IEdmEntityType;
			var dataEntityTypeReference = new EdmEntityTypeReference(dataEntityType, true);
			
			var entityReferenceSchemaType = model.FindDeclaredType(string.Format("{0}.{1}", NamespaceName, "EntityReference"));
			var entityReferenceComplexType = entityReferenceSchemaType as IEdmComplexType;
			var entityReferenceComplexTypeReference = new EdmComplexTypeReference(entityReferenceComplexType, true);
			var optionSetSchemaType = model.FindDeclaredType(string.Format("{0}.{1}", NamespaceName, "OptionSet"));
			var optionSetComplexType = optionSetSchemaType as IEdmComplexType;
			var optionSetComplexTypeReference = new EdmComplexTypeReference(optionSetComplexType, true);

			var entityObject = BuildEdmEntityObject(record, view, viewColumns, dataEntityTypeReference, entityReferenceComplexTypeReference, optionSetComplexTypeReference, entityListIdString, viewIdString);

			return entityObject;
		}

		protected virtual IEdmEntityObject BuildEdmEntityObject(Entity record, SavedQueryView view, IEnumerable<SavedQueryView.ViewColumn> viewColumns, EdmEntityTypeReference dataEntityTypeReference, EdmComplexTypeReference entityReferenceComplexTypeReference, EdmComplexTypeReference optionSetComplexTypeReference, string entityListIdString, string viewIdString)
		{
			if (record == null)
			{
				return null;
			}

			var entityObject = new EdmEntityObject(dataEntityTypeReference);
			
			entityObject.TrySetPropertyValue(view.PrimaryKeyLogicalName, record.Id);

			foreach (var column in viewColumns)
			{
				var value = record.Attributes.Contains(column.LogicalName) ? record.Attributes[column.LogicalName] : null;

				if (value is AliasedValue)
				{
					var aliasedValue = value as AliasedValue;
					value = aliasedValue.Value;
				}

				if (column.Metadata == null)
				{
					continue;
				}

				var propertyName = column.LogicalName;

				if (propertyName.Contains('.'))
				{
					propertyName = string.Format("{0}_{1}", column.Metadata.EntityLogicalName, column.Metadata.LogicalName);
				}

				switch (column.Metadata.AttributeType)
				{
					case AttributeTypeCode.DateTime:
						// CRM stores UTC; an unspecified Kind must not use the web server's local offset.
						var dateTime = value as DateTime?;
						entityObject.TrySetPropertyValue(propertyName, dateTime.HasValue
							? (object)new DateTimeOffset(dateTime.Value.Kind == DateTimeKind.Unspecified
								? DateTime.SpecifyKind(dateTime.Value, DateTimeKind.Utc) : dateTime.Value)
							: null);
						break;
					case AttributeTypeCode.Money:
						var money = value as Money;
						decimal moneyValue = 0;
						if (money != null)
						{
							moneyValue = money.Value;
						}
						entityObject.TrySetPropertyValue(propertyName, moneyValue);
						break;
					case AttributeTypeCode.Customer:
					case AttributeTypeCode.Lookup:
					case AttributeTypeCode.Owner:
						var entityReference = value as EntityReference;
						if (entityReference == null)
						{
							continue;
						}
						var entityReferenceObject = new EdmComplexObject(entityReferenceComplexTypeReference);
						entityReferenceObject.TrySetPropertyValue("Name", entityReference.Name);
						entityReferenceObject.TrySetPropertyValue("Id", entityReference.Id);
						entityObject.TrySetPropertyValue(propertyName, entityReferenceObject);
						break;
					case AttributeTypeCode.State:
						var stateOptionSet = value as OptionSetValue;
						if (stateOptionSet == null)
						{
							continue;
						}
						var stateAttributeMetadata = column.Metadata as StateAttributeMetadata;
						if (stateAttributeMetadata == null)
						{
							continue;
						}
						var stateOption = stateAttributeMetadata.OptionSet.Options.FirstOrDefault(o => o != null && o.Value != null && o.Value.Value == stateOptionSet.Value);
						if (stateOption == null)
						{
							continue;
						}
						var stateLabel = stateOption.Label.LocalizedLabels.FirstOrDefault(l => l.LanguageCode == LanguageCode) ?? stateOption.Label.GetLocalizedLabel();
						var stateOptionName = stateLabel == null ? stateOption.Label.GetLocalizedLabelString() : stateLabel.Label;
						var stateOptionSetObject = new EdmComplexObject(optionSetComplexTypeReference);
						stateOptionSetObject.TrySetPropertyValue("Name", stateOptionName);
						stateOptionSetObject.TrySetPropertyValue("Value", stateOptionSet.Value);
						entityObject.TrySetPropertyValue(propertyName, stateOptionSetObject);
						break;
					case AttributeTypeCode.Picklist:
						var optionSet = value as OptionSetValue;
						if (optionSet == null)
						{
							continue;
						}
						var picklistAttributeMetadata = column.Metadata as PicklistAttributeMetadata;
						if (picklistAttributeMetadata == null)
						{
							continue;
						}
						var option = picklistAttributeMetadata.OptionSet.Options.FirstOrDefault(o => o != null && o.Value != null && o.Value.Value == optionSet.Value);
						if (option == null)
						{
							continue;
						}
						var label = option.Label.LocalizedLabels.FirstOrDefault(l => l.LanguageCode == LanguageCode) ?? option.Label.GetLocalizedLabel();
						var name = label == null ? option.Label.GetLocalizedLabelString() : label.Label;
						var optionSetObject = new EdmComplexObject(optionSetComplexTypeReference);
						optionSetObject.TrySetPropertyValue("Name", name);
						optionSetObject.TrySetPropertyValue("Value", optionSet.Value);
						entityObject.TrySetPropertyValue(propertyName, optionSetObject);
						break;
					case AttributeTypeCode.Status:
						var statusOptionSet = value as OptionSetValue;
						if (statusOptionSet == null)
						{
							continue;
						}
						var statusAttributeMetadata = column.Metadata as StatusAttributeMetadata;
						if (statusAttributeMetadata == null)
						{
							continue;
						}
						var statusOption = statusAttributeMetadata.OptionSet.Options.FirstOrDefault(o => o != null && o.Value != null && o.Value.Value == statusOptionSet.Value);
						if (statusOption == null)
						{
							continue;
						}
						var statusLabel = statusOption.Label.LocalizedLabels.FirstOrDefault(l => l.LanguageCode == LanguageCode) ?? statusOption.Label.GetLocalizedLabel();
						var statusOptionName = statusLabel == null ? statusOption.Label.GetLocalizedLabelString() : statusLabel.Label;
						var statusOptionSetObject = new EdmComplexObject(optionSetComplexTypeReference);
						statusOptionSetObject.TrySetPropertyValue("Name", statusOptionName);
						statusOptionSetObject.TrySetPropertyValue("Value", statusOptionSet.Value);
						entityObject.TrySetPropertyValue(propertyName, statusOptionSetObject);
						break;
					default:
						entityObject.TrySetPropertyValue(propertyName, value);
						break;
				}
				entityObject.TrySetPropertyValue("list_id", entityListIdString);
				entityObject.TrySetPropertyValue("view_id", viewIdString);
			}
			return entityObject;
		}
	}
}
