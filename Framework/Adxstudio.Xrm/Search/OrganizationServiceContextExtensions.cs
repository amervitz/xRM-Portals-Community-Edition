/*
  Copyright (c) Microsoft Corporation. All rights reserved.
  Licensed under the MIT License. See License.txt in the project root for license information.
*/

using System;
using System.Collections.Generic;
using Adxstudio.Xrm.Resources;
using Microsoft.Xrm.Client;
using Microsoft.Xrm.Sdk.Client;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;

namespace Adxstudio.Xrm.Search
{
	internal static class OrganizationServiceContextExtensions
	{
		public static EntityMetadata GetEntityMetadata(this OrganizationServiceContext context, string logicalName, EntityFilters entityFilters)
		{
			var response = context.Execute(new RetrieveEntityRequest { LogicalName = logicalName, EntityFilters = entityFilters }) as RetrieveEntityResponse;

			if (response == null)
			{
				throw new InvalidOperationException("Unable to retrieve the entity metadata for {0}.".FormatWith(logicalName));
			}

			return response.EntityMetadata;
		}

		public static EntityMetadata GetEntityMetadata(this OrganizationServiceContext context, string logicalName)
		{
			return GetEntityMetadata(context, logicalName, EntityFilters.Attributes);
		}

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

		public static bool AssertEntityExists(this OrganizationServiceContext context, string logicalName)
		{
			try
			{
				return Metadata.OrganizationServiceContextExtensions.EntityExists(context, logicalName);
			}
			catch (Exception e)
			{
				SearchEventSource.Log.WriteError(e);
				return false;
			}
		}
	}
}
