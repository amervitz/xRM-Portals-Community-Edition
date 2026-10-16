# Content Management

Everything a visitor reads or downloads is a Dataverse record. This page covers the content objects, how
they fit together, and how a URL becomes a rendered page.

## The content tree

Pages form a **tree**. Each `adx_webpage` has a parent (`adx_parentpageid`) except the root
(`adx_isroot`), and a **partial URL** (`adx_partialurl`) that becomes its path segment. The full URL of a
page is built by walking from the root down through its ancestors.

```
adx_webpage (root, partial URL "home" or blank)
├── adx_webpage  partial URL "products"
│   ├── adx_webpage  partial URL "widgets"      →  /products/widgets
│   └── adx_webpage  partial URL "services"     →  /products/services
└── adx_webpage  partial URL "support"
    └── adx_webpage  partial URL "contact"      →  /support/contact
```

The tree is compiled into the **content map**, which is what actually routes requests and powers
navigation. If a page is not in the content map, it does not exist as far as visitors are concerned.

### Web page fields you will use most

| Column | Purpose |
|---|---|
| `adx_name` | The record's name (authoring identity, also used by `include`/template lookups). |
| `adx_title` | The page title, used in the browser title and headings. |
| `adx_partialurl` | The URL segment. |
| `adx_parentpageid` | The parent page. |
| `adx_pagetemplateid` | How the page renders — the most important field. |
| `adx_publishingstateid` | Whether the page is live. |
| `adx_hiddenfromsitemap` | Keeps the page routable but out of generated navigation. |
| `adx_copy` | The page's main content (HTML, often containing Liquid). |
| `adx_summary` | A short summary, used by listings and search fragments. |
| `adx_meta` | `<meta>` tags for the page. |
| `adx_customcss`, `adx_customjavascript` | Per-page style and script. |
| `adx_displaydate`, `adx_displayorder` | Ordering in listings and navigation. |
| `adx_enablerating`, `adx_feedbackpolicy`, `adx_commentpolicy` | Per-page ratings, feedback and comment policy. |
| `adx_redirect`, `adx_redirecturl` | Redirect this page to another URL. |
| `adx_webpagelanguageid` | The language of this page, in a multi-language site. |
| `adx_headerwebtemplateid`, `adx_footerwebtemplateid`, `adx_usewebsiteheaderandfooter` | Per-page header and footer overrides. |

## Page templates: how a page renders

A **page template** (`adx_pagetemplate`) decides the rendering path, and there are exactly two kinds:

| `adx_type` | Name | Behaviour |
|---|---|---|
| `756150000` | **Rewrite** | The page is served by an ASP.NET page. The template points at a file in the application (for example `~/Pages/Page.aspx`). |
| `756150001` | **Web Template** | The page is rendered by **Liquid**: the template points at an `adx_webtemplate`, whose `adx_source` column holds the Liquid source. |

A template also carries:

- `adx_isdefault` — the template used when a page does not specify one.
- Associated records (`adx_entityform`, `adx_entitylist`, `adx_webform`, `adx_entityname`) so that
  built-in templates know what they render.
- `adx_usewebsiteheaderandfooter` — whether the site header/footer wraps the output.
- A description, used by the authoring UI.

`Samples/MasterPortal/Pages/` contains the built-in ASP.NET pages that Rewrite templates point at:
`Page.aspx`, `WebTemplate.aspx`, `WebForm.aspx`, `EntityList`-related pages, `Listing.aspx`,
`Profile.aspx`, `SiteMap.aspx`, `Search.aspx`, and the error pages `404.aspx`, `500.aspx`,
`CrmConnectionError.aspx`.

### When to use which

- **Web Template (Liquid)** is the normal modern choice: templates live in Dataverse, can be edited
  without a deployment, and can `include` each other. See
  [Web Templates and Liquid](Web-Templates-and-Liquid.md).
- **Rewrite** is for portal features that need server-side code — the built-in listing, profile, search
  and web form pages, or custom ASP.NET pages you add to the application.

## Web templates

An `adx_webtemplate` holds **Liquid** in `adx_source`, plus `adx_name` (used as the file name when
included) and `adx_websiteid`.

Web templates are the reusable building blocks of a portal's look:

- **Layouts** — `layout_1_column`, `layout_2_column_wide_left`, `layout_2_column_wide_right`,
  `layout_3_column_wide_middle` are shipped as embedded templates in the framework.
- **Partials** — `page_header`, `page_copy`, `top_navigation`, `side_navigation`, `breadcrumbs`,
  `snippet`, `weblink_list_group`, `child_link_list_group`, plus feature-specific ones (`entity_list_grid`,
  `entity_list_filter`, `events_upcoming`, `poll`, `search`, `ad`, `badges`, `blogs`, `forums`).

Templates resolve from two file systems at once, in order:

1. **`adx_webtemplate` records** from the website (`EntityFileSystem`), then
2. **embedded framework resources** (`EmbeddedResourceFileSystem`, from the `Adxstudio.Xrm.Liquid`
   resource namespace).

So you can override a shipped template by creating a web template with the same name. These templates are
addressable in Liquid with `include` and `extends` — see
[Web Templates and Liquid](Web-Templates-and-Liquid.md).

