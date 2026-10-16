# Glossary

Portal documentation uses a mix of product marketing terms, Dataverse terms and legacy Adxstudio names.
This page is the canonical definition list for the rest of the wiki.

## Platform terms

| Term | Meaning |
|---|---|
| **CRM** | Customer Relationship Management. In this codebase it refers to the Dynamics 365 organization you connect to (now generally Dataverse). The word is used interchangeably with Dataverse in older code and configuration names. |
| **Dataverse** | The Microsoft data platform (formerly Common Data Service) behind Dynamics 365 and Power Platform. The portal stores all its content and configuration here. |
| **Organization** | One Dataverse environment. Identified by a base URL such as `https://org.crm.dynamics.com`. |
| **Organization service** | The API the portal uses to read and write Dataverse records. The portal wraps it with caching and failover layers. |
| **Solution** | A packaged set of Dataverse components (tables, columns, forms, views, workflows, portal records). The portal requires a *base solution* plus, optionally, your own. |
| **Base solution** | The solution that supplies the portal data model (`adx_*` records). Selected with the `PortalBaseSolution` app setting. |
| **`adx_*` table** | The legacy portal data model table prefix. Still the prefix used throughout this codebase. |
| **`mspp_*` table** | The prefix of the Power Pages *enhanced* data model virtual tables. The portal can coexist with these, but does not use them for its own content. |

## Portal concepts

| Term | Meaning |
|---|---|
| **Portal** | The website presented to end users. In this codebase "portal" and "website" are nearly synonymous; the `adx_website` record is the root configuration object. |
| **Website** | An `adx_website` record. Binds a set of host names to a content tree, languages and settings. Multiple websites can exist in one Dataverse environment. |
| **Website binding** | A record that maps a host name (or primary domain) to a website, so the same application serves different sites. |
| **Web page** | An `adx_webpage` record: a node in the content tree with a title, partial URL, publishing state and a page template. This is the primary unit of portal content. |
| **Partial URL** | The URL segment of a web page relative to its parent, used to build the page's address. |
| **Page template** | An `adx_pagetemplate` record that decides *how* a web page is rendered. Two types exist: a **Rewrite** template (an ASP.NET `.aspx` page in the application) and a **Web Template** (a Liquid template stored in Dataverse). |
| **Web template** | An `adx_webtemplate` record holding **Liquid** source in its `adx_source` column. Web templates can `include` and `extends` each other. |
| **Content snippet** | An `adx_contentsnippet` record: a small localized block of HTML/Liquid addressed by name, used for shared fragments. |
| **Site marker** | A named pointer to a web page, so templates and code can reference a page without hard-coding a URL. |
| **Web link set / web link** | Records that build navigation menus (`adx_weblinkset` containing `adx_weblink`). |
| **Publishing state** | The workflow state of a web page (draft, published, etc.). Controls visibility and whether the page is in the sitemap. |
| **Content map** | The in-memory model of the website's page tree, permissions and URLs. Built at application start and invalidated when content changes. |
| **Site map provider** | The ASP.NET provider that serves the content map (and forum/blog/event page trees) to navigation controls. |

## Templating terms

| Term | Meaning |
|---|---|
| **Liquid** | The template language used by web templates, content snippets and many configuration values. Implemented with the **DotLiquid** library. |
| **Drop** | A DotLiquid object that exposes data to a template (`page`, `user`, `forums`, `blogs`, …). In this codebase a drop is usually a class in `Web/Mvc/Liquid` ending in `Drop`. |
| **Tag** | A Liquid statement in `{% %}`. The portal adds custom tags such as `entitylist`, `entityform`, `entityview`, `webform`, `chart`, `fetchxml`, `searchindex`, `editable`, `rating`, `redirect`, `outputcache`, `substitution`. |
| **Filter** | A Liquid transformation applied with `\|`, such as `date`, `where` or `text_to_html`. |
| **Global object** | A variable always available in a template (`page`, `user`, `request`, `settings`, …). |

## Feature terms

| Term | Meaning |
|---|---|
| **Entity list** | An `adx_entitylist` record plus views and metadata that renders a grid/calendar/map of Dataverse records with search, filtering, sorting and actions. |
| **Entity form** | An `adx_entityform` record plus `adx_entityformmetadata` that renders create/edit/read-only forms for a Dataverse table. |
| **Web form** | An `adx_webform` with steps (`adx_webformstep`) that can gather input, create/update records and run logic. |
| **Table permission** | An `adx_entitypermission` record granting a web role create/read/write/delete/append rights on a Dataverse table. In Power Pages documentation this is called a *table permission*. |
| **Web role** | An `adx_webrole` record assigned to portal users. Roles drive table permissions and content access. |
| **Content access level** | A higher-level permission that gates web pages, web files and knowledge articles. |
| **Web page access control rule** | A per-page rule granting Read/Restricted Read/Write/Delete/Create/Append rights to web roles. |
| **Anonymous users role / authenticated users role** | Built-in roles used by the default permission model. |
| **Entity permission provider** | The component that evaluates table permissions and caches the result. |

## Hosting and operational terms

| Term | Meaning |
|---|---|
| **MasterPortal** | The deployable web application project under `Samples/MasterPortal`. |
| **Output cache** | ASP.NET output caching, backed here by an object cache provider. Several profiles vary by user/roles/website/language. |
| **Object cache** | The in-process memory cache used for Dataverse records and rendered output. |
| **Portal bus** | The mechanism that broadcasts cache-invalidation messages between web instances (via Azure Service Bus / Event Hub). |
| **Cache warmup** | A start-up step that pre-loads commonly used records from a saved request list (`App_Data/Adxstudio.Xrm.Services.Cache.Warmup`). |
| **Search index** | A Lucene.Net index built from selected Dataverse records, stored under `App_Data/Adxstudio.Xrm.Search`, used by the portal search and faceted search. |
| **`WebNotification.axd`** | The endpoint Dataverse plugins call to tell the portal that content changed, so caches can be invalidated. |
| **`Diag.axd`** | A diagnostics endpoint. |
| **`_services/about`** | A route that reports product/build information. |

## Conventions used in this wiki

- Code identifiers appear as `code`.
- **Bold** marks the name of a Dataverse record type on first use.
- "The portal" means the running web application; "Dataverse" means the backing environment.
- Settings are written as they are stored, e.g. `Authentication/Registration/Enabled`, without quoting.
