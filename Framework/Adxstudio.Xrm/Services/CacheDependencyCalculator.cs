/*
  Copyright (c) Microsoft Corporation. All rights reserved.
  Licensed under the MIT License. See License.txt in the project root for license information.
*/

namespace Adxstudio.Xrm.Services
{
	using System;
	using System.Collections.Generic;
	using System.Linq;

	using Adxstudio.Xrm.Diagnostics.Trace;
	using Adxstudio.Xrm.EventHubBasedInvalidation;
	using Adxstudio.Xrm.Services.Query;

	using Microsoft.Crm.Sdk.Messages;
	using Microsoft.Xrm.Client;
	using Microsoft.Xrm.Client.Services;
	using Microsoft.Xrm.Sdk;
	using Microsoft.Xrm.Sdk.Messages;
	using Microsoft.Xrm.Sdk.Query;

	/// <summary>The cache dependency calculator.</summary>
	public class CacheDependencyCalculator
	{
		/// <summary>The _dependency entity object format.</summary>
		public static readonly string DependencyEntityObjectFormat = "{0}:entity:{1}:id={2}";

		/// <summary>The _dependency entity class format.</summary>
		public static readonly string DependencyEntityClassFormat = "{0}:entity:{1}";

		/// <summary>The _dependency metadata format.</summary>
		public static readonly string DependencyMetadataFormat = "{0}:metadata:*";

		/// <summary>The dependency content format.</summary>
		public static readonly string DependencyContentFormat = "{0}:content:*";

		/// <summary>The _dependency tag format.</summary>
		public static readonly string DependencyTagFormat = "{0}:tag:{1}";

		/// <summary>The tag single.</summary>
		public static readonly string TagSingle = "single";

		/// <summary>The _tag single entity format.</summary>
		public static readonly string TagSingleEntityFormat = "{0}:unique:{1}";

		/// <summary>Gets the cache entry change monitor prefix.</summary>
		public string CacheEntryChangeMonitorPrefix { get; }

		/// <summary>Initializes a new instance of the <see cref="CacheDependencyCalculator"/> class.</summary>
		/// <param name="cacheEntryChangeMonitorPrefix">The cache entry change monitor prefix.</param>
		public CacheDependencyCalculator(string cacheEntryChangeMonitorPrefix)
		{
			this.CacheEntryChangeMonitorPrefix = cacheEntryChangeMonitorPrefix;
		}

		/// <summary>The is cached request.</summary>
		/// <param name="request">The request.</param>
		/// <returns>The <see cref="bool"/>.</returns>
		public static bool IsCachedRequest(OrganizationRequest request)
		{
			var cached = request as CachedOrganizationRequest;

			if (cached != null)
			{
				return IsCachedRequest(cached.Request);
			}

			var keyed = request as KeyedRequest;

			if (keyed != null)
			{
				return IsCachedRequest(keyed.Request);
			}

			return request != null && Array.BinarySearch(CachedRequestsSorted, request.RequestName) >= 0;
		}

		/// <summary>The get dependencies for object.</summary>
		/// <param name="query">The query.</param>
		/// <param name="isSingle">The is single.</param>
		/// <param name="path">The path.</param>
		/// <returns>The dependencies.</returns>
		public IEnumerable<string> GetDependenciesForObject(object query, bool isSingle = false, IEnumerable<object> path = null)
		{
			// Below block is for empty dependencies.
			if (query is IncrementKnowledgeArticleViewCountRequest) return this.GetDependenciesEmpty(); // Requests of type 'IncrementKnowledgeArticleViewCountRequest' have no dependencies to invalidate.

			// The below block performs an un-wrapping of the queries with any inner requests.
			if (query is CachedOrganizationRequest) return this.GetDependencies(query as CachedOrganizationRequest, isSingle, path ?? new List<object> { query });
			if (query is KeyedRequest) return this.GetDependencies(query as KeyedRequest, isSingle, path ?? new List<object> { query });
			if (query is RetrieveSingleRequest) return this.GetDependencies(query as RetrieveSingleRequest, true, path ?? new List<object> { query });
			if (query is RetrieveSingleResponse) return this.GetDependencies(query as RetrieveSingleResponse, path ?? new List<object> { query });
			if (query is RetrieveRequest) return this.GetDependencies(query as RetrieveRequest, path ?? new List<object> { query });
			if (query is RetrieveResponse) return this.GetDependencies(query as RetrieveResponse, path ?? new List<object> { query });
			if (query is OrganizationRequest) return this.GetDependencies(query as OrganizationRequest, isSingle, path ?? new List<object> { query });
			if (query is OrganizationResponse) return this.GetDependencies(query as OrganizationResponse, isSingle, path ?? new List<object> { query });

			if (query is Fetch) return this.GetDependencies(query as Fetch, isSingle);
			if (query is QueryBase) return this.GetDependencies(query as QueryBase, isSingle);
			if (query is IEnumerable<Entity>) return this.GetDependencies(query as IEnumerable<Entity>, isSingle);
			if (query is IEnumerable<EntityReference>) return this.GetDependencies(query as IEnumerable<EntityReference>, isSingle);
			if (query is EntityCollection) return this.GetDependencies(query as EntityCollection, isSingle);
			if (query is Entity) return this.GetDependencies(query as Entity, isSingle);
			if (query is EntityReference) return this.GetDependencies(query as EntityReference, isSingle);
			if (query is RelationshipQueryCollection) return this.GetDependencies(query as RelationshipQueryCollection, isSingle);

			return this.GetDependenciesEmpty();
		}

