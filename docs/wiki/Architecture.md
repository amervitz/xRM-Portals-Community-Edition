# Architecture

This page explains how the portal is put together: the projects, the layers, what happens during a
request, and the components that run in the background. Read [Glossary](Glossary.md) first if any term
is unfamiliar.

## Projects

The solution is `Solutions/Portals/Portals.sln` and contains nine projects. Five are class libraries
shipped as assemblies; one is the web application; the rest are supporting projects.

| Project | Type | Role |
|---|---|---|
| **MasterPortal** (`Samples/MasterPortal`) | ASP.NET Web Application | The deployable site. Contains `Web.config`, `Global.asax`, OWIN `Startup`, MVC areas, Web Forms pages, controls, master pages, CSS and JavaScript. |
| **Adxstudio.Xrm** (`Framework/Adxstudio.Xrm`) | Class library | The portal engine. Content management, entity lists/forms, web forms, Liquid, search, commerce, forums/blogs/ideas/issues, security, caching, OWIN middleware, configuration. This is where almost all feature code lives. |
| **Microsoft.Xrm.Portal** (`Framework/Microsoft.Xrm.Portal`) | Class library | Portal context and the older CMS access layer: CrmSiteMap providers, content map nodes, IdentityModel, routing primitives, web controls. |
| **Microsoft.Xrm.Client** (`Framework/Microsoft.Xrm.Client`) | Class library | Dataverse client plumbing: organization service contexts, connection configuration (`microsoft.xrm.client` section), object caches, security providers. |
| **Adxstudio.Xrm.Resources** (`Framework/Adxstudio.Xrm.Resources/Adxstudio.Xrm.Resources`) | Class library | Localized UI strings (`ResourceFiles/strings.*.resx` for ~40 languages) exposed through `ResourceManager`. |
| **SafeHtml** (`Framework/SafeHtml`) | Class library | HTML sanitizer (`SafeHtml.SafeHtmSanitizer`) used for user-generated content such as forum posts. |
| **Adxstudio.Xrm.Files** (`Framework/Adxstudio.Xrm.Files`) | Class library | Embedded file resources served under the reserved `~/xrm-adx` path. |
| **Microsoft.Xrm.Portal.Files** (`Framework/Microsoft.Xrm.Portal.Files`) | Class library | Embedded legacy resources served under the reserved `~/xrm` path. |
| **Framework** (`Framework` solution folder) | Solution folder | Groups the framework projects. |

### Layering

```
                  MasterPortal  (areas, aspx pages, views, controls, config)
                        │
        ┌───────────────┴────────────────┐
        ▼                                ▼
  Adxstudio.Xrm                   Microsoft.Xrm.Portal
  (feature engine, Liquid,         (portal context, CMS,
   search, commerce, security)      sitemap, IdentityModel)
        │                                │
        └───────────────┬────────────────┘
                        ▼
                Microsoft.Xrm.Client
              (organization service, caching,
               connection configuration)
                        │
                        ▼
             Microsoft.Xrm.Sdk  +  Dataverse / Dynamics 365
```

Dependencies flow downward. `Adxstudio.Xrm` is the largest assembly (~511 source files under
`Web/`, plus feature folders) and depends on `Microsoft.Xrm.Client` and `Microsoft.Xrm.Portal`.

## Where features live in `Adxstudio.Xrm`

The assembly is organised by feature area. Folder names are the fastest way to find behaviour:

| Folder | Contains |
|---|---|
| `AspNet` | ASP.NET Identity integration, portal users and roles, website management, portal bus, localization, OWIN helpers. |
| `Caching` | Object caches and the output-cache provider. |
| `Cms` | Content model: content map, web pages, snippets, site markers, web links, ads, polls, ratings, comments, publishing states, solution definition. |
| `Commerce` | Shopping cart, checkout, payment handlers (PayPal, Authorize.Net, demo), discounts. |
| `Configuration` | The `adxstudio.xrm` configuration section and its provider. |
| `ContentAccess` | Content access levels and their providers. |
| `EntityForm` / `EntityList` | Entity form and entity list data adapters. |
| `EventHubBasedInvalidation` | Cache-and-search invalidation driven by Azure Event Hub messages. |
| `Events`, `Conferences` | Events, schedules, speakers, sponsors, registrations. |
| `Forums`, `Blogs`, `Ideas`, `Issues`, `Feedback`, `Tagging`, `KnowledgeArticles` | Community and knowledge features. |
| `Globalization` | Language and culture handling. |
| `Liquid` | Built-in `*.liquid` / `*.json` content templates shipped as embedded files (ad, blogs, breadcrumbs, entity list grid, layouts, polls, search, side/top navigation, …). |
| `Marketing`, `Memberships`, `Partner`, `Products`, `Cases`, `HelpDesk`, `Activity`, `Notes`, `SharePoint`, `Visualizations` | Feature-specific adapters and providers. |
| `Owin` | OWIN middleware and handlers, including SAML 2.0. |
| `Search` | Lucene.Net index, indexers, searchers, facets, sorting, configuration. |
| `Security` | Entity (table) permissions, caching security providers, web notification cryptography. |
| `Services` | The organization service wrappers: caching, evented, failover, token management, query helpers (`Services/Query`). |
| `Web` | MVC controllers, Liquid drops and tags, HTML helpers, handlers, modules, routing, site-map providers, UI web controls (including `CrmEntityFormView`, entity grids, web form controls). |

