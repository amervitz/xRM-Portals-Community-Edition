# Framework Namespaces

A guided tour of the four framework assemblies, so you can find code fast. File counts are approximate and
give a sense of where the weight sits.

```
Framework/
  Adxstudio.Xrm/                  the portal engine (largest)
  Microsoft.Xrm.Portal/           portal context, CMS access, routing primitives
  Microsoft.Xrm.Client/           Dataverse client, caching, configuration
  SafeHtml/                       HTML sanitizer
  Adxstudio.Xrm.Resources/        localized UI strings
  Adxstudio.Xrm.Files/            embedded xrm-adx resources
  Microsoft.Xrm.Portal.Files/     embedded xrm resources
```

---

## `Adxstudio.Xrm`

### `Web` (~511 files) — the biggest namespace by far

| Sub-area | Contents |
|---|---|
| `Web/Mvc/Liquid` | Every Liquid drop (`*Drop.cs`), the registered filters, the template file systems (`EntityFileSystem`, `LocalFileSystem`, `CompositeFileSystem`, `EmbeddedResourceFileSystem`), and `PortalLiquidContext`. |
| `Web/Mvc/Liquid/Tags` | The custom tags: `Editable`, `EntityList`, `EntityForm`, `WebForm`, `EntityView`, `FetchXml`, `OutputCache`, `SearchIndex`, `Chart`, `Redirect`, `Rating`, `Substitution`. |
| `Web/Mvc/Html` | `LiquidExtensions` (the Liquid entry point) and hundreds of HTML helpers used by pages and controls. |
| `Web/Mvc/Controllers` | `CmsTemplateController` (template metadata, preview, and live preview) and other MVC controllers. |
| `Web/Handlers` | `WebNotificationHandler`, `DiagnosticsHandler`, `CacheFeedHandler`, `AnnotationHandler`, `CloudBlobRedirectHandler`, `WebResourceHandler`, `AboutProductHandler`, the CMS entity handlers, and the ElFinder file manager. |
| `Web/Modules` | `PortalRoutingModule`, `OutputCacheModule`, `UpdateCookieModule`, `ErrorNotifierModule`. |
| `Web/Routing` | Route handlers for CMS entities, attributes, relationships, children, file attachments, URLs, web resources, and the cache-plugin message. |
| `Web/UI/WebControls` | The control library: entity grids and list views (`CrmEntityListView`), `CrmEntityFormView`, `EntityForm`, `WebForm`, `Snippet`, `Property`, `Captcha`, `TagCloud`, `LiquidServerControl`. |
| `Web/UI/Content` | Configuration objects for grids, actions, filters and modals (JSON-configured). |
| `Web/Providers` | `ContentMapEntityWebsiteProvider` and other provider implementations. |
| `Web/Security` | `CrmContactAccountRoleProvider`, `CrmContactProfileProvider`, and security trimming. |
| Site-map providers (`Web/*SiteMapProvider.cs`) | `ContentMapCrmSiteMapProvider`, `ForumSiteMapProvider`, `EventSiteMapProvider`, `BlogSiteMapProvider`, plus handlers and display modes. |

**Start here if you are extending rendering.** The `Web/Mvc/Liquid` folder is where "how does Liquid see
this?" is answered, and `Web/UI/WebControls` is where "how is this rendered in ASP.NET?" is answered.

### Feature namespaces