		/// <summary>The get dependencies empty.</summary>
		/// <returns>The empty dependencies.</returns>
		internal IEnumerable<string> GetDependenciesEmpty()
		{
			yield break;
		}

		/// <summary>The get dependencies.</summary>
		/// <param name="entity">The entity.</param>
		/// <param name="isSingle">The is single.</param>
		/// <returns>The dependencies.</returns>
		internal IEnumerable<string> GetDependencies(Entity entity, bool isSingle)
		{
			foreach (var dependency in this.GetDependencies(entity.ToEntityReference(), isSingle))
			{
				yield return dependency;
			}

			// Looks for Entity references (lookups) in entity.Attributes and adds dependencies, so when
			// a lookup changes, a main entity also gets invalidated. Like Incident and changing Subject name.
			var attributesLookups = entity.Attributes.Values.OfType<EntityReference>();
			foreach (var lookup in attributesLookups)
			{
				foreach (var related in this.GetDependencies(lookup, true))
				{
					yield return related;
				}
			}

			// walk the related entities
			foreach (var related in this.GetDependencies(entity.RelatedEntities, isSingle))
			{
				yield return related;
			}

			// If the entity is also an Activity, then add dependency to activitypointer.
			EntityReference activityDependency;
			if (this.IsActivityEntity(entity, out activityDependency))
			{
				foreach (var dependency in this.GetDependencies(activityDependency, isSingle))
				{
					yield return dependency;
				}
			}
		}

		/// <summary>The get dependencies.</summary>
		/// <param name="entity">The entity.</param>
		/// <param name="isSingle">The is single.</param>
		/// <returns>The dependencies.</returns>
		internal IEnumerable<string> GetDependencies(EntityReference entity, bool isSingle)
		{
			if (!isSingle)
			{
				yield return this.GetDependency(entity.LogicalName);
			}

			yield return this.GetDependency(entity.LogicalName, entity.Id);
		}

		/// <summary>The get dependency.</summary>
		/// <param name="entityName">The entity name.</param>
		/// <returns>The <see cref="string"/>.</returns>
		internal string GetDependency(string entityName)
		{
			if (!string.IsNullOrWhiteSpace(entityName))
			{
				ADXTrace.Instance.TraceVerbose(TraceCategory.Application, string.Format("Entity {0} is Added to Enabled Entity List for Portal Cache ", EntityNamePrivacy.GetEntityName(entityName)));
				WebAppConfigurationProvider.PortalUsedEntities.TryAdd(entityName, true);
			}

			return DependencyEntityClassFormat.FormatWith(this.CacheEntryChangeMonitorPrefix, entityName);
		}

		/// <summary>The get dependency.</summary>
		/// <param name="entityName">The entity name.</param>
		/// <param name="id">The id.</param>
		/// <returns>The <see cref="string"/>.</returns>
		internal string GetDependency(string entityName, Guid? id)
		{
			if (!string.IsNullOrWhiteSpace(entityName))
			{
				ADXTrace.Instance.TraceVerbose(TraceCategory.Application, string.Format("Entity {0} is Added to Enabled Entity List for Portal Cache", EntityNamePrivacy.GetEntityName(entityName)));
				WebAppConfigurationProvider.PortalUsedEntities.TryAdd(entityName, true);
			}

			return DependencyEntityObjectFormat.FormatWith(this.CacheEntryChangeMonitorPrefix, entityName, id);
		}