`Framework/Adxstudio.Xrm/Web/Mvc/Liquid` is the directory to know: nearly every object exposed to
Liquid templates has a `*Drop.cs` file there, and the custom tags live in `Web/Mvc/Liquid/Tags`.

## Application start-up

Start-up happens in two phases.

### 1. `PreApplicationStart` (`Samples/MasterPortal/App_Start/SetupConfig.cs`)

Registered with `[assembly: PreApplicationStartMethod]`, it runs before the application proper:

- If the portal is **not configured** (no `Xrm` connection string and no setup record), it registers the
  **setup** routing module so the installer can run, and registers setup bundles.
- If the portal **is configured**, it registers the normal `PortalRoutingModule`, enables anti-forgery
  token emission without an X-Frame-Options header, and hooks configuration-created events so the
  connection string and website name can be injected at runtime.

`SetupConfig.IsPortalConfigured()` is `!InitialSetupIsRunning() && !ProvisioningInProgress()`; the whole
application branches on it.

### 2. `Application_Start` (`Global.asax.cs`) and OWIN `Startup`

`Global.asax.cs`:

- Sets the anti-forgery cookie name to `__RequestVerificationToken` and disables the MVC response header.
- Calls `SetupConfig.ApplicationStart()`. If setup or provisioning is running, it stops there.
- Otherwise registers area routes, global filters, MVC routes and bundles.
- `Application_PreRequestHandlerExecute` re-applies the website language culture to the current thread,
  because .NET 4.6+ does not preserve culture set inside OWIN middleware.
- `GetVaryByCustomString` implements the output-cache variation keys used by the cache profiles:
  `roles`, `user`, `website`, `culture`, `language` and combinations of them.
- `Session_Start` emits portal telemetry when telemetry is enabled and the portal is configured.
- `Application_Error` logs unhandled exceptions through `WebEventSource`.

`Startup.cs` (`Site.Startup`) configures OWIN in a carefully ordered pipeline:

1. `ConfigureApplicationLifecycleEvents`, `UseETWMiddleware` (server elapsed-time tracking).
2. `UseRequireSsl` (redirects to HTTPS based on website settings).
3. `UseAppInfo`, `UseHealth`, `UseScaleOutTelemetry`, `UseRequestTelemetry`.
4. Portal bus middleware for application-restart and cache-invalidation messages.
5. Per-OWIN-context factories: `ApplicationDbContext`, `ApplicationOrganizationManager`,
   `ApplicationWebsiteManager`, `CrmWebsite`.
6. `UseCurrentThreadCulture`, then website resolution (`websiteManager.Find(...)`).
7. Website-dependent configuration: primary domain name, display modes, website header settings, Strict
   Transport Security, **search provider**, **content map**, then `ConfigureAuth(app, website)`
   (ASP.NET Identity, cookie auth, SAML/OpenID/external providers).
8. `CreatePerOwinContext<ContextLanguageInfo>`, `UseStageMarker(PipelineStage.Authenticate)`.
9. Culture configuration including language-specific script bundles.
10. Event Hub cache invalidation, cache warmup, and `StartupComplete`.

If website resolution throws, the application restarts the web application so OWIN can reinitialise on
the next request.

`Startup.Auth.cs` supplies `ConfigureAuth`: it builds a login path from the
`Authentication/Registration/LoginButtonAuthenticationType` setting, creates the per-request user,
invitation, sign-in and settings managers, wires the sitemap-aware authentication cookie,
`UseKentorOwinCookieSaver`, external sign-in cookie, two-factor cookies, and finally
`UsePortalsAuthentication`.

