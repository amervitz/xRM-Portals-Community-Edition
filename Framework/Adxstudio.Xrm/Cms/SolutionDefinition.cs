/*
  Copyright (c) Microsoft Corporation. All rights reserved.
  Licensed under the MIT License. See License.txt in the project root for license information.
*/

using System;
using System.Collections.Generic;
using System.Linq;
using Adxstudio.Xrm.AspNet.Cms;
using Adxstudio.Xrm.Services.Query;

namespace Adxstudio.Xrm.Cms
{
	public class SolutionDefinition
	{
		public static SolutionDefinition Create(
			string solution,
			IDictionary<string, EntityDefinition> entities,
			IDictionary<string, ManyRelationshipDefinition> manyRelationships)
		{
			return new SolutionDefinition(new[] { solution }, entities, manyRelationships);
		}

		public IEnumerable<string> Solutions { get; private set; }
		public IDictionary<string, EntityDefinition> Entities { get; private set; }
		public IDictionary<string, ManyRelationshipDefinition> ManyRelationships { get; private set; }

		public SolutionDefinition(
			IEnumerable<string> solutions,
			IDictionary<string, EntityDefinition> entities,
			IDictionary<string, ManyRelationshipDefinition> manyRelationships)
		{
			Solutions = solutions;
			Entities = entities;
			ManyRelationships = manyRelationships;
		}

		/// <summary>
		/// Filters out the entities based on the solution version passed to it.
		/// </summary>
		/// <param name="crmSolutions">Dictionary which contains incofrmation of all solutions installed in CRM.</param>
		/// <returns></returns>
		public Dictionary<string, EntityDefinition> GetFilteredEntities(IDictionary<string, SolutionInfo> crmSolutions)
		{
			Dictionary<string, EntityDefinition> filteredEntities = new Dictionary<string, EntityDefinition>();
			foreach (var entity in this.Entities)
			{
				// If solution name is missing in entity definition or CRM is missing this solution, do the filtering based on MicrosoftCrmPortalBase solution version.
				var solutionVersion = !string.IsNullOrEmpty(entity.Value.Solution) && crmSolutions.ContainsKey(entity.Value.Solution)
						? crmSolutions[entity.Value.Solution].SolutionVersion
						: crmSolutions[PortalSolutions.SolutionNames.BaseSolutionName].SolutionVersion;

				if (entity.Value.IntroducedVersion != null)
				{
					var entityVersion = entity.Value.IntroducedVersion;
					if (entityVersion.Major <= solutionVersion.Major && entityVersion.Minor <= solutionVersion.Minor)
					{
						filteredEntities.Add(entity.Key, GetFilteredEntity(entity.Value, solutionVersion, crmSolutions));
					}
				}
				else
				{
					filteredEntities.Add(entity.Key, GetFilteredEntity(entity.Value, solutionVersion, crmSolutions));
				}
			}
			return filteredEntities;
		}

		private EntityDefinition GetFilteredEntity(EntityDefinition entity, Version solutionVersion, IDictionary<string, SolutionInfo> crmSolutions)
		{
			var filteredRelationships = entity.GetFilteredRelationships(crmSolutions);
			var filteredColumnSet = entity.GetFilteredColumns(solutionVersion);
			return new EntityDefinition(entity.Solution, 
				entity.LogicalName, 
				entity.PrimaryIdAttributeName,
				entity.EntityNodeType,
				entity.ActiveStateCode,
				filteredColumnSet,
				entity.QueryBuilder,
				entity.IntroducedVersion,
				entity.CheckEntityBeforeContentMapRefresh, 
				filteredRelationships);
		}

		#region Union Members

		public SolutionDefinition Union(SolutionDefinition solution)
		{
			return Union(this, solution);
		}

		private static SolutionDefinition Union(SolutionDefinition first, SolutionDefinition second)
		{
			if (first == null && second == null) return null;
			if (first == null) return second;
			if (second == null) return first;

			var solutions = first.Solutions.Union(second.Solutions).ToArray();
			var entities = Union(first.Entities, second.Entities, first.Solutions).ToDictionary(e => e.LogicalName, e => e);
			var relationships = Union(first.ManyRelationships, second.ManyRelationships, first.Solutions).ToDictionary(r => r.SchemaName, r => r);

			return new SolutionDefinition(solutions, entities, relationships);
		}

		private static IEnumerable<EntityDefinition> Union(IDictionary<string, EntityDefinition> first, IDictionary<string, EntityDefinition> second, IEnumerable<string> baseSolutions)
		{
			if (first == null && second == null) yield break;

			if (first == null)
			{
				foreach (var entity in second.Values)
				{
					yield return entity;
				}

				yield break;
			}

			if (second == null)
			{
				foreach (var entity in first.Values)
				{
					yield return entity;
				}

				yield break;
			}

			foreach (var fe in first.Values)
			{
				EntityDefinition se;

				if (second.TryGetValue(fe.LogicalName, out se) && fe.Solution == se.Solution)
				{
					yield return Union(fe, se);
				}
				else
				{
					yield return fe;
				}
			}

			foreach (var se in second.Values.Where(e => !baseSolutions.Contains(e.Solution)))
			{
				yield return se;
			}
		}

		private static EntityDefinition Union(EntityDefinition first, EntityDefinition second)
		{
			if (first == null && second == null) return null;
			if (first == null) return second;
			if (second == null) return first;

			var columnSets = Union(first.ColumnSets, second.ColumnSets);
			var relationships = Union(first.Relationships, second.Relationships);

			// allow non-null values of the second solution to override the first solution

			var entity = new EntityDefinition(
				second.Solution ?? first.Solution,
				second.LogicalName ?? first.LogicalName,
				second.PrimaryIdAttributeName ?? first.PrimaryIdAttributeName,
				second.EntityNodeType ?? first.EntityNodeType,
				second.ActiveStateCode ?? first.ActiveStateCode,
				columnSets,
				second.QueryBuilder ?? first.QueryBuilder,
				second.IntroducedVersion ?? first.IntroducedVersion,
				second.CheckEntityBeforeContentMapRefresh || first.CheckEntityBeforeContentMapRefresh,
				relationships);

			return entity;
		}

		private static IEnumerable<T> Union<T>(IEnumerable<T> first, IEnumerable<T> second)
		{
			if (first == null && second == null) return null;
			if (first == null) return second;
			if (second == null) return first;

			return first.Union(second).ToArray();
		}

		private static IEnumerable<ManyRelationshipDefinition> Union(IDictionary<string, ManyRelationshipDefinition> first, IDictionary<string, ManyRelationshipDefinition> second, IEnumerable<string> baseSolutions)
		{
			if (first == null && second == null) yield break;

			if (first == null)
			{
				foreach (var relationship in second.Values)
				{
					yield return relationship;
				}

				yield break;
			}

			if (second == null)
			{
				foreach (var relationship in first.Values)
				{
					yield return relationship;
				}

				yield break;
			}

			foreach (var sr in second.Values.Where(e => !baseSolutions.Contains(e.Solution)))
			{
				yield return sr;
			}
		}

		#endregion

		#region QueryExpression Members

		public IEnumerable<Fetch> GetQueries(IDictionary<string, object> parameters)
		{
			foreach (var ed in Entities.Values.Where(e => e.QueryBuilder != null))
			{
				yield return ed.QueryBuilder.CreateQuery(ed, parameters);
			}
		}

		#endregion
	}
}