| Namespace | Files | Contains |
|---|---|---|
| `Cms` | 109 | Content map and content model: `ContentMap`, `ContentMapProvider`, `Ad`, `AdPlacement`, `Poll`, `Rating`, `Snippet`, `WebLink`, `SiteMarker`, `Author`, `CommentDataAdapterFactory`, `PageCommentPolicy`, publishing state access, site/entity data adapters, `CmsSolutionDefinitionProvider`. |
| `Forums` | 59 | `Forum`, `ForumDataAdapter`, forum threads/posts, `ForumCommentPolicy`, access permissions, aggregations, notification/alert adapters, thread tagging. |
| `Blogs` | 50 | `Blog`, `BlogPost`, `BlogPostComment`, `BlogDataAdapter`, feed handlers and route handlers, archive generation, comment policy. |
| `Ideas` | 47 | `IdeaForum`, `Idea`, `IdeaVote`, `IdeaComment`, data adapters, aggregations (by hot, by new), comment and submission policy. |
| `Services` | 42 | The organization-service wrappers and query helpers: `CachedOrganizationService`, `CompositeOrganizationServiceCache`, `OrganizationServiceCache`, `ContentMapOrganizationServiceCache`, `PortalBusOrganizationServiceCache`, `EventCachedOrganizationService`, `CrmOnlineOrganizationService`, `CrmTokenManager`, `RetrieveSingleRequest`/`FetchMultipleRequest`, and `Services/Query` (the `Fetch`/`Condition`/`Filter` object model). |
| `Commerce` | 41 | Cart, purchasables, orders and quotes, discounts and price lists, payment handlers. See [Commerce](Commerce.md). |
| `Products` | 35 | Products, brands, pricing, product images, reviews, sales attachments. |
| `EventHubBasedInvalidation` | 28 | Event Hub subscriptions, change tracking (`CrmChangeTrackingManager`, `EntityTrackingInfo`, `TimeBasedChangedData`), notification transformation, cache invalidation jobs. |
| `Issues` | 25 | Issue forums, issues, votes, comments, comment/submission policy, factory and aggregations. |
| `Events` | 24 | `Event`, `EventSchedule`, `EventOccurrence`, `EventSpeaker`, `EventSponsor`, calendar and route handlers, recurrence handling. |
| `KnowledgeArticles` | 20 | `KnowledgeArticle`, data and aggregation adapters, related articles/notes/products, view and rating tracking. |
| `Notes` | 18 | Annotations: `AnnotationDataAdapter`, `AnnotationFile`, `CrmAnnotationFile`, `AzureAnnotationFile`, `AnnotationSettings`. |
| `SharePoint` | 17 | `SharePointDataAdapter`, `SharePointConnection`, `ClientFactory`, items/collections/results, and `SharePoint/Configuration`. |
| `Configuration` | 17 | The `adxstudio.xrm` configuration section: `CrmSection`, `AdxstudioCrmConfigurationManager`, `AdxstudioCrmConfigurationProvider`, `ContentMapElement`, `CacheFeedElement`, `OrganizationServiceEventElement`, `X509FindCondition`, `CertificateExtensions`. |
| `Cases` | 17 | `Case`, `CaseAccess`, permission scopes, resolutions, data adapters. |
| `Core` | 16 | Core data adapter, metadata helpers, and `Core/Telemetry` (`PortalFeatureTrace`, `TelemetryState`). |
| `Security` | 13 | Entity permission evaluation (`CrmEntityPermissionProvider`, `EntityPermission`, `CrmEntityPermissionRight`), caching security providers, `WebNotificationCryptography`, `ContentAccessProvider`. |
| `Performance` | 13 | `PerformanceProfiler`, markers, `EventSourcePerformanceLogger`. |
| `Activity` | 12 | Activities, activity collections, timeline and portal comments, MIME attachments. |
| `Category` | 11 | Categories, child/related articles, aggregation and data adapters. |
| `Diagnostics` | 10 | Diagnostics support (tracing, probes). |
| `Caching` | 10 | `ObjectCacheOutputCacheProvider`, `OutputObjectCache`, `CacheInvalidation`, `OrganizationServiceCacheService` and its behavior/instance provider. |
| `Visualizations` | 9 | Charts: `CrmChart`, `CrmChartBuilder`, `CrmView`, measures and dimensions. |
| `Json` | 8 | JSON handling that understands Dataverse types: `CrmJsonConverter`, `CrmJsonConvert`, `JsonEntityFilters`, `JsonCondition`. |
| `EntityList` | 7 | `EntityListDataAdapter`, `EntityListCalendarDataAdapter`, `EntityListPackageRepositoryDataAdapter`, `EntityListFunctions`, `PackageDataAdapter`. |
| `Data` | 6 | `PaginatedList`, `ObjectShredder`, enumerable extensions. |
| `Owin` | 6 | Request telemetry middleware, and `Owin/Security/Saml2` (the SAML 2.0 handler, middleware, options and configuration manager). |
| `Commerce`/`Conferences` | 2 | `ConferenceEventDataAdapter`. |
| `ContentAccess` | 4 | `ContentAccessLevelProvider`, `CategoryAccessProvider`, `ProductAccessProvider`, `ContentAccessConfiguration`. |
| `EntityForm` | 4 | `EntityFormFunctions`, `IEntityForm`, `EntityFormObject`. |
| `Feedback` | 4 | Comments and feedback policies. |
| `Globalization` | 3 | `DateTimeFormatInfo`, `LocaleIds`, `LabelExtensions`. |
| `IdentityModel` | 3 | Federation-related helpers under `IdentityModel`. |
| `Mapping` | 3 | Geolocation, `GeoHelpers`, mapping field metadata. |
| `Marketing` | 8 | `MarketingDataAdapter`, `MarketingList`, encryption key handling (`EncryptionKeyMissingException`, `InvalidSignatureException`). |
| `Metadata` | 2 | `MoneyFormatter`, metadata query helpers. |
| `Net` | 2 | `BingMapLookup`, `BingLocationResponse` — map lookup integration. |
| `Partner` | 3 | Partner opportunity history and enums. |
| `ServiceModel` | 2 | `ServiceHostContext`, `Namespaces`. |
| `Tagging` | 7 | `TagInfo`, `ITaggable`, `TagCloudData`, tag name comparison. |
| `Text` | 4 | `FileSize`, `Mask`, `SimpleHtmlFormatter`, `StringHelper`. |
| `Threading` | 4 | `FluentSchedulerJob`, `HttpSingleton`, `MonitorLockProvider`, `NamedLock`. |
| `Account` | 1 | Account query helpers. |
| `Collections` | 10 | Collection infrastructure (`Collections/Json` helpers). |
| `Decorators` | 7 | `HttpContextDecorator`, `CookieInspector`, `HeaderDecorator`, `ItemDecorator`, `ICollectionInspector`. |
| `AspNet` (50) | | ASP.NET Identity integration, CMS website management, portal bus, localization. See below. |
| `IPortalLiquidContext` / `PortalLiquidContext` | | The Liquid context passed to drops. |
| `Liquid` (resource folder) | | Not C# — the embedded `*.liquid` / `*.json` content templates (ad, blogs, breadcrumbs, layouts, entity list grid, polls, search, side/top navigation, …). |

