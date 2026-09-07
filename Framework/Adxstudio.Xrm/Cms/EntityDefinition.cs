/*
  Copyright (c) Microsoft Corporation. All rights reserved.
  Licensed under the MIT License. See License.txt in the project root for license information.
*/

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Adxstudio.Xrm.AspNet.Cms;
using Adxstudio.Xrm.Services.Query;
using Adxstudio.Xrm.Web.UI;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace Adxstudio.Xrm.Cms
{
	/// <summary>
	/// Describes a content map column set.
	/// </summary>
	public class SolutionColumnSet
	{
		public SolutionColumnSet(string solution, params EntityNodeColumn[] columns)
		{
			Solution = solution;
			EntityNodeColumns = columns;
		}

		public string Solution { get; private set; }

		public IEnumerable<string> ColumnSet
		{
			get
			{
				if (EntityNodeColumns != null && EntityNodeColumns.Any())
				{
					return EntityNodeColumns.Select(column => column.Name);
				}
				return null;
			}
		}

		public IEnumerable<EntityNodeColumn> EntityNodeColumns { get; private set; }

		public SolutionColumnSet GetFilteredColumns(Version solutionVersion)
		{
			List<EntityNodeColumn> filteredColumns = new List<EntityNodeColumn>();
			if (this.EntityNodeColumns != null)
			{
				foreach (var column in this.EntityNodeColumns)
				{
					if (column.IntroducedVersion != null)
					{
						var columnVersion = column.IntroducedVersion;
						if (columnVersion.Major <= solutionVersion.Major && columnVersion.Minor <= solutionVersion.Minor)
						{
							filteredColumns.Add(column);
						}
					}
					else
					{
						filteredColumns.Add(column);
					}
				}
			}
			return new SolutionColumnSet(this.Solution, filteredColumns.ToArray());
		}
	}

	/// <summary>
	/// Describes a content map entity node.
	/// </summary>
	public class EntityDefinition
	{
		/// <summary>
		/// Builds and returns an <see cref="EntityDefinition"/> object.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="solution"></param>
		/// <param name="logicalName"></param>
		/// <param name="primaryIdAttributeName"></param>
		/// <param name="columnSet"></param>
		/// <param name="activeStateCode"></param>
		/// <param name="queryBuilder"></param>
		/// <param name="version"></param>
		/// <param name="relationships"></param>
		/// <returns>New <see cref="EntityDefinition"/> object.</returns>
		public static EntityDefinition Create<T>(
			string solution,
			string logicalName,
			string primaryIdAttributeName,
			EntityNodeColumn[] columnSet,
			int? activeStateCode,
			QueryBuilder queryBuilder,
			Version version,
			params RelationshipDefinition[] relationships)
			where T : EntityNode
		{
			return Create<T>(solution, logicalName, primaryIdAttributeName, columnSet, activeStateCode, queryBuilder, version, false, relationships);
		}

		/// <summary>
		/// Builds and returns an <see cref="EntityDefinition"/> object.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="solution"></param>
		/// <param name="logicalName"></param>
		/// <param name="primaryIdAttributeName"></param>
		/// <param name="columnSet"></param>
		/// <param name="activeStateCode"></param>
		/// <param name="queryBuilder"></param>
		/// <param name="version"></param>
		/// <param name="checkEntityBeforeRefreshing">Whether to check an entity matches on the relationships before refreshing it into the ContentMap. Typically this is not necessary.</param>
		/// <param name="relationships"></param>
		/// <returns>New <see cref="EntityDefinition"/> object.</returns>
		public static EntityDefinition Create<T>(
			string solution,
			string logicalName,
			string primaryIdAttributeName,
			EntityNodeColumn[] columnSet,
			int? activeStateCode,
			QueryBuilder queryBuilder,
			Version version,
			bool checkEntityBeforeRefreshing,
			params RelationshipDefinition[] relationships)
			where T : EntityNode
		{
			return new EntityDefinition(
				solution, 
				logicalName, 
				primaryIdAttributeName, 
				typeof(T), 
				activeStateCode,
				new[]
				{
					new SolutionColumnSet(solution, columnSet),
				},
				queryBuilder,
				version,
				checkEntityBeforeRefreshing,
				relationships);
		}

		/// <summary>
		/// Builds and returns an <see cref="EntityDefinition"/> object.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="solution"></param>
		/// <param name="logicalName"></param>
		/// <param name="columnSet"></param>
		/// <param name="version"></param>
		/// <param name="relationships"></param>
		/// <returns>New <see cref="EntityDefinition"/> object.</returns>
		public static EntityDefinition Extend<T>(
			string solution,
			string logicalName,
			SolutionColumnSet columnSet,
			Version version,
			params RelationshipDefinition[] relationships)
		{
			var columnSets = columnSet != null ? new[] { columnSet } : null;
			return new EntityDefinition(solution, logicalName, null, typeof(T), null, columnSets, null, version, false, relationships);
		}

		public string LogicalName { get; private set; }
		public string PrimaryIdAttributeName { get; private set; }
		public Type EntityNodeType { get; private set; }
		public string Solution { get; private set; }
		public int? ActiveStateCode { get; private set; }
		public IEnumerable<SolutionColumnSet> ColumnSets { get; private set; }
		public IEnumerable<RelationshipDefinition> Relationships { get; private set; }
		public QueryBuilder QueryBuilder { get; private set; }

		/// <summary>
		/// Introduced version of entity in portals.
		/// </summary>
		public Version IntroducedVersion { get; private set; }

		/// <summary>
		/// Whether to check an entity matches on the relationships before refreshing it into the ContentMap. Typically this is not necessary.
		/// When the ContentMap is built, only entities that match on the relationships defined by the solution are included. 
		/// But when the ContentMap is refreshed for a particular entity (or entities), those relationships are not enforced, and so
		/// an entity could be added to ContentMap even if it doesn't match on any relationships definition. Setting this to true will
		/// force an extra check to prevent this scenario.
		/// </summary>
		public bool CheckEntityBeforeContentMapRefresh { get; private set; }

		/// <summary>
		/// <see cref="EntityDefinition"/> constructor.
		/// </summary>
		/// <param name="solution"></param>
		/// <param name="logicalName"></param>
		/// <param name="primaryIdAttributeName"></param>
		/// <param name="entityNodeType"></param>
		/// <param name="activeStateCode"></param>
		/// <param name="columnSets"></param>
		/// <param name="queryBuilder"></param>
		/// <param name="version"></param>
		/// <param name="checkEntityBeforeRefreshing">Whether to check an entity matches on the relationships before refreshing it into the ContentMap. Typically this is not necessary.</param>
		/// <param name="relationships"></param>
		public EntityDefinition(
			string solution,
			string logicalName,
			string primaryIdAttributeName,
			Type entityNodeType,
			int? activeStateCode,
			IEnumerable<SolutionColumnSet> columnSets,
			QueryBuilder queryBuilder,
			Version version,
			bool checkEntityBeforeRefreshing,
			IEnumerable<RelationshipDefinition> relationships)
		{
			Solution = solution;
			LogicalName = logicalName;
			PrimaryIdAttributeName = primaryIdAttributeName;
			EntityNodeType = entityNodeType;
			ActiveStateCode = activeStateCode;
			ColumnSets = columnSets;
			QueryBuilder = queryBuilder;
			IntroducedVersion = version;
			CheckEntityBeforeContentMapRefresh = checkEntityBeforeRefreshing;
			Relationships = relationships;
		}

		/// <summary>
		/// Checks if a given entity should be included in the content map by making sure it matches one of the relationships defined for this entity.
		/// Note: will only perform the check if 'CheckEntityBeforeContentMapRefresh' is set to True for this entity definition, otherwise will always return true.
		/// </summary>
		/// <param name="entity">Entity to check.</param>
		/// <returns>Whether the given entity should be included in the content map.</returns>
		public bool ShouldIncludeInContentMap(Entity entity)
		{
			if (!this.CheckEntityBeforeContentMapRefresh)
			{
				// This entity definition doesn't have 'CheckEntityBeforeContentMapRefresh' enabled, so automatically return true.
				return true;
			}
			
			if (!this.Relationships.Any())
			{
				// No relationships to check, automatically include.
				return true;
			}

			foreach (var relationship in this.Relationships)
			{
				var regardingEntity = entity.GetAttributeValue<EntityReference>(relationship.ForeignIdAttributeName);
				if (string.Equals(regardingEntity?.LogicalName, relationship.ForeignEntityLogicalname))
				{
					// Found matching relationship, include.
					return true;
				}
			}

			// If we fell through, then it means the entity didn't match any defined relationship, exclude.
			return false;
		}

		public Fetch CreateFetchExpression()
		{
			var fetch = new Fetch
			{
				Entity = new FetchEntity(LogicalName) { Attributes = GetFetchAttributes(), Filters = GetStateCodeFilters() }
			};

			return fetch;
		}

		private ICollection<FetchAttribute> GetFetchAttributes()
		{
			// merge the column sets

			return ColumnSets.SelectMany(set => set.ColumnSet).Select(set => new FetchAttribute { Name = set }).ToArray();
		}

		private ICollection<Filter> GetStateCodeFilters()
		{
			if (ActiveStateCode == null) return null;

			return new[]
			{
				new Filter
				{
					Conditions = new[] { new Condition("statecode", ConditionOperator.Equal, ActiveStateCode.Value) }
				}
			};
		}

		public Fetch CreateFetch()
		{
			var columnSet = ColumnSets.SelectMany(set => set.ColumnSet).ToArray();
			var fetch = new Fetch
			{
				Entity = new FetchEntity(LogicalName)
				{
					Attributes = columnSet.Select(column => new FetchAttribute(column)).ToList()
				},
				SkipCache = true
			};

			if (ActiveStateCode != null)
			{
				fetch.AddFilter(new Filter
				{
					Conditions = new List<Condition>
					{
						new Condition("statecode", ConditionOperator.Equal, ActiveStateCode.Value)
					}
				});
			}

			return fetch;
		}

		public EntityNode ToNode(Entity entity)
		{
			var node = Activator.CreateInstance(EntityNodeType, entity, null) as EntityNode;
			return node;
		}

		/// <summary>
		/// Filters out columns based on solution version.
		/// </summary>
		/// <param name="solutionVersion"></param>
		/// <returns></returns>
		public IEnumerable<SolutionColumnSet> GetFilteredColumns(Version solutionVersion)
		{
			List<SolutionColumnSet> filteredColumnSet = new List<SolutionColumnSet>();
			foreach (var solutionColumnSet in this.ColumnSets)
			{
				filteredColumnSet.Add(solutionColumnSet.GetFilteredColumns(solutionVersion));
			}
			return filteredColumnSet;
		}

		/// <summary>
		/// Filters out the relationships based on the solution version.
		/// </summary>
		/// <param name="crmSolutions">Dictionary which contains incofrmation of all solutions installed in CRM.</param>
		/// <returns></returns>
		public IEnumerable<RelationshipDefinition> GetFilteredRelationships(IDictionary<string, SolutionInfo> crmSolutions)
		{
			List<RelationshipDefinition> filteredRelationships = new List<RelationshipDefinition>();

			if (this.Relationships != null)
			{
				foreach (var relationship in this.Relationships)
				{
					// If solution name is missing in Relationship definition or CRM is missing this solution, do the filtering based on MicrosoftCrmPortalBase solution version.
					var solutionVersion = !string.IsNullOrEmpty(relationship.Solution) && crmSolutions.ContainsKey(relationship.Solution)
						? crmSolutions[relationship.Solution].SolutionVersion
						: crmSolutions[PortalSolutions.SolutionNames.BaseSolutionName].SolutionVersion;
					

					if (relationship.IntroducedVersion != null)
					{
						var relationshipVersion = relationship.IntroducedVersion;
						if (relationshipVersion.Major <= solutionVersion.Major && relationshipVersion.Minor <= solutionVersion.Minor)
						{
							filteredRelationships.Add(relationship);
						}
					}
					else
					{
						filteredRelationships.Add(relationship);
					}
				}
			}
			return filteredRelationships;
		}
	}
}