		/// <summary>The cached requests content.</summary>
		private static readonly IEnumerable<string> CachedRequestsContent = new[]
		{
			"Retrieve", "RetrieveMultiple", "RetrieveVersion", "WhoAmI", "RetrieveSingle",
		};

		/// <summary>The cached requests metadata.</summary>
		private static readonly IEnumerable<string> CachedRequestsMetadata = new[]
		{
			"RetrieveAllEntities",
			"RetrieveAllOptionSets",
			"RetrieveAllManagedProperties",
			"RetrieveAttribute",
			"RetrieveEntity",
			"RetrieveRelationship",
			"RetrieveTimestamp",
			"RetrieveOptionSet",
			"RetrieveManagedProperty",
			"RetrieveMetadataChanges",
			"RetrieveLocLabels",
			"RetrieveMultipleSystemFormsWithAllLabels",
			"RetrieveProvisionedLanguages"
		};

		/// <summary>The cached requests.</summary>
		private static readonly IEnumerable<string> CachedRequests = CachedRequestsContent.Concat(CachedRequestsMetadata);

		/// <summary>The cached requests sorted.</summary>
		private static readonly string[] CachedRequestsSorted = CachedRequests.OrderBy(r => r).ToArray();

		/// <summary>The is content request.</summary>
		/// <param name="request">The request.</param>
		/// <returns>The <see cref="bool"/>.</returns>
		private static bool IsContentRequest(OrganizationRequest request)
		{
			return request != null && CachedRequestsContent.Contains(request.RequestName);
		}

		/// <summary>The is metadata request.</summary>
		/// <param name="request">The request.</param>
		/// <returns>The <see cref="bool"/>.</returns>
		private static bool IsMetadataRequest(OrganizationRequest request)
		{
			return request != null && CachedRequestsMetadata.Contains(request.RequestName);
		}

		/// <summary>The get dependencies.</summary>
		/// <param name="request">The request.</param>
		/// <param name="isSingle">The is single.</param>
		/// <param name="path">The path.</param>
		/// <returns>The dependencies.</returns>
		private IEnumerable<string> GetDependencies(CachedOrganizationRequest request, bool isSingle, IEnumerable<object> path)
		{
			return this.GetDependenciesForObject(request.Request, isSingle, path);
		}

		/// <summary>The get dependencies.</summary>
		/// <param name="request">The request.</param>
		/// <param name="isSingle">The is single.</param>
		/// <param name="path">The path.</param>
		/// <returns>The dependencies.</returns>
		private IEnumerable<string> GetDependencies(KeyedRequest request, bool isSingle, IEnumerable<object> path)
		{
			return this.GetDependenciesForObject(request.Request, isSingle, path);
		}

		/// <summary>The get dependencies.</summary>
		/// <param name="request">The request.</param>
		/// <param name="isSingle">The is single.</param>
		/// <param name="path">The path.</param>
		/// <returns>The dependencies.</returns>
		private IEnumerable<string> GetDependencies(RetrieveSingleRequest request, bool isSingle, IEnumerable<object> path)
		{
			yield return this.GetTag(TagSingle);

			if (request.Fetch != null)
			{
				yield return this.GetTag("fetch");

				foreach (var dependency in this.GetDependencies(request.Fetch, isSingle))
				{
					yield return dependency;
				}
			}

			foreach (var dependency in this.GetDependenciesForObject(request.Request, isSingle, path))
			{
				yield return dependency;
			}
		}

		/// <summary>The get dependencies.</summary>
		/// <param name="response">The response.</param>
		/// <param name="path">The path.</param>
		/// <returns>The dependencies.</returns>
		private IEnumerable<string> GetDependencies(RetrieveSingleResponse response, IEnumerable<object> path)
		{
			yield return this.GetTag(TagSingle);

			var empty = response.Entity == null;

			if (empty)
			{
				yield return this.GetTag("empty");

				// recalculate the dependencies for an empty result

				foreach (var dependency in this.GetDependencies(response.Request, false, path))
				{
					yield return dependency;
				}
			}

			foreach (var dependency in this.GetDependenciesForObject(response.Entity, true, path))
			{
				yield return dependency;
			}
		}

