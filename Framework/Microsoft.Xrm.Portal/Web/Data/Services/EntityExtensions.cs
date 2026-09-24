/*
  Copyright (c) Microsoft Corporation. All rights reserved.
  Licensed under the MIT License. See License.txt in the project root for license information.
*/

using System;
using Encoder = System.Web.Security.AntiXss.AntiXssEncoder;
using Microsoft.Xrm.Client;
using Microsoft.Xrm.Client.Metadata;
using Microsoft.Xrm.Portal.Configuration;
using Microsoft.Xrm.Sdk;

namespace Microsoft.Xrm.Portal.Web.Data.Services
{
	/// <summary>
	/// Extension methods for <see cref="Entity"/> for building <see cref="CmsDataService{TDataContext}"/> service URIs.
	/// </summary>
	internal static class EntityExtensions
	{
		public static string GetDataServiceUri(this Entity entity, string serviceBaseUri)
		{
			if (string.IsNullOrEmpty(serviceBaseUri))
			{
				return null;
			}

			var uriInfo = new EntityDataServiceUriInfo(entity);

			if (!uriInfo.Valid)
			{
				return null;
			}

			return "{0}/{1}(guid'{2}')".FormatWith(serviceBaseUri.TrimEnd('/'), UrlEncode(uriInfo.EntitySetName), UrlEncode(uriInfo.PrimaryKey.ToString()));
		}

		public static string GetDataServiceUri(this Entity entity)
		{
			return GetDataServiceUri(entity, GetCmsServiceBaseUri());
		}

		public static string GetDataServicePropertyUri(this Entity entity, string propertyName, string serviceBaseUri)
		{
			if (string.IsNullOrEmpty(propertyName))
			{
				return null;
			}

			var entityUri = GetDataServiceUri(entity, serviceBaseUri);

			if (entityUri == null)
			{
				return null;
			}

			return "{0}/{1}".FormatWith(entityUri, UrlEncode(propertyName));
		}

		public static string GetDataServicePropertyUri(this Entity entity, string propertyName)
		{
			return GetDataServicePropertyUri(entity, propertyName, GetCmsServiceBaseUri());
		}

		public static string GetDataServiceCrmAssociationSetUri(this Entity entity, string portalName, Relationship relationship)
		{
			return GetDataServiceCrmAssociationSetUri(entity, portalName, relationship, GetCmsServiceBaseUri());
		}

		public static string GetDataServiceCrmAssociationSetUri(this Entity entity, string portalName, Relationship relationship, string serviceBaseUri)
		{
			if (string.IsNullOrEmpty(relationship.SchemaName))
			{
				return null;
			}

			try
			{
				var portal = PortalCrmConfigurationManager.CreatePortalContext(portalName);
				var context = portal.ServiceContext;

				EntitySetInfo entitySetInfo;
				RelationshipInfo associationInfo;

				if (!OrganizationServiceContextInfo.TryGet(context, entity, out entitySetInfo)
					|| !entitySetInfo.Entity.RelationshipsBySchemaName.TryGetValue(relationship, out associationInfo))
				{
					return null;
				}

				return GetDataServicePropertyUri(entity, associationInfo.Property.Name, serviceBaseUri);
			}
			catch (InvalidOperationException)
			{
				return null;
			}
		}

		public static string GetEntityDeleteDataServiceUri(this Entity entity)
		{
			return GetEntityDeleteDataServiceUri(entity, GetCmsServiceBaseUri());
		}

		public static string GetEntityDeleteDataServiceUri(this Entity entity, string serviceBaseUri)
		{
			if (string.IsNullOrEmpty(serviceBaseUri))
			{
				return null;
			}

			var uriInfo = new EntityDataServiceUriInfo(entity);

			if (!uriInfo.Valid)
			{
				return null;
			}

			return "{0}/DeleteEntity?entitySet='{1}'&entityID=guid'{2}'".FormatWith(serviceBaseUri.TrimEnd('/'), UrlEncode(uriInfo.EntitySetName), UrlEncode(uriInfo.PrimaryKey.ToString()));
		}

		public static string GetEntityUrlDataServiceUri(this Entity entity)
		{
			return GetEntityUrlDataServiceUri(entity, GetCmsServiceBaseUri());
		}

		public static string GetEntityUrlDataServiceUri(this Entity entity, string serviceBaseUri)
		{
			if (string.IsNullOrEmpty(serviceBaseUri))
			{
				return null;
			}

			var uriInfo = new EntityDataServiceUriInfo(entity);

			if (!uriInfo.Valid)
			{
				return null;
			}

			return "{0}/GetEntityUrl?entitySet='{1}'&entityID=guid'{2}'".FormatWith(serviceBaseUri.TrimEnd('/'), UrlEncode(uriInfo.EntitySetName), UrlEncode(uriInfo.PrimaryKey.ToString()));
		}

		public static string GetEntityFileAttachmentDataServiceUri(this Entity entity)
		{
			return GetEntityFileAttachmentDataServiceUri(entity, GetCmsServiceBaseUri());
		}

		public static string GetEntityFileAttachmentDataServiceUri(this Entity entity, string serviceBaseUri)
		{
			if (string.IsNullOrEmpty(serviceBaseUri))
			{
				return null;
			}

			var uriInfo = new EntityDataServiceUriInfo(entity);

			if (!uriInfo.Valid)
			{
				return null;
			}

			return "{0}/AttachFilesToEntity?entitySet='{1}'&entityID=guid'{2}'".FormatWith(serviceBaseUri.TrimEnd('/'), UrlEncode(uriInfo.EntitySetName), UrlEncode(uriInfo.PrimaryKey.ToString()));
		}

		private static string UrlEncode(string s)
		{
			return Encoder.UrlEncode(s);
		}

		private class EntityDataServiceUriInfo
		{
			public EntityDataServiceUriInfo(Entity entity)
			{
				if (entity == null)
				{
					Valid = false;

					return;
				}

				var entityAttribute = entity.GetType().GetFirstOrDefaultCustomAttribute<EntityAttribute>();

				if (entityAttribute == null || string.IsNullOrEmpty(entityAttribute.EntitySetName))
				{
					Valid = false;

					return;
				}

				EntitySetName = entityAttribute.EntitySetName;
				PrimaryKey = entity.Id;
				Valid = true;
			}

			public string EntitySetName { get; private set; }

			public Guid PrimaryKey { get; private set; }

			public bool Valid { get; private set; }
		}

		private static string GetCmsServiceBaseUri(string portalName = null)
		{
			// TODO: allow the portalName to be specified
			return PortalCrmConfigurationManager.GetCmsServiceBaseUri(portalName);
		}
	}
}
