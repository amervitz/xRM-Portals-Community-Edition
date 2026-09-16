/*
  Copyright (c) Microsoft Corporation. All rights reserved.
  Licensed under the MIT License. See License.txt in the project root for license information.
*/

namespace Adxstudio.Xrm.Search
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Web;
	using Adxstudio.Xrm.Cms;
    using Adxstudio.Xrm.Search.Facets;
    using Adxstudio.Xrm.Search.Index;
    using Lucene.Net.Index;
    using Lucene.Net.QueryParsers;
    using Lucene.Net.Search;
    using Lucene.Net.Documents;
    using Microsoft.Xrm.Client;
    using Microsoft.Xrm.Sdk;
    using Microsoft.Xrm.Portal.Configuration;

    public class CrmEntityIndexSearcher : ICrmEntityIndexSearcher, IRawLuceneIndexSearcher
	{
		private const int InitialSearchLimitMultiple = 1;
		private const int ExtendedSearchLimitMultiple = 2;

		private readonly ICrmEntityIndex _index;
		private readonly IndexSearcher _searcher;

		public CrmEntityIndexSearcher(ICrmEntityIndex index)
		{
			if (index == null)
			{
				throw new ArgumentNullException("index");
			}

			_index = index;

			if (!IndexReader.IndexExists(_index.Directory))
			{
				throw new IndexNotFoundException("Search index not found in {0}. Ensure index is constructed before attempting to search.".FormatWith(_index.Directory));
			}

			var stopwatch = new Stopwatch();
			stopwatch.Start();

			_searcher = new IndexSearcher(_index.Directory, true);

			stopwatch.Stop();

			ADXTrace.Instance.TraceInfo(TraceCategory.Application, string.Format("Lucene IndexSearcher initialized ({0}ms)", stopwatch.ElapsedMilliseconds));
		}

        protected ICrmEntityIndex Index
		{
			get { return _index; }
		}

		protected IndexSearcher Searcher
		{
			get { return _searcher; }
		}

		public virtual void Dispose()
		{
			_searcher.Dispose();
		}

		public virtual ICrmEntitySearchResultPage Search(ICrmEntityQuery query)
		{
            ADXTrace.Instance.TraceInfo(TraceCategory.Application, string.Format(@"query=(PageNumber={0},PageSize={1},LogicalNames=({2}))", query.PageNumber, query.PageSize, string.Join(",", query.LogicalNames.ToArray())));

			var pageNumber = query.PageNumber;

			if (pageNumber < 1)
			{
				ADXTrace.Instance.TraceInfo(TraceCategory.Application, "Page number cannot be less than 1. Forcing PageNumber to 1.");

				pageNumber = 1;
			}

			var pageSize = query.PageSize;

            if (pageSize < 1)
			{
				ADXTrace.Instance.TraceInfo(TraceCategory.Application, "Page size cannot be less than 1. Forcing PageSize to 1.");

				pageSize = 1;
			}

			var luceneQuery = CreateQuery(query);

			var resultFactory = Index.GetSearchResultFactory(luceneQuery);

            var results = new List<ICrmEntitySearchResult>();

			// We add a +1 to the searchLimit and resultLimit so as to try and go one result beyond the requested result page, so that
			// approximateTotalHits will reflect whether there is at least one further valid/readable result beyond the current page.
			// This eliminates the edge case where a user gets a full page of results, the total hits indicates there are more results,
			// but there actually aren't any, leading to a blank final page of results.
			var userResults = GetUserSearchResults(
				query,
				((pageSize * pageNumber) * InitialSearchLimitMultiple) + 1,
				0,
				(pageSize * pageNumber) + 1,
				resultFactory,
                pageNumber,
                pageSize,
				results);

			return userResults;
		}

		public IEnumerable<EntityReference> Search(Query query, int maxSearchResults)
		{
			var entities = new List<EntityReference>();

			var topDocs = this._searcher.Search(query, maxSearchResults);

			if (topDocs.ScoreDocs.Length < 1)
			{
				return entities;
			}

			foreach (var indexDoc in topDocs.ScoreDocs)
			{
				var document = this._searcher.Doc(indexDoc.Doc);
				var entity = new EntityReference();

				var primaryFieldName = document.GetField("_logicalname");
				var primaryKeyField = document.GetField("_primarykey");

				entity.Id = Guid.Parse(primaryKeyField.StringValue);
				entity.LogicalName = primaryFieldName.StringValue;

				entities.Add(entity);
			}
			return entities;
		}

        protected virtual Query CreateQuery(ICrmEntityQuery query)
		{
			var booleanQuery = new BooleanQuery();
			var queryParser = new QueryParser(Index.Version, Index.ContentFieldName, Index.GetQuerySpecificAnalyzer(query.MultiLanguageEnabled, query.ContextLanguage));

			// The QueryText field is intended to be a user-submitted query, so we want to be forgiving in how
			// we parse it. If the query parser fails to parse the value, escape it and parse it again.
			if (!string.IsNullOrWhiteSpace(query.QueryText))
			{
				Query textQuery;

				try
				{
					textQuery = queryParser.Parse(query.QueryText);
				}
				catch (ParseException)
				{
					textQuery = queryParser.Parse(QueryParser.Escape(query.QueryText));
				}

				booleanQuery.Add(textQuery, Occur.MUST);
			}

			// The Filter field, on the other hand, is intended to be a provided by a developer/admin, and will
			// therefore be parsed strictly. If this query is invalid, a ParseException will be thrown.
			if (!string.IsNullOrWhiteSpace(query.Filter))
			{
				booleanQuery.Add(queryParser.Parse(query.Filter), Occur.MUST);
			}

			// If there is no user query text or filter, return no results. (A BooleanQuery with no clauses does this.)
			if (!booleanQuery.Any())
			{
				return new BooleanQuery();
			}

			if (query.LogicalNames.Any())
			{
				var logicalNameQuery = new BooleanQuery();

				foreach (var logicalName in query.LogicalNames)
				{
					if (string.IsNullOrEmpty(logicalName))
					{
						continue;
					}

					logicalNameQuery.Add(new TermQuery(new Term(Index.LogicalNameFieldName, logicalName)), Occur.SHOULD);
				}

				booleanQuery.Add(logicalNameQuery, Occur.MUST);
			}

			this.AddLanguageRestrictionToQuery(query, booleanQuery);

			return booleanQuery;
		}

		/// <summary>
		/// Adds language restriction to search query, if possible
		/// </summary>
		/// <param name="query">Entity query</param>
		/// <param name="booleanQuery">Search query</param>
		private void AddLanguageRestrictionToQuery(ICrmEntityQuery query, BooleanQuery booleanQuery)
		{
			if (string.IsNullOrEmpty(this.Index.LanguageLocaleCodeFieldName))
			{
				return;
			}

			// Multilanguage enabled, language code provided in query
			if (query.MultiLanguageEnabled && !string.IsNullOrEmpty(query.ContextLanguage.Code))
			{
				var languageQuery = new BooleanQuery();
				languageQuery.Add(new TermQuery(new Term(this.Index.LanguageLocaleCodeFieldName, query.ContextLanguage.Code.ToLowerInvariant())), Occur.SHOULD);
				languageQuery.Add(new TermQuery(new Term(this.Index.LanguageLocaleCodeFieldName, this.Index.LanguageLocaleCodeDefaultValue)), Occur.SHOULD);

				booleanQuery.Add(languageQuery, Occur.MUST);
			}
			//// Multilanguage disabled, KB language present in settings
			else if (!query.MultiLanguageEnabled && !string.IsNullOrWhiteSpace(this.Index.LanguageLocaleCode))
			{
				var languageQuery = new BooleanQuery();
				languageQuery.Add(new TermQuery(new Term(this.Index.LanguageLocaleCodeFieldName, this.Index.LanguageLocaleCode)), Occur.SHOULD);
				languageQuery.Add(new TermQuery(new Term(this.Index.LanguageLocaleCodeFieldName, this.Index.LanguageLocaleCodeDefaultValue)), Occur.SHOULD);

				booleanQuery.Add(languageQuery, Occur.MUST);
			}
			//// Otherwise, do not restrict the language
		}

        /// <summary>
        /// Gets the unprocessed results for a search query.
        /// </summary>
        /// <param name="query">The query</param>
        /// <param name="searchLimit">The max number of results we want</param>
        /// <param name="offset">The number of already processed results to skip.</param>
        /// <returns>The results of the search unfiltered for the user.</returns>
        protected virtual RawSearchResultSet GetRawSearchResults(ICrmEntityQuery query, int searchLimit, int rawOffset)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            var luceneQuery = CreateQuery(query);
            TopDocs topDocs;

            try
            {
                topDocs = _searcher.Search(luceneQuery, searchLimit);
            }
            catch (Exception e)
            {
                SearchEventSource.Log.QueryError(e);
                throw;
            }

            stopwatch.Stop();

			ADXTrace.Instance.TraceInfo(TraceCategory.Application, string.Format("Lucene: {0} total hits ({1}ms)", topDocs.TotalHits, stopwatch.ElapsedMilliseconds));

            PortalFeatureTrace.TraceInstance.LogSearch(FeatureTraceCategory.Search, topDocs.TotalHits, stopwatch.ElapsedMilliseconds, string.Format("Lucene: {0} total hits ({1}ms)", topDocs.TotalHits, stopwatch.ElapsedMilliseconds));

            return ConvertTopDocsToRawSearchResultSet(topDocs, rawOffset);
        }

        /// <summary>
        /// Gets the unprocessed results for a search query.
        /// </summary>
        /// <param name="query">The query</param>
        /// <param name="searchLimit">The max number of results we want</param>
        /// <param name="offset">The number of already processed results to skip.</param>
        /// <returns>The results of the search unfiltered for the user.</returns>
        protected virtual RawSearchResultSet GetRawSearchResults(Query query, int searchLimit, int offset)
        {
            return null;
        }

	    /// <summary>
		/// Creates a result page based off the processed search results.
		/// </summary>
		/// <param name="results">Search results to display to user.</param>
		/// <param name="approximateTotalHits">Estimate of the number of results.</param>
		/// <param name="pageNumber">The page number.</param>
		/// <param name="pageSize">The Page size.</param>
		/// <param name="rawSearchResultSet">The raw search result set, which contains the raw results and other search related info.</param>
		/// <returns>The result page to return to the user.</returns>
		protected virtual ICrmEntitySearchResultPage GenerateResultPage(ICollection<ICrmEntitySearchResult> results, int approximateTotalHits, int pageNumber, int pageSize, RawSearchResultSet rawSearchResultSet)
        {
            var resultOffset = (pageNumber - 1) * pageSize;

            var pageResults = results.Skip(resultOffset).Take(pageSize).ToList();

            return new CrmEntitySearchResultPage(pageResults, approximateTotalHits, pageNumber, pageSize);
        }

        /// <summary>
        /// Get the processed search results provided by the query available to the searching user.
        /// </summary>
        /// <param name="query">Search query.</param>
        /// <param name="searchLimit">Number of results to obtain from the underlying search library.</param>
        /// <param name="initialOffset">Number of already processed results to skip.</param>
        /// <param name="resultLimit">Number of results to return in the result page.</param>
        /// <param name="resultFactory">Factory to generate the ICrmEntitySearchResults</param>
        /// <param name="pageNumber">Page number</param>
        /// <param name="pageSize">Page size</param>
        /// <param name="results">Processed results so far.</param>
        /// <returns></returns>
        protected ICrmEntitySearchResultPage GetUserSearchResults(ICrmEntityQuery query, int searchLimit, int initialOffset, int resultLimit, ICrmEntitySearchResultFactory resultFactory, int pageNumber, int pageSize, ICollection<ICrmEntitySearchResult> results)
        {
            ADXTrace.Instance.TraceInfo(TraceCategory.Application, string.Format("(searchLimit={0},rawOffset={1},resultLimit={2})", searchLimit, initialOffset, resultLimit));
            RawSearchResultSet rawSearchResults = GetRawSearchResults(query, searchLimit, initialOffset);
            
            if (initialOffset >= rawSearchResults.TotalHits)
            {
                return GenerateResultPage(results, rawSearchResults.TotalHits, pageNumber, pageSize, rawSearchResults);
            }

            var stopwatch = new Stopwatch();
            stopwatch.Start();
            var groupedNotes = new List<IGrouping<EntityReference, ICrmEntitySearchResult>>();
            var displayNotes = IsAnnotationSearchEnabled();

            if (displayNotes && !string.IsNullOrEmpty(query.QueryTerm))
            {
                var rawNotes = this.GetRelatedAnnotations(rawSearchResults, query);

                var notes =
                    rawNotes.Select(document => resultFactory.GetResult(document, 1, results.Count + 1)).ToList();

                //Grouping Notes by related Knowledge Articles
                groupedNotes =
                    notes.Where(note => note.EntityLogicalName == "annotation")
                        .GroupBy(note => note.Entity.GetAttributeValue<EntityReference>("objectid"))
                        .ToList();
            }

            var offsetForNextIteration = initialOffset;

            foreach (var scoreDoc in rawSearchResults.Results)
            {
                offsetForNextIteration++;

                var result = resultFactory.GetResult(_searcher.Doc(scoreDoc.Doc), scoreDoc.Score, results.Count + 1);

                // Not a valid user result, filter out
                if (result == null)
                {
                    continue;
                }

                if (result.EntityLogicalName == "knowledgearticle" && displayNotes)
                {
                    var relatedNotes = groupedNotes.Where(a => a.Key.Id == result.EntityID).SelectMany(i => i).Take(3).ToList();

                    if (relatedNotes.Any(note => note.Fragment == result.Fragment))
                    {
                        result.Fragment = GetKnowledgeArticleDescription(result);
                    }
                    result.Entity["relatedNotes"] = relatedNotes;
                }

                results.Add(result);

                if (results.Count >= resultLimit)
                {
                    stopwatch.Stop();

					ADXTrace.Instance.TraceInfo(TraceCategory.Application, string.Format("Gathered {0} results, done ({1}ms)", results.Count, stopwatch.ElapsedMilliseconds));

                    PortalFeatureTrace.TraceInstance.LogSearch(FeatureTraceCategory.Search, results.Count, stopwatch.ElapsedMilliseconds, string.Format("Gathered {0} results, done ({1}ms)", results.Count, stopwatch.ElapsedMilliseconds));

                    return GenerateResultPage(results, rawSearchResults.TotalHits, pageNumber, pageSize, rawSearchResults);
                }
            }

            stopwatch.Stop();

            // We asked for more hits than we got back from Lucene, and we still didn't gather enough valid
            // results. That's all we're going to get, so the number of results we got is the number of hits.
            if (searchLimit >= rawSearchResults.TotalHits)
            {
                ADXTrace.Instance.TraceInfo(TraceCategory.Application, string.Format("All available results ({0}) gathered, done ({1}ms)", results.Count, stopwatch.ElapsedMilliseconds));

                PortalFeatureTrace.TraceInstance.LogSearch(FeatureTraceCategory.Search, results.Count, stopwatch.ElapsedMilliseconds, string.Format("All available results ({0}) gathered, done ({1}ms)", results.Count, stopwatch.ElapsedMilliseconds));

                return GenerateResultPage(results, results.Count, pageNumber, pageSize, rawSearchResults);
            }

			ADXTrace.Instance.TraceInfo(TraceCategory.Application, string.Format("{0} results gathered so far ({1}ms)", results.Count, stopwatch.ElapsedMilliseconds));

            PortalFeatureTrace.TraceInstance.LogSearch(FeatureTraceCategory.Search, results.Count, stopwatch.ElapsedMilliseconds, string.Format("{0} results gathered so far ({1}ms)", results.Count, stopwatch.ElapsedMilliseconds));

            return GetUserSearchResults(query, searchLimit * ExtendedSearchLimitMultiple, offsetForNextIteration, resultLimit, resultFactory, pageNumber, pageSize, results);
        }

        private IEnumerable<Document> GetRelatedAnnotations(RawSearchResultSet rawSearchResults, ICrmEntityQuery query)
        {
            Query textQuery;
            var noteQuery = new BooleanQuery();
            var queryParser = new QueryParser(Index.Version, Index.ContentFieldName, Index.GetQuerySpecificAnalyzer(query.MultiLanguageEnabled, query.ContextLanguage));
            try
            {
                textQuery = queryParser.Parse(string.Format("+({0}) filename:({0}) notetext:({0}) _logicalname:annotation~0.9^0.3", query.QueryTerm));
            }
            catch (ParseException)
            {
                textQuery = queryParser.Parse(QueryParser.Escape(string.Format("+({0}) filename:({0}) notetext:({0}) _logicalname:annotation~0.9^0.3", query.QueryTerm)));
            }

            noteQuery.Add(textQuery, Occur.MUST);
            noteQuery.Add(new TermQuery(new Term("_logicalname", "annotation")), Occur.MUST);
            foreach (var scoreDoc in rawSearchResults.Results)
            {
            var resultField = _searcher.Doc(scoreDoc.Doc).GetField("_logicalname");
                if (resultField != null && resultField.StringValue == "knowledgearticle")
                {
                    var primaryKey = _searcher.Doc(scoreDoc.Doc).GetField("_primarykey");
                    noteQuery.Add(new TermQuery(new Term("annotation_knowledgearticleid", primaryKey.StringValue)), Occur.SHOULD);
                }
            }

            var rawNoteResults = GetRawSearchResults(noteQuery, 30, 0);

            return rawNoteResults.Results.Select(rawNoteResult => _searcher.Doc(rawNoteResult.Doc)).ToList();
        }

        private RawSearchResultSet ConvertTopDocsToRawSearchResultSet(TopDocs topDocs, int rawOffset)
        {
            List<RawSearchResult> rawSearchResults = new List<RawSearchResult>();
            for (int i = rawOffset; i < topDocs.ScoreDocs.Length; i++)
            {
                rawSearchResults.Add(new RawSearchResult(topDocs.ScoreDocs[i].Doc, topDocs.ScoreDocs[i].Score));
            }

            return new RawSearchResultSet(rawSearchResults, topDocs.ScoreDocs.Length);
        }

        private bool IsAnnotationSearchEnabled()
        {
            var portalContext = PortalCrmConfigurationManager.CreatePortalContext();
            var displayNotesEnabledString = portalContext.ServiceContext.GetSiteSettingValueByName(portalContext.Website, "KnowledgeManagement/DisplayNotes");

            bool displayNotesEnabled;
            bool.TryParse(displayNotesEnabledString, out displayNotesEnabled);

            return displayNotesEnabled;
        }

        private string GetKnowledgeArticleDescription(ICrmEntitySearchResult result)
        {
            var description = result.Entity.GetAttributeValue<string>("description");
            if (description != null)
            {
                return description;
            }

            var content = result.Entity.GetAttributeValue<string>("content");
            if (content != null)
            {
                content = content.Length >= 140 ? ContentFieldBuilder.StripContent(content.Substring(0, 140) + "...")
                                                : ContentFieldBuilder.StripContent(content + "...");
            }
            return content;
        }


        /// <summary>
        /// Represents an unprocessed search result.
        /// </summary>
        protected class RawSearchResult
        {
            public int Doc { get; private set; }
            public float Score { get; private set; }
            public List<RawSearchResult> ChildResults { get; set; } 

            public RawSearchResult(int doc, float score)
            {
                this.Doc = doc;
                this.Score = score;
            }
        }

        /// <summary>
        /// Represents a set of unprocessed search results and related data.
        /// </summary>
        protected class RawSearchResultSet 
        {
            public IEnumerable<RawSearchResult> Results { get; private set; }
            public int TotalHits { get; private set; }
            public IEnumerable<FacetView> FacetViews { get; private set; }
            public IEnumerable<string> SortingOptions { get; private set; }

            public RawSearchResultSet(IEnumerable<RawSearchResult> results, int totalHits, IEnumerable<FacetView> facetViews = null, IEnumerable<string> sortingOptions = null)
            {
                this.Results = results;
                this.TotalHits = totalHits;
                this.FacetViews = facetViews;
                this.SortingOptions = sortingOptions;
            }
        }
    }
}