		/// <summary>The get dependencies.</summary>
		/// <param name="request">The request.</param>
		/// <param name="path">The path.</param>
		/// <returns>The dependencies.</returns>
		private IEnumerable<string> GetDependencies(RetrieveRequest request, IEnumerable<object> path)
		{
			yield return this.GetTag(TagSingle);

			// retrieve is guaranteed to have an entity result

			foreach (var dependency in this.GetDependenciesForObject(request.Target, true, path))
			{
				yield return dependency;
			}

			// disable single dependency mode for related entities

			foreach (var dependency in this.GetDependenciesForObject(request.RelatedEntitiesQuery, false, path))
			{
				yield return dependency;
			}
		}

		/// <summary>The get dependencies.</summary>
		/// <param name="response">The response.</param>
		/// <param name="path">The path.</param>
		/// <returns>The dependencies.</returns>
		private IEnumerable<string> GetDependencies(RetrieveResponse response, IEnumerable<object> path)
		{
			yield return this.GetTag(TagSingle);

			foreach (var dependency in this.GetDependenciesForObject(response.Entity, true, path))
			{
				yield return dependency;
			}
		}

		/// <summary>The get dependencies.</summary>
		/// <param name="request">The request.</param>
		/// <param name="isSingle">The is single.</param>
		/// <param name="path">The path.</param>
		/// <returns>The dependencies.</returns>
		private IEnumerable<string> GetDependencies(OrganizationRequest request, bool isSingle, IEnumerable<object> path)
		{
			if (IsContentRequest(request))
			{
				yield return DependencyContentFormat.FormatWith(this.CacheEntryChangeMonitorPrefix);
			}
			else if (IsMetadataRequest(request))
			{
				yield return DependencyMetadataFormat.FormatWith(this.CacheEntryChangeMonitorPrefix);
			}

			if (request is FetchMultipleRequest)
			{
				yield return this.GetTag("fetch");

				var fetch = ((FetchMultipleRequest)request).Fetch;

				foreach (var dependency in this.GetDependencies(fetch, isSingle))
				{
					yield return dependency;
				}
			}
			else
			{
				foreach (var parameter in request.Parameters)
				{
					var value = parameter.Value;

					if (value != null && !path.Contains(value))
					{
						foreach (var child in this.GetDependenciesForObject(value, isSingle, path.Concat(new[] { value })))
						{
							yield return child;
						}
					}
				}
			}
		}

		/// <summary>The get dependencies.</summary>
		/// <param name="response">The response.</param>
		/// <param name="isSingle">The is single.</param>
		/// <param name="path">The path.</param>
		/// <returns>The dependencies.</returns>
		private IEnumerable<string> GetDependencies(OrganizationResponse response, bool isSingle, IEnumerable<object> path)
		{
			foreach (var parameter in response.Results)
			{
				var value = parameter.Value;

				if (value != null && !path.Contains(value))
				{
					foreach (var child in this.GetDependenciesForObject(value, isSingle, path.Concat(new[] { value })))
					{
						yield return child;
					}
				}
			}
		}

		/// <summary>The get dependencies.</summary>
		/// <param name="query">The query.</param>
		/// <param name="isSingle">The is single.</param>
		/// <returns>The dependencies.</returns>
		private IEnumerable<string> GetDependencies(QueryBase query, bool isSingle)
		{
			if (!isSingle)
			{
				if (query is QueryExpression)
				{
					yield return this.GetDependency((query as QueryExpression).EntityName);

					foreach (var linkEntity in this.GetLinkEntities(query as QueryExpression))
					{
						yield return this.GetDependency(linkEntity.LinkToEntityName);
						yield return this.GetDependency(linkEntity.LinkFromEntityName);
					}
				}
				else if (query is QueryByAttribute)
				{
					yield return this.GetDependency((query as QueryByAttribute).EntityName);
				}
				else if (query is FetchExpression)
				{
					var fetch = Fetch.Parse((query as FetchExpression).Query);

					foreach (var dependency in this.GetDependencies(fetch, isSingle))
					{
						yield return dependency;
					}
				}
			}
		}

		/// <summary>The get dependencies.</summary>
		/// <param name="fetch">The fetch.</param>
		/// <param name="isSingle">The is single.</param>
		/// <returns>The dependencies.</returns>
		private IEnumerable<string> GetDependencies(Fetch fetch, bool isSingle)
		{
			if (!isSingle)
			{
				// generate dependencies from the fetch expression

				if (fetch?.Entity == null)
				{
					yield break;
				}

				yield return this.GetDependency(fetch.Entity.Name);

				if (fetch.Entity.Links == null)
				{
					yield break;
				}

				foreach (var linkEntity in this.GetLinkEntities(fetch.Entity.Links))
				{
					if (linkEntity.IsUnique == true)
					{
						yield return this.GetTag(TagSingle);
						yield return this.GetSingleEntityTag(linkEntity.Name);
					}
					else
					{
						yield return this.GetDependency(linkEntity.Name);
					}
				}
			}
		}

