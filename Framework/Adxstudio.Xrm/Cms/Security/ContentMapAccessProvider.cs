/*
  Copyright (c) Microsoft Corporation. All rights reserved.
  Licensed under the MIT License. See License.txt in the project root for license information.
*/

namespace Adxstudio.Xrm.Cms.Security
{
	using System.Web;
	using System.Web.Security;
	using Adxstudio.Xrm.Configuration;
	using Adxstudio.Xrm.Security;
	using Adxstudio.Xrm.Web;
	using Microsoft.Xrm.Client.Security;
	using Microsoft.Xrm.Sdk;
	using Microsoft.Xrm.Sdk.Client;

	/// <summary> The content map access provider. </summary>
	internal abstract class ContentMapAccessProvider : CacheSupportingCrmEntitySecurityProvider
	{
		/// <summary> The content map provider. </summary>
		protected IContentMapProvider ContentMapProvider { get; set; }

		/// <summary> Initializes a new instance of the <see cref="ContentMapAccessProvider"/> class. </summary>
		/// <param name="context"> The context. </param>
		protected ContentMapAccessProvider(HttpContext context)
			: this(context != null ? context.GetContentMapProvider() : AdxstudioCrmConfigurationManager.CreateContentMapProvider())
		{
		}

		/// <summary> Initializes a new instance of the <see cref="ContentMapAccessProvider"/> class. </summary>
		/// <param name="contentMapProvider"> The content map provider. </param>
		protected ContentMapAccessProvider(IContentMapProvider contentMapProvider)
		{
			this.ContentMapProvider = contentMapProvider ?? AdxstudioCrmConfigurationManager.CreateContentMapProvider();
		}

		/// <summary> The get user roles. </summary>
		/// <returns> The user roles. </returns>
		protected string[] GetUserRoles()
		{
			// Windows Live ID Server decided to return null for an unauthenticated user's name
			// A null username, however, breaks the Roles.GetRolesForUser() because it expects an empty string.
			var currentUsername = HttpContext.Current.User != null && HttpContext.Current.User.Identity != null
									? HttpContext.Current.User.Identity.Name ?? string.Empty
									: string.Empty;

			return Roles.GetRolesForUser(currentUsername);
		}

		/// <summary> The try assert. </summary>
		/// <param name="context"> The context. </param>
		/// <param name="entityReference"> The entity reference. </param>
		/// <param name="right"> The right. </param>
		/// <param name="dependencies"> The dependencies. </param>
		/// <returns> The assertion. </returns>
		public bool TryAssert(OrganizationServiceContext context, EntityReference entityReference, CrmEntityRight right, CrmEntityCacheDependencyTrace dependencies)
		{

			EntityNode entity = null;
			this.ContentMapProvider.Using(map => map.TryGetValue(entityReference, out entity));

			return entity != null && this.TryAssert(context, entity.ToEntity(), right, dependencies);
		}

		/// <summary> The try assert. </summary>
		/// <param name="context"> The context. </param>
		/// <param name="entity"> The entity. </param>
		/// <param name="right"> The right. </param>
		/// <param name="dependencies"> The dependencies. </param>
		/// <returns> The assertion. </returns>
		public override bool TryAssert(OrganizationServiceContext context, Entity entity, CrmEntityRight right, CrmEntityCacheDependencyTrace dependencies)
		{
			return this.ContentMapProvider.Using(map => this.TryAssert(context, entity, right, dependencies, map));
		}

		/// <summary> The try assert. </summary>
		/// <param name="context"> The context. </param>
		/// <param name="entity"> The entity. </param>
		/// <param name="right"> The right. </param>
		/// <param name="dependencies"> The dependencies. </param>
		/// <param name="map"> The map. </param>
		/// <returns> The assertion. </returns>
		protected abstract bool TryAssert(OrganizationServiceContext context, Entity entity, CrmEntityRight right, CrmEntityCacheDependencyTrace dependencies, ContentMap map);

		/// <summary> The add dependencies. </summary>
		/// <param name="dependencies"> The dependencies. </param>
		/// <param name="entity"> The entity. </param>
		/// <param name="attributes"> The attributes. </param>
		protected void AddDependencies(CrmEntityCacheDependencyTrace dependencies, Entity entity, string[] attributes)
		{
			foreach (var attribute in attributes)
			{
				dependencies.AddEntitySetDependency(attribute);
			}

			dependencies.AddEntityDependency(entity);
		}
	}
}
