/*
  Copyright (c) Microsoft Corporation. All rights reserved.
  Licensed under the MIT License. See License.txt in the project root for license information.
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Encoder = System.Web.Security.AntiXss.AntiXssEncoder;
using Microsoft.Xrm.Client;
using Microsoft.Xrm.Client.Caching;
using Microsoft.Xrm.Client.Metadata;
using Microsoft.Xrm.Client.Runtime.Serialization;
using Microsoft.Xrm.Portal.Configuration;
using Microsoft.Xrm.Portal.Runtime;

namespace Microsoft.Xrm.Portal.Web.Data.Services
{
	internal static class TypeExtensions
	{
		public static string GetCrmEntitySetDataServiceUri(this Type crmDataContextType, string crmEntityName)
		{
			return GetCrmEntitySetDataServiceUri(crmDataContextType, crmEntityName, GetCmsServiceBaseUri());
		}

		public static string GetCrmEntitySetDataServiceUri(this Type crmDataContextType, string crmEntityName, string serviceBaseUri)
		{
			var portal = PortalCrmConfigurationManager.CreatePortalContext();
			return GetCrmEntitySetDataServiceUri(crmDataContextType, crmEntityName, serviceBaseUri, "adx_websiteid", portal.Website.Id);
		}

		public static string GetCrmEntitySetDataServiceUri(
			this Type crmDataContextType,
			string crmEntityName,
			string serviceBaseUri,
			string foreignKeyAttributeName,
			Guid foreignKeyValue)
		{
			if (string.IsNullOrEmpty(serviceBaseUri))
			{
				return null;
			}

			var entitySetInfo = GetEntitySetInfo(crmDataContextType, crmEntityName);

			if (entitySetInfo == null)
			{
				return null;
			}

			var filter = string.Empty;
			AttributeInfo propertyInfo;

			if (entitySetInfo.Entity.AttributesByLogicalName.TryGetValue(foreignKeyAttributeName, out propertyInfo))
			{
				filter = "?$filter={0}/Id eq guid'{1}'".FormatWith(UrlEncode(propertyInfo.Property.Name), UrlEncode(foreignKeyValue.ToString()));
			}

			return "{0}/{1}{2}".FormatWith(serviceBaseUri.TrimEnd('/'), UrlEncode(entitySetInfo.Property.Name), filter);
		}

		public static string GetCrmEntitySetSchemaMap(this Type crmDataContextType, string crmEntityName)
		{
			return ObjectCacheManager.Get("microsoft.xrm.portal:entity-schema-map:json:{0}:{1}".FormatWith(crmDataContextType.FullName, crmEntityName), cache =>
			{
				EntitySetInfo entitySetInfo;

				if (!OrganizationServiceContextInfo.TryGet(crmDataContextType, crmEntityName, out entitySetInfo)
					|| entitySetInfo.Entity == null
						|| entitySetInfo.Entity.EntityLogicalName == null)
				{
					throw new InvalidOperationException(@"Unable to retrieve entity set information for entity name ""{0}"".".FormatWith(crmEntityName));
				}

				var properties = entitySetInfo.Entity.AttributesByLogicalName.Values;

				var schemaMap = properties.ToDictionary(info => info.CrmPropertyAttribute.LogicalName, info => info.Property.Name);

				var json = schemaMap.SerializeByJson(new Type[] { });

				return json;
			});
		}

		public static string GetCrmEntityDeleteDataServiceUriTemplate(this Type crmDataContextType, string crmEntityName)
		{
			return GetCrmEntityDeleteDataServiceUriTemplate(crmDataContextType, crmEntityName, GetCmsServiceBaseUri());
		}

		public static string GetCrmEntityDeleteDataServiceUriTemplate(this Type crmDataContextType, string crmEntityName, string serviceBaseUri)
		{
			if (string.IsNullOrEmpty(serviceBaseUri))
			{
				return null;
			}

			var entitySetInfo = GetEntitySetInfo(crmDataContextType, crmEntityName);

			if (entitySetInfo == null)
			{
				return null;
			}

			var primaryKeyPropertyName = GetPrimaryKeyPropertyName(crmDataContextType, crmEntityName);

			if (string.IsNullOrEmpty(primaryKeyPropertyName))
			{
				return null;
			}

			var entityIDTemplateVariable = "{" + primaryKeyPropertyName + "}";

			return "{0}/DeleteEntity?entitySet='{1}'&entityID=guid'{2}'".FormatWith(serviceBaseUri.TrimEnd('/'), UrlEncode(entitySetInfo.Property.Name), entityIDTemplateVariable);
		}

		public static string GetCrmEntityUrlDataServiceUriTemplate(this Type crmDataContextType, string crmEntityName)
		{
			return GetCrmEntityUrlDataServiceUriTemplate(crmDataContextType, crmEntityName, GetCmsServiceBaseUri());
		}

		public static string GetCrmEntityUrlDataServiceUriTemplate(this Type crmDataContextType, string crmEntityName, string serviceBaseUri)
		{
			if (string.IsNullOrEmpty(serviceBaseUri))
			{
				return null;
			}

			var entitySetInfo = GetEntitySetInfo(crmDataContextType, crmEntityName);

			if (entitySetInfo == null)
			{
				return null;
			}

			var primaryKeyPropertyName = GetPrimaryKeyPropertyName(crmDataContextType, crmEntityName);

			if (string.IsNullOrEmpty(primaryKeyPropertyName))
			{
				return null;
			}

			var entityIDTemplateVariable = "{" + primaryKeyPropertyName + "}";

			return "{0}/GetEntityUrl?entitySet='{1}'&entityID=guid'{2}'".FormatWith(serviceBaseUri.TrimEnd('/'), UrlEncode(entitySetInfo.Property.Name), entityIDTemplateVariable);
		}

		public static string GetCrmEntityFileAttachmentDataServiceUriTemplate(this Type crmDataContextType, string crmEntityName)
		{
			return GetCrmEntityFileAttachmentDataServiceUriTemplate(crmDataContextType, crmEntityName, GetCmsServiceBaseUri());
		}

		public static string GetCrmEntityFileAttachmentDataServiceUriTemplate(this Type crmDataContextType, string crmEntityName, string serviceBaseUri)
		{
			if (string.IsNullOrEmpty(serviceBaseUri))
			{
				return null;
			}

			var entitySetInfo = GetEntitySetInfo(crmDataContextType, crmEntityName);

			if (entitySetInfo == null)
			{
				return null;
			}

			var primaryKeyPropertyName = GetPrimaryKeyPropertyName(crmDataContextType, crmEntityName);

			if (string.IsNullOrEmpty(primaryKeyPropertyName))
			{
				return null;
			}

			var entityIDTemplateVariable = "{" + primaryKeyPropertyName + "}";

			return "{0}/AttachFilesToEntity?entitySet='{1}'&entityID=guid'{2}'".FormatWith(serviceBaseUri.TrimEnd('/'), UrlEncode(entitySetInfo.Property.Name), entityIDTemplateVariable);
		}

		public static IEnumerable<PropertyInfo> GetEntitySetProperties(this Type crmDataContextType)
		{
			crmDataContextType.ThrowOnNull("crmDataContextType");

			var dataContextPublicProperties = crmDataContextType.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.GetProperty);

			return dataContextPublicProperties.Where(property =>
			{
				var propertyType = property.PropertyType;

				if (!propertyType.IsGenericType)
				{
					return false;
				}

				var genericDefinition = propertyType.GetGenericTypeDefinition();

				return genericDefinition == typeof(IQueryable<>);
			});
		}

		private static EntitySetInfo GetEntitySetInfo(Type crmDataContextType, string crmEntityName)
		{
			EntitySetInfo entitySetInfo;

			OrganizationServiceContextInfo.TryGet(crmDataContextType, crmEntityName, out entitySetInfo);

			return entitySetInfo;
		}

		private static string GetPrimaryKeyPropertyName(Type crmDataContextType, string crmEntityName)
		{
			EntitySetInfo entitySetInfo;

			if (!OrganizationServiceContextInfo.TryGet(crmDataContextType, crmEntityName, out entitySetInfo)
				|| entitySetInfo.Entity.PrimaryKeyProperty == null) return null;

			return entitySetInfo.Entity.PrimaryKeyProperty.Property.Name;
		}

		private static string UrlEncode(string s)
		{
			return Encoder.UrlEncode(s);
		}

		private static string GetCmsServiceBaseUri(string portalName = null)
		{
			return PortalCrmConfigurationManager.GetCmsServiceBaseUri(portalName);
		}
	}
}