		/// <summary>The get dependencies.</summary>
		/// <param name="collection">The collection.</param>
		/// <param name="isSingle">The is single.</param>
		/// <returns>The dependencies.</returns>
		private IEnumerable<string> GetDependencies(RelationshipQueryCollection collection, bool isSingle)
		{
			foreach (var relatedEntitiesQuery in collection)
			{
				foreach (var dependency in this.GetDependencies(relatedEntitiesQuery.Value, isSingle))
				{
					yield return dependency;
				}
			}
		}

		/// <summary>The get dependencies.</summary>
		/// <param name="entities">The entities.</param>
		/// <param name="isSingle">The is single.</param>
		/// <returns>The dependencies.</returns>
		private IEnumerable<string> GetDependencies(EntityCollection entities, bool isSingle)
		{
			if (!isSingle)
			{
				yield return this.GetDependency(entities.EntityName);
			}

			// try to get any aliased dependencies
			Dictionary<string, Guid> related;
			if (this.TryGetAliasedGuids(entities, out related))
			{
				foreach (var value in related)
				{
					yield return this.GetSingleEntityTag(value.Key);
					yield return this.GetDependency(value.Key, value.Value);
				}
			}

			// Try to see if these entities are custom Activity entities, and if so generate additional dependencies on activitypointer.
			IEnumerable<EntityReference> activityDependencies;
			if (this.TryGetActivityDependencies(entities, out activityDependencies))
			{
				foreach (var value in activityDependencies)
				{
					yield return this.GetDependency(value.LogicalName, value.Id);
				}

				// Also add dependency on the entity logical name
				foreach (var value in activityDependencies.Select(a => a.LogicalName).Distinct())
				{
					yield return this.GetDependency(value);
				}
			}

			foreach (var dependency in this.GetDependencies(entities.Entities, isSingle))
			{
				yield return dependency;
			}
		}

		/// <summary>The get dependencies.</summary>
		/// <param name="entities">The entities.</param>
		/// <param name="isSingle">The is single.</param>
		/// <returns>The dependencies.</returns>
		private IEnumerable<string> GetDependencies(IEnumerable<Entity> entities, bool isSingle)
		{
			return entities.SelectMany(e => this.GetDependencies(e, isSingle));
		}

		/// <summary>The get dependencies.</summary>
		/// <param name="entities">The entities.</param>
		/// <param name="isSingle">The is single.</param>
		/// <returns>The dependencies.</returns>
		private IEnumerable<string> GetDependencies(IEnumerable<EntityReference> entities, bool isSingle)
		{
			return entities.SelectMany(e => this.GetDependencies(e, isSingle));
		}

		/// <summary>The get dependencies.</summary>
		/// <param name="relationships">The relationships.</param>
		/// <param name="isSingle">The is single.</param>
		/// <returns>The dependencies.</returns>
		private IEnumerable<string> GetDependencies(RelatedEntityCollection relationships, bool isSingle)
		{
			return relationships.SelectMany(e => this.GetDependencies(e, isSingle));
		}

		/// <summary>The get dependencies.</summary>
		/// <param name="pair">The pair.</param>
		/// <param name="isSingle">The is single.</param>
		/// <returns>The dependencies.</returns>
		private IEnumerable<string> GetDependencies(KeyValuePair<Relationship, EntityCollection> pair, bool isSingle)
		{
			if (!isSingle)
			{
				yield return this.GetDependency(pair.Key.SchemaName);
			}

			foreach (var entity in this.GetDependencies(pair.Value, isSingle))
			{
				yield return entity;
			}
		}

		/// <summary>The get single entity tag.</summary>
		/// <param name="name">The name.</param>
		/// <returns>The <see cref="string"/>.</returns>
		private string GetSingleEntityTag(string name)
		{
			return TagSingleEntityFormat.FormatWith(this.CacheEntryChangeMonitorPrefix, name);
		}

		/// <summary>The get tag.</summary>
		/// <param name="tag">The tag.</param>
		/// <returns>The <see cref="string"/>.</returns>
		private string GetTag(string tag)
		{
			return DependencyTagFormat.FormatWith(this.CacheEntryChangeMonitorPrefix, tag);
		}