## Content snippets

An `adx_contentsnippet` is a small named block of markup — a phone number, a legal footer, a call to
action — resolved by name and language:

- `adx_name` — the key used to address it.
- `adx_value` — the content (may contain Liquid).
- `adx_contentsnippetlanguageid` — the language, for localized values.
- `adx_websiteid` — the website scope.

Snippets are exposed to templates as the `snippets` global object and can be resolved in ASP.NET markup
with the `Snippet` expression builder (`<%$ Snippet:Name %>`).

Snippet types matter in the admin UI: some are *text*, some are *HTML*, so that the editor sanitizes or
escapes appropriately. When a snippet holds HTML, put Liquid in it deliberately — it is rendered.

## Web files

An `adx_webfile` is a downloadable file:

| Column | Purpose |
|---|---|
| `adx_name`, `adx_partialurl` | Name and URL segment. |
| `adx_publishingstateid` | Whether it is served. |
| `adx_mimetype` | Content type to send. |
| `adx_contentdisposition` | Inline vs. attachment. |
| `adx_summary` | Description. |
| `adx_cloudblobaddress` | The blob address when storage is cloud-based. |

Where files are stored is controlled by `WebFiles/StorageLocation` and `WebFiles/CloudStorageAccount`.
See [Web Files and Attachments](Web-Files-and-Attachments.md).

## Navigation

Portal navigation comes from several places, and which you use depends on how dynamic it needs to be:

| Mechanism | Records | Use for |
|---|---|---|
| **Content tree** | `adx_webpage` | Automatically derived navigation — the top and side navigation templates walk the page tree. |
| **Web link sets** | `adx_weblinkset` containing `adx_weblink` | Hand-curated menus, footer links, external links. |
| **Site markers** | `adx_sitemarker` | Named pointers to a page, so templates and code reference a page without hard-coding a URL. |
| **Shortcuts** | `adx_shortcut` | Extra entries contributed to the sitemap. |

The shipped navigation templates — `top_navigation`, `side_navigation`, `breadcrumbs`,
`weblink_list_group`, `child_link_list_group` — show how these are combined in practice.

Web links support images (`adx_imageurl`, `adx_imagealttext`, `adx_imagewidth`, `adx_imageheight`),
open-in-new-window behaviour (`adx_openinnewwindow`), and display ordering.

## Publishing states

An `adx_publishingstate` models a workflow stage for content — commonly **Draft** and **Published**, and
often additional stages such as *In review*. Every web page and web file points at a publishing state.

What publishing states give you:

- **Visibility** — only pages in a state that the site treats as "published" appear to visitors.
- **Sitemap membership** — unpublished content is absent from the content map.
- **Transition security** — `IPublishingStateTransitionSecurityProvider` and the
  `adx_publishingstatetransitionrule` table control who may move content between states.
- **Preview** — `adx_previewunpublishedentities` lets authorized users preview unpublished content.

A publishing state has a website scope, a display order, and a default flag.

## Organising content at scale

| Tool | Records | Purpose |
|---|---|---|
| **Page tags** | `adx_pagetag`, `adx_pagetagrelationship` | Tag pages for cross-cutting listings and "related" queries. |
| **Categories** | `adx_category` | A hierarchical categorization used by the Category area (`Category.aspx`, `CategoryTopic.aspx`). |
| **URL history** | `adx_urlhistory` | Records old URLs so the portal can redirect them after a page moves. |
| **Redirects** | Page `adx_redirect`/`adx_redirecturl`, plus redirect providers | 301/302 handling. |

**Partial URLs should be stable.** If you must change one, the `adx_urlhistory` mechanism exists so that
the old address still resolves.

## Access control on content

Three different mechanisms can restrict a page, and they compose:

1. **Web page access control rules** (`adx_webpageaccesscontrolrule`) — per-page rights (Read,
   Restricted Read, Write, Delete, Create, Append) granted to web roles. A rule with **Restricted Read**
   hides the page from listings while still allowing direct access.
2. **Publishing state** — only published content is served.
3. **Content access levels** — a higher-level gate that applies across web pages, web files and knowledge
   articles, chiefly used for licensing/entitlement.

See [Security and Authentication](Security-and-Authentication.md).

## How a request becomes a page

1. The URL is matched against the **content map** built from published pages and website bindings.
2. The matching page's **page template** decides the renderer.
3. A **Web Template** template renders its Liquid with the standard globals — including `page`, which
   exposes the page's own fields — while a **Rewrite** template runs the ASP.NET page.
4. The result is returned, and may be **output cached** (varying by roles, user, website and language).

Practical consequence: if a change does not appear, the likely culprits are the publishing state, a stale
content map, or the output cache. See [Troubleshooting](Troubleshooting.md#content-changes-do-not-appear).

## Related pages

- [Web Templates and Liquid](Web-Templates-and-Liquid.md) — writing the templates
- [Entity Lists](Entity-Lists.md), [Entity Forms](Entity-Forms.md), [Web Forms](Web-Forms.md)
- [Web Files and Attachments](Web-Files-and-Attachments.md)
- [Security and Authentication](Security-and-Authentication.md)
- [Portal Data Model](Portal-Data-Model.md) — the full set of content tables
