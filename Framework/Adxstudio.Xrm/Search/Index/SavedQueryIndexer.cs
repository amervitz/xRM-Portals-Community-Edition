/*
  Copyright (c) Microsoft Corporation. All rights reserved.
  Licensed under the MIT License. See License.txt in the project root for license information.
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using Adxstudio.Xrm.Resources;
using Microsoft.Xrm.Client.Diagnostics;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Client;

namespace Adxstudio.Xrm.Search.Index
{
	public class SavedQueryIndexer : ICrmEntityIndexer
	{
		private Lazy<ICrmEntityIndexer[]> _indexers;

		public SavedQueryIndexer(ICrmEntityIndex index, string savedQueryName)
		{
			if (index == null)
			{
				throw new ArgumentNullException("index");
			}

			if (string.IsNullOrEmpty(savedQueryName))
			{
				throw new ArgumentException("Can't be null or empty.", "savedQueryName");
			}

			Index = index;
			SavedQueryName = savedQueryName;
			_indexers = new Lazy<ICrmEntityIndexer[]>(GetIndexersForSavedQueries, LazyThreadSafetyMode.None);
		}

		protected string SavedQueryName { get; private set; }

		protected ICrmEntityIndex Index { get; private set; }

		protected IEnumerable<ICrmEntityIndexer> Indexers
		{
			get { return _indexers.Value; }
		}

		public IEnumerable<CrmEntityIndexDocument> GetDocuments()
		{
            ADXTrace.Instance.TraceInfo(TraceCategory.Application, string.Format("Start: {0}", SavedQueryName));

			var documents = Indexers.SelectMany(indexer => indexer.GetDocuments());

            ADXTrace.Instance.TraceInfo(TraceCategory.Application, string.Format("End: {0}", SavedQueryName));

			return documents;
		}

		public bool Indexes(string entityLogicalName)
		{
			return Indexers.Any(indexer => indexer.Indexes(entityLogicalName));
		}

		protected virtual ICrmEntityIndexer GetIndexerForSavedQuery(Entity query)
		{
			var savedQuery = new SavedQuery(query);

			return new FetchXmlIndexer(Index, savedQuery.FetchXml, savedQuery.TitleAttributeLogicalName);
		}

		/// <remarks>
		/// Power Pages' enhanced data model ships its own "Portal Search" views on mspp_* virtual tables, which this
		/// portal doesn't serve and which lack attributes such as modifiedon that indexing requires. These views are
		/// excluded by the table named in their FetchXML, such as &lt;entity name="mspp_webpage"&gt;.
		/// </remarks>
		protected virtual IQueryable<Entity> GetSavedQueries(OrganizationServiceContext dataContext)
		{
			return dataContext.CreateQuery("savedquery")
				.Where(e => e.GetAttributeValue<string>("name") == SavedQueryName
					&& e.GetAttributeValue<int?>("statecode") == 0
					&& !e.GetAttributeValue<string>("fetchxml").Contains("<entity name=\"mspp_"));
		}

		private ICrmEntityIndexer[] GetIndexersForSavedQueries()
		{
			return GetSavedQueries(Index.DataContext).Select(GetIndexerForSavedQuery).ToArray();
		}
	}
}
