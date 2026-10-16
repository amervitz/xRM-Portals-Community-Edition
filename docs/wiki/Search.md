# Search

The portal has its own full-text search, built on **Lucene.Net 3.0.3** with faceting via
**BoboBrowse.Net**. Records from Dataverse are indexed into a local Lucene index and queried by the portal
search page and the faceted search UI.

Search is independent of Dataverse search — it indexes whatever you tell it to index, using Dataverse
views and FetchXML.

## Enabling search

| Setting | Where | Effect |
|---|---|---|
| `Search/Enabled` | Site setting | Turns the portal search provider on for the website. When it is disabled the search provider throws, and the search UI returns nothing. |
| `Search/IndexQueryName` | Site setting | Names the query used to build the index. |
| `Search/RecordTypeFacetsEntities` | Site setting | Which entities contribute record-type facets. |
| `EncryptIndex` | `Web.config` app setting | Whether the Lucene index is encrypted. |
| `Adxstudio.Xrm.Search.Index.DirectoryPath` | Site setting | Where the index directory lives, if not the default. |
| `<adxstudio.xrm><search>` | `Web.config` | `defaultProvider` (required), `enabled`, and the `providers` collection. |

The index directory defaults to `App_Data/Adxstudio.Xrm.Search` under the web application, which is inside
the repository sample as a pre-seeded index. **That directory must be writable** by the application pool
identity.

## How indexing works

The search subsystem lives in `Framework/Adxstudio.Xrm/Search`. The pieces:

| Component | Role |
|---|---|
| `SearchManager` | Coordinates index building and updates. |
| `ICrmEntityIndex`, `WebCrmEntityIndex`, `PortalIndex` | The Lucene index abstraction and its web implementation. |
| `ICrmEntityIndexBuilder` / `CrmEntityIndexBuilder` | Builds an index from a configuration. |
| `ICrmEntityIndexUpdater` / `EventedIndexUpdater` | Applies incremental updates. |
| `ICrmEntityIndexer` | The indexing contract, implemented per source. |
| `FetchXmlIndexer`, `FetchXmlIndexDocumentFactory` | Indexes records from FetchXML queries. |
| `SavedQueryIndexer`, `UserQueryIndexer`, `SingleEntitySavedQueryIndexer`, `SingleEntityUserQueryIndexer`, `EntitySetSavedQueryIndexer`, `EntitySetUserQueryIndexer` | Indexes from Dataverse saved views (`savedquery`) and personal views (`userquery`). |
| `CrmEntityIndexDocument` | One indexed document, with its fields. |
| `ContentFieldBuilder` | Builds the indexed content fields. |
| `CmsIndexHelper` | Helpers specific to CMS content. |
| `CrmEntityIndexInfo` | Describes the index. |

An index configuration selects a **Dataverse view** and the **columns** to index per document. The
indexer runs the view's FetchXML, projects the columns into a Lucene document, and stores it.

### Provider and query side

| Component | Role |
|---|---|
| `PortalSearchProvider`, `SearchProvider` | The website's search provider. |
| `PortalIndexSearcher` / `ICrmEntityIndexSearcher` | Query execution. |
| `SharedIndexSearcher` / `SharedIndexSearcherCache` / `SingleUseIndexSearcherPool` / `IIndexSearcherPool` | Searcher pooling — reuse instead of reopening the index per request. |
| `CrmEntityQuery`, `ScopedEntityQuery` | Query construction, including scoping by entity type and permission. |
| `CrmEntitySearchResult`, `CrmEntitySearchResultPage`, `CrmEntitySearchResultFactory` | Results and paging. |
| `SimpleHtmlHighlightedFragmentProvider`, `EmptyFragmentProvider` | Result snippets with highlighted terms. |
| `ExtendedAttributeIndexSearcher`, `ExtendedAttributeSearchProvider` | Searching on additional attributes. |
| `FSDirectorySearchProvider` | Filesystem-directory-based provider. |
| `SearchDisabledProviderException`, `IndexNotFoundException`, `SearchEventSource` | Failure modes and logging. |

### Facets

Faceted search (the `Areas/Search` UI, `js/faceted-search.js`, `Facets/*`) adds filtering by facet:

| Component | Role |
|---|---|
| `PortalFacetedIndexSearcher` | Facet-aware searching. |
| `FacetConfiguration`, `FacetConstraints`, `FacetView`, `ConstraintHit` | Facet definition and current selection. |
| `FixedFacetsConfiguration` | Facets whose available values are fixed. |
| `FacetValueDescComparatorFactory` | Result ordering. |
| `ModifiedOnDateRange` | Date-range faceting. |
| `IFacetedSearchConfigurationProvider` | Supplies the configuration. |

Facet option lists are exposed to Liquid through the `search_filter_options` filter
(`SearchFilterOptionFilters`).

## The search index in environments with the enhanced data model

Building the index used to fail in environments that also have the Power Pages **enhanced data model**.
The `mspp_*` virtual tables have their own "Portal Search" views; the indexer picked those up and then
requested `modifiedon` from them, which threw, because virtual tables such as `mspp_webpage` do not have
that column.

**Views on `mspp_*` tables are now skipped.** If index rebuilds were failing in your environment, this
fixes them — and if you had hidden those views as a workaround, you can revert that.

## Rendering search

- **`{% searchindex %}`** renders search results, driven by the `searchindex` tag.
- **`Areas/Search`** provides the faceted search pages and scripts (`faceted-search.js`,
  `facet.css`, `search-content.css`).
- **`Pages/Search.aspx`** is the built-in search page.
- The shipped `search.liquid` embedded template shows a complete search template.

## Rebuilding the index

Rebuild when:

- you change which views/columns are indexed,
- the configuration (`Search/IndexQueryName`, faceting entity list) changes,
- the index is corrupt or the directory was deleted,
- content changed while search was disabled.

Rebuilding is an administrative operation performed through the portal's search administration path. In a
**multi-instance** deployment, remember that the index is a **per-instance filesystem artifact** unless
you place it on shared storage — each instance needs its own index, or you must design around it. See
[Deployment](Deployment.md#scale-out-considerations).

Index updates can also be driven by **Event Hub invalidation** (`EventHubBasedInvalidation`,
`SearchSubscription`) and by `EventedIndexBuilder` / `EventedIndexUpdater`, so content changes propagate
to the index without a manual rebuild.

## Troubleshooting

| Symptom | Likely cause |
|---|---|
| Search returns nothing at all | `Search/Enabled` is false, or the index directory is missing/unwritable. |
| Results are stale | Index not rebuilt, or invalidation not reaching this instance. |
| Index build fails | Check the trace; in enhanced-data-model environments this used to be the `mspp_*` view problem (now fixed). |
| Search box renders but no query runs | The search provider is not configured for the website, or the enabled search element is missing from `adxstudio.xrm`. |
| Facets are empty | `Search/RecordTypeFacetsEntities` and the faceted-search configuration. |
| Works locally, not in production | `App_Data` is not writable in production, or instances have separate indexes. |

## Related pages

- [Configuration Reference](Configuration-Reference.md#adxstudioxrm-section) — the `search` element
- [Site Settings Reference](Site-Settings-Reference.md#search)
- [Deployment](Deployment.md) — writable paths and scale-out
- [Liquid Tags and Filters Reference](Liquid-Tags-and-Filters-Reference.md#searchindex)
- [Troubleshooting](Troubleshooting.md#search-returns-no-results)