### `AspNet`

| Sub-area | Contents |
|---|---|
| `AspNet` (root) | `BaseManager`, `BaseStore`, `CrmDbContext`, `CrmEntityStore`, `CrmModel`, `EntityManager`, `Extensions`. |
| `AspNet/Identity` | `CrmUser`, `CrmUserStore`, `CrmUserManager`, `CrmSignInManager`, `CrmUserLogin`, `CrmClaimsIdentityFactory`, `CrmInvitationStore`, `InvitationManager`, `CrmPasswordValidator`, `CrmEmailTokenProvider`, `CrmPhoneNumberTokenProvider`, `IdentityErrorDescriber`. |
| `AspNet/Cms` | `CrmWebsite`, `CrmWebsiteStore`, `WebsiteManager`, `ContextLanguageInfo`, `PortalSolutions`, `WebAppSettings`, `PortalHostingEnvironment`, and the middleware: `AppInfoMiddleware`, `HealthMiddleware`, `RequireSslMiddleware`, `ScaleOutMiddleware`, `ETWMiddleware`, `StrictTransportSecurityMiddleware`, `WebsiteHeaderSettingsMiddleware`, `SiteMapAuthenticationMiddleWare`, `PluginMessageMiddleware`, `ApplicationRestartPluginMessageMiddleware`. |
| `AspNet/PortalBus` | Portal bus messages and subscription plumbing. |
| `AspNet/Mvc`, `AspNet/Organization`, `AspNet/Extensions` | MVC integration, organization managers, extension methods (including `UsePortalsAuthentication`). |

