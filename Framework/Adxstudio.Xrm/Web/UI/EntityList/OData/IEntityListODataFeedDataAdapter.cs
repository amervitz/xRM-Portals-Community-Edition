/*
  Copyright (c) Microsoft Corporation. All rights reserved.
  Licensed under the MIT License. See License.txt in the project root for license information.
*/

using System;
using System.Collections.Generic;
using System.Net.Http;
using Microsoft.AspNet.OData;
using Microsoft.OData.Edm;
using Microsoft.Xrm.Sdk;

namespace Adxstudio.Xrm.Web.UI.EntityList.OData
{
	/// <summary>
	/// Provides operations on Entity List for OData feeds
	/// </summary>
	public interface IEntityListODataFeedDataAdapter
	{
		/// <summary>
		/// Language code used to return labels for the specified local.
		/// </summary>
		int LanguageCode { get; }

		/// <summary>
		/// Name used to specify the Namespace for the model.
		/// </summary>
		string NamespaceName { get; }

		/// <summary>
		/// Get EdmModel
		/// </summary>
		/// <returns><see cref="IEdmModel"/></returns>
		IEdmModel GetEdmModel();

		/// <summary>
		/// Get the entity list records for a given website that will be used to get the view definitions
		/// </summary>
		/// <param name="website">Website <see cref="EntityReference"/></param>
		List<Entity> GetEntityLists(EntityReference website);

		/// <summary>
		/// Get the view
		/// </summary>
		/// <param name="savedQueryId">Unique identifier of the savedquery entity</param>
		/// <returns><see cref="SavedQueryView"/></returns>
		SavedQueryView GetView(Guid savedQueryId);

		/// <summary>
		/// Get the view
		/// </summary>
		/// <param name="savedQuery">savedquery entity</param>
		/// <returns><see cref="SavedQueryView"/></returns>
		SavedQueryView GetView(Entity savedQuery);

		/// <summary>
		/// Get's the entity list associated with the entity set name in the model
		/// </summary>
		/// <param name="model"><see cref="IEdmModel"/></param>
		/// <param name="entitySetName">Name of the entity set</param>
		/// <returns><see cref="Entity"/></returns>
		Entity GetEntityList(IEdmModel model, string entitySetName);

		/// <summary>
		/// Get's the page size defined on the entity list associated with the entity set name in the model.
		/// </summary>
		/// <param name="model"><see cref="IEdmModel"/></param>
		/// <param name="entitySetName">Name of the entity set</param>
		/// <returns>page size</returns>
		int GetPageSize(IEdmModel model, string entitySetName);

		/// <summary>
		/// Gets the collection of records for the requested entity set and specified OData query options.
		/// </summary>
		/// <param name="model"><see cref="IEdmModel"/></param>
		/// <param name="entitySetName">Name of the entity set</param>
		/// <param name="queryOptions"><see cref="Microsoft.AspNet.OData.Query.ODataQueryOptions"/></param>
		/// <param name="querySettings"><see cref="Microsoft.AspNet.OData.Query.ODataQuerySettings"/></param>
		/// <param name="request"><see cref="HttpRequestMessage"/></param>
		/// <returns><see cref="EdmEntityObjectCollection"/></returns>
		EdmEntityObjectCollection SelectMultiple(IEdmModel model, string entitySetName, Microsoft.AspNet.OData.Query.ODataQueryOptions queryOptions, Microsoft.AspNet.OData.Query.ODataQuerySettings querySettings, HttpRequestMessage request);

		/// <summary>
		/// Get a single record for the specified entity set where the object's key matches the id provided.
		/// </summary>
		/// <param name="model"><see cref="IEdmModel"/></param>
		/// <param name="entitySetName">Name of the entity set</param>
		/// <param name="id">Unique Identifier key of the record</param>
		/// <returns>A single <see cref="IEdmEntityObject"/></returns>
		IEdmEntityObject Select(IEdmModel model, string entitySetName, Guid id);
	}
}
