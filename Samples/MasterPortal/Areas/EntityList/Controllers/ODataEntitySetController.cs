/*
  Copyright (c) Microsoft Corporation. All rights reserved.
  Licensed under the MIT License. See License.txt in the project root for license information.
*/

using System;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using Microsoft.AspNet.OData;
using Microsoft.AspNet.OData.Extensions;
using Microsoft.AspNet.OData.Query;
using Adxstudio.Xrm.Resources;
using Adxstudio.Xrm.Web.UI.EntityList.OData;
using Microsoft.OData.Edm;

namespace Site.Areas.EntityList.Controllers
{
	public class ODataEntitySetController : ODataController
	{
		public EdmEntityObjectCollection Get()
		{
			var path = Request.ODataProperties().Path;
			var edmType = path.EdmType;
			var collectionType = edmType as IEdmCollectionType;
			
			if (edmType.TypeKind != EdmTypeKind.Collection || collectionType == null)
			{				
                throw new HttpResponseException(Request.CreateErrorResponse(HttpStatusCode.InternalServerError, string.Format("EdmType.TypeKind is not valid."))); 
           	}

			var entityType = collectionType.ElementType.AsEntity();
			var entitySetName = path.NavigationSource.Name;
			var model = Request.GetModel();
			var dataAdapter = new EntityListODataFeedDataAdapter(new PortalConfigurationDataAdapterDependencies());
			var pageSize = dataAdapter.GetPageSize(model, entitySetName);
			var queryContext = new ODataQueryContext(Request.GetModel(), entityType.Definition, path);
			var queryOptions = new ODataQueryOptions(queryContext, Request);
			var querySettings = new ODataQuerySettings { PageSize = pageSize };
			
			// http://www.asp.net/web-api/overview/odata-support-in-aspnet-web-api/odata-security-guidance
			var validationSettings = new ODataValidationSettings
			{
				AllowedFunctions = AllowedFunctions.EndsWith | AllowedFunctions.StartsWith | AllowedFunctions.Contains,
				AllowedQueryOptions = AllowedQueryOptions.Filter | AllowedQueryOptions.OrderBy | AllowedQueryOptions.Top | AllowedQueryOptions.Skip | AllowedQueryOptions.Count | AllowedQueryOptions.Format,
				MaxNodeCount = 100,
				MaxTop = pageSize
			};
			
			queryOptions.Validate(validationSettings);

			return dataAdapter.SelectMultiple(model, entitySetName, queryOptions, querySettings, Request);
		}

		public IEdmEntityObject Get([FromODataUri] Guid key)
		{
			var path = Request.ODataProperties().Path;
			var entityType = path.EdmType as IEdmEntityType;
			var entitySetName = path.NavigationSource.Name;
			var model = Request.GetModel();
			var dataAdapter = new EntityListODataFeedDataAdapter(new PortalConfigurationDataAdapterDependencies());
			var entity = dataAdapter.Select(model, entitySetName, key);

			if (entity == null)
			{
				throw new HttpResponseException(Request.CreateErrorResponse(HttpStatusCode.NotFound, string.Format("{0} couldn't be found with key {1}.", entitySetName, key)));
			}

			return entity;
		}
	}
}