---

## `Microsoft.Xrm.Portal`

The older portal layer, still central to routing and the site map:

| Sub-area | Contents |
|---|---|
| `Cms` | `CrmSiteMapNode`, `EntityNode`, entity URLs, `PageTemplateNode`, website providers, content maps. |
| `Web/Compilation` | The `CrmSiteMap` and `Context` expression builders. |
| `Web/UI/WebControls` | Legacy portal controls registered under the `crm` tag prefix. |
| `Web/Routing`, `Web/Providers` | Portal route handlers and providers. |
| `Configuration` | `PortalCrmSection`, `PortalContextElement`, `DependencyProviderElement`, `PortalCrmConfigurationManager`. |
| `IdentityModel` | Federation configuration. |
| `Access`, `Core`, `Data`, `Reflection`, `Runtime`, `ServiceModel`, `Threading` | Supporting infrastructure. |

`Microsoft.Xrm.Portal.Configuration.DependencyProvider` is the base class your dependency provider derives
from — see [Extensibility](Extensibility.md).

---

## `Microsoft.Xrm.Client`

| Sub-area | Contents |
|---|---|
| `Configuration` | `CrmSection` (`microsoft.xrm.client`), `CrmConfigurationManager`, contexts/services/caches/object-cache elements. |
| `Services` | `CrmOrganizationServiceContext`, the organization service implementations. |
| `Caching` | Client-side caching primitives. |
| `Security` | `ICrmEntitySecurityProvider` and implementations. |
| `Collections`, `Diagnostics`, `Messages`, `Metadata`, `Reflection`, `Runtime`, `Threading`, `Windows` | Supporting infrastructure. |

This is the layer that reads `microsoft.xrm.client` from `Web.config` and constructs the service stack.
See [CRM Connection](CRM-Connection.md).

---

## `SafeHtml`

`SafeHtml.SafeHtmSanitizer.GetSafeHtml` — the HTML sanitizer the portal uses for user-generated content
(forum posts, comments, ideas, issues). Do not replace it with ad-hoc regex sanitization.

---

## `Adxstudio.Xrm.Resources`

`ResourceManager` plus `ResourceFiles/strings.*.resx` for ~40 languages. Exposed to Liquid as `resx` and to
ASP.NET via `ResourceManagerExpressionBuilder`.

---

## `Adxstudio.Xrm.Files` and `Microsoft.Xrm.Portal.Files`

Embedded file resources served from the reserved virtual paths:

- `~/xrm-adx` → `Adxstudio.Xrm.Files`
- `~/xrm` → `Microsoft.Xrm.Portal.Files`

`Adxstudio.Xrm.Files` carries the portal's shipped `xrm-adx` assets; `Microsoft.Xrm.Portal.Files` carries
the legacy `xrm` assets (including the front-side editing scripts under `xrm/js/editable`).

---

## Finding code fast: a recipe

1. **Something renders wrong** → `Web/Mvc/Liquid` (Liquid) or `Web/UI/WebControls` (ASP.NET).
2. **A tag or filter misbehaves** → `Web/Mvc/Liquid/Tags` or the filter classes in `Web/Mvc/Liquid`.
3. **Data is wrong or stale** → `Services` (caching) and `Caching`.
4. **A permission decision is wrong** → `Security` and `Cms/Security`.
5. **The URL is wrong or 404s** → `Microsoft.Xrm.Portal/Cms` and `Web/Routing`.
6. **Start-up fails** → `AspNet/Cms` middleware and `Configuration`.
7. **Search is wrong** → `Search`.
8. **A Dataverse operation fails** → `Services`, then `Microsoft.Xrm.Client`.

## Related pages

- [Architecture](Architecture.md)
- [Extensibility](Extensibility.md)
- [Caching and Performance](Caching-and-Performance.md)
- [Diagnostics and Logging](Diagnostics-and-Logging.md)