		/// <summary>The get link entities.</summary>
		/// <param name="query">The query.</param>
		/// <returns>The dependencies.</returns>
		private IEnumerable<LinkEntity> GetLinkEntities(QueryExpression query)
		{
			return this.GetLinkEntities(query.LinkEntities);
		}

		/// <summary>The get link entities.</summary>
		/// <param name="linkEntities">The link entities.</param>
		/// <returns>The dependencies.</returns>
		private IEnumerable<LinkEntity> GetLinkEntities(IEnumerable<LinkEntity> linkEntities)
		{
			foreach (var linkEntity in linkEntities)
			{
				if (linkEntity != null)
				{
					yield return linkEntity;

					foreach (var child in this.GetLinkEntities(linkEntity.LinkEntities))
					{
						yield return child;
					}
				}
			}
		}

		/// <summary>The get link entities.</summary>
		/// <param name="linkEntities">The link entities.</param>
		/// <returns>The dependencies.</returns>
		private IEnumerable<Link> GetLinkEntities(IEnumerable<Link> linkEntities)
		{
			if (linkEntities == null)
			{
				yield break;
			}

			foreach (var linkEntity in linkEntities)
			{
				if (linkEntity != null)
				{
					yield return linkEntity;

					foreach (var child in this.GetLinkEntities(linkEntity.Links))
					{
						yield return child;
					}
				}
			}
		}

		/// <summary>The try get aliased guids.</summary>
		/// <param name="entities">The entities.</param>
		/// <param name="related">The related.</param>
		/// <returns>The <see cref="bool"/>.</returns>
		private bool TryGetAliasedGuids(EntityCollection entities, out Dictionary<string, Guid> related)
		{
			related = new Dictionary<string, Guid>();
			foreach (var entity in entities.Entities)
			{
				foreach (var attribute in entity.Attributes.Values)
				{
					var value = attribute as AliasedValue;
					if (value != null && !related.ContainsKey(value.EntityLogicalName) && value.Value is Guid)
					{
						related.Add(value.EntityLogicalName, (Guid)value.Value);
					}
				}
			}

			return related.Any();
		}

		/// <summary>
		/// Checks if any entity is a custom Activity entity, and if so generates additional respective dependencies on activitypointer.
		/// ex: If given an adx_portalcomment entity, then an additional dependency should be placed on activitypointer with this entity's ID.
		/// </summary>
		/// <param name="entities">Entities to check.</param>
		/// <param name="activityDependencies">Additional activitypointer dependencies.</param>
		/// <returns>Whether any additional activity dependencies were discovered.</returns>
		private bool TryGetActivityDependencies(EntityCollection entities, out IEnumerable<EntityReference> activityDependencies)
		{
			var activityDependenciesList = new List<EntityReference>();
			foreach (var entity in entities.Entities)
			{
				EntityReference activityEntity;
				if (this.IsActivityEntity(entity, out activityEntity))
				{
					activityDependenciesList.Add(activityEntity);
				}
			}
			activityDependencies = activityDependenciesList;
			return activityDependencies.Any();
		}

		/// <summary>
		/// Checks if an entity is a custom Activity entity by checking for:
		/// 1) the entity isn't already an activitypointer, 2) presence of "activityid" attribute and 3) if the activityid = entity.Id.
		/// ex: If given an adx_portalcomment entity, then an additional dependency should be placed on activitypointer with this entity's ID.
		/// </summary>
		/// <param name="entity">Entity to check.</param>
		/// <param name="activityEntity">The given entity as an activitypointer.</param>
		/// <returns>Whether the given entity is an Activity.</returns>
		private bool IsActivityEntity(Entity entity, out EntityReference activityEntity)
		{
			activityEntity = new EntityReference("activitypointer", entity.Id);

			// If the entity has an activityid, but is not an activitypointer, then this entity is also an Activity.
			if (entity.Attributes.Contains("activityid") && !string.Equals(entity.LogicalName, "activitypointer", StringComparison.CurrentCultureIgnoreCase))
			{
				// If this truly is an activity, then the activityid will equal the entity id.
				Guid activityId;
				if (Guid.TryParse(entity.GetAttributeValue("activityid").ToString(), out activityId))
				{
					if (activityId == entity.Id)
					{
						return true;
					}
				}
			}
			return false;
		}
	}
}