## Request pipeline

A typical page request flows like this:

1. **IIS / ASP.NET** picks up the request. `runAllManagedModulesForAllRequests` is on, so managed
   modules run for all requests.
2. **`PortalRoutingModule`** matches the URL against the content map rather than against a fixed route
   table. Content map nodes map a web page's partial URL path to a route handler.
3. **Output cache** — the `OutputCacheModule` and the `Xrm` output-cache provider may serve a cached
   response, varying by the custom strings above.
4. **OWIN pipeline** resolves the website, language, user and content map, as described above.
5. **Route handler** — for a CMS page, a controller renders the page's page template. A Web Template
   (Liquid) template is rendered through `LiquidExtensions`; a Rewrite template maps to an `.aspx` page
   under `Samples/MasterPortal/Pages`.
6. **Liquid rendering** — `LiquidExtensions` builds a per-request *Liquid environment* (globals +
   registers), parses the template with DotLiquid and writes the output. See
   [Web Templates and Liquid](Web-Templates-and-Liquid.md).
7. **Dataverse access** — data is fetched through the layered organization service: caching service,
   evented service, content-map-aware cache, then the SDK client (`OrganizationServiceProxy`,
   `ServiceClient` or `CrmServiceClient`).
8. **Response** is written; the output-cache provider may store it.

### Reserved endpoints

Configured in `Web.config` and route registration:

| Path | Purpose |
|---|---|
| `WebNotification.axd` | Receives change notifications from Dataverse plugins for cache invalidation. |
| `Diag.axd` | Diagnostics handler. |
| `PaymentProcessing.axd` | Payment provider callback (PayPal / Authorize.Net). |
| `_services/about` | Product/build information (`AboutProductHandler`). |
| `_resources/`, `xrm/`, `xrm-adx/` | Embedded resource and script paths served from assemblies. |
| `/setup/` | First-run setup and provisioning UI when the portal is unconfigured. |
| `Telerik.Web.UI.WebResource.axd` | Telerik web resource handler (legacy control dependency). |

## Content map

The **content map** is central to how pages are served. It is built from `adx_webpage`,
`adx_pagetemplate`, `adx_webpageaccesscontrolrule`, publishing states and website bindings, and it
supplies:

- URL routing (partial URL → page),
- the sitemap used by navigation controls and `sitemap` in Liquid,
- permission information used by the site-map security trimming.

It is cached in the object cache and invalidated by web notifications, portal bus messages or Event Hub
invalidation. See [Caching and Performance](Caching-and-Performance.md).

## Background and out-of-band components

- **Portal bus** — `UsePortalBus<T>` middleware plus `PortalBusOrganizationServiceCache`; broadcasts
  cache-invalidation and application-restart messages between instances.
- **Event Hub invalidation** — `EventHubBasedInvalidation` subscribes to Azure Event Hub for both cache
  and search subscriptions, so content and index changes propagate to all instances.
- **Cache warmup** — replays a saved list of organization requests from
  `App_Data/Adxstudio.Xrm.Services.Cache.Warmup` at start-up to prime the object cache.
- **Search index** — a Lucene.Net index under `App_Data/Adxstudio.Xrm.Search`, rebuilt by
  `SearchManager` and queried by `PortalSearchProvider` and the faceted search UI.
- **FluentScheduler** — used for scheduled jobs such as search index maintenance and invalidation work.

## Configuration surfaces

Three configuration layers are in play, in increasing specificity:

1. **`Web.config`** — application settings, connection strings, provider registrations (membership, role
   manager, profile, site map, output cache), binding redirects, handlers and modules. See
   [Configuration Reference](Configuration-Reference.md).
2. **Configuration sections in `Web.config`** — `microsoft.xrm.client` (contexts, services, caches),
   `microsoft.xrm.portal` (portal contexts, security provider, cache policy) and `adxstudio.xrm`
   (search, content map, cache feed, organization service events, CDN).
3. **Portal site settings in Dataverse** — `adx_sitesetting` records resolved per website, read by the
   code at runtime. See [Site Settings Reference](Site-Settings-Reference.md).

## Related pages

- [Getting Started: Build](Getting-Started-Build.md) — build and run this locally
- [Configuration Reference](Configuration-Reference.md) — the configuration layers in detail
- [Framework Namespaces](Framework-Namespaces.md) — walking the `Adxstudio.Xrm` assembly
- [Caching and Performance](Caching-and-Performance.md) — caches and invalidation
