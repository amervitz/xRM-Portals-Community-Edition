# Portal Administration

This page is the starting point for **portal administrators** — the people who run a live portal without
writing code. It explains where everything lives, what you are responsible for, and where to go next.

## The mental model

There is no separate portal admin UI to learn. **The portal is administered in Dataverse.** Almost
everything you change — pages, templates, navigation, permissions, settings, forms, lists — is a Dataverse
record. The web application reads those records and renders them.

```
Dataverse record                        →  What it becomes on the site
────────────────────────────────────────────────────────────────────────
adx_website                                the site itself (host names, languages)
adx_webpage                                a page in the content tree
adx_pagetemplate                           how a page renders (ASP.NET page or Liquid web template)
adx_webtemplate                            a Liquid template
adx_contentsnippet                         a named, localized block of markup
adx_webfile                                a downloadable file
adx_entitylist / adx_entityform / adx_webform   interactive table/list/form experiences
adx_sitesetting                            a configuration value
adx_webrole / adx_entitypermission /       who can see and do what
  adx_webpageaccesscontrolrule
adx_weblinkset / adx_weblink / adx_sitemarker    navigation
```

So your working tools are:

1. **The Dataverse/model-driven app or the classic Portal Management app** for editing portal records.
2. **The portal itself**, which some features let you edit inline (the `editable` Liquid tag and the
   front-side editing controls).
3. **`Web.config`** on the server, for the connection and a handful of application-level settings.

## Users and administrators

There are two distinct populations, and it is worth keeping them straight:

| Population | Stored as | Managed with |
|---|---|---|
| **Portal users** (customers, partners, citizens) | **Contact** records, optionally with an `adx_identity` for credentials | Registration, invitations, or Dataverse user administration |
| **Portal administrators** (you) | **System users** in Dataverse with permission to edit portal records | Dataverse security roles and the model-driven app |
| **Portal authors/editors** (optional) | Portal users in web roles with **content management** privileges | Web roles plus the front-side editing features |

A portal user is *not* a Dataverse system user. Portal users sign in through the portal's own
authentication (ASP.NET Identity over OWIN) and are authorized by **web roles**, not Dataverse security
roles.

## What you configure, and where

### 1. The website

The `adx_website` record is the root. From it you control:

- **Website bindings** — which host names belong to this site.
- **Languages** and the default language (see [Multi-Language](Multi-Language.md)).
- **Header and footer web templates**.
- **Publishing states** available on the site.
- **Search provider** configuration and whether search is enabled.
- **Header settings** (security headers, HSTS) and display modes.

### 2. Content

Pages, files, snippets, templates and navigation. This is the bulk of day-to-day work — see
[Content Management](Content-Management.md).

### 3. Behaviour and features

| Feature | Configured by | Documentation |
|---|---|---|
| Lists of Dataverse records | `adx_entitylist` + views + metadata | [Entity Lists](Entity-Lists.md) |
| Forms over Dataverse records | `adx_entityform` + `adx_entityformmetadata` | [Entity Forms](Entity-Forms.md) |
| Multi-step forms | `adx_webform` + `adx_webformstep` | [Web Forms](Web-Forms.md) |
| Files and attachments | `adx_webfile`, annotation settings | [Web Files and Attachments](Web-Files-and-Attachments.md) |
| Sign-in, registration, providers | Authentication site settings | [Security and Authentication](Security-and-Authentication.md) |
| Permissions | Web roles, table permissions, content access, page access rules | [Security and Authentication](Security-and-Authentication.md) |
| Search | `Search/*` settings + index rebuild | [Search](Search.md) |
| Community features | Forums, blogs, ideas, issues, tags, badges | [Community Features](Community-Features.md) |
| Support | Cases, help desk, knowledge articles | [Cases and Knowledge](Cases-and-Knowledge.md) |
| Commerce | Products, cart, payment providers | [Commerce](Commerce.md) |
| SharePoint documents | SharePoint site settings | [SharePoint Integration](SharePoint-Integration.md) |
| Ads, polls, marketing | `adx_ad`, `adx_poll`, marketing lists | [Ads, Polls and Marketing](Ads-Polls-and-Marketing.md) |
| Everything configurable at runtime | `adx_sitesetting` records | [Site Settings Reference](Site-Settings-Reference.md) |

### 4. The application

A small number of things live in `Web.config` rather than Dataverse: the Dataverse connection and client
selection, cache and trace settings, the machine key, SMTP settings, and the provider registrations.
Those are covered in [Configuration Reference](Configuration-Reference.md).

## Site settings: the configuration dial

Hundreds of behaviours are controlled by **`adx_sitesetting`** records. A setting has a name (a slashed
path such as `Authentication/Registration/OpenRegistrationEnabled`) and a value, and it is scoped to a
website. Settings are resolved per website at runtime, with a code default when the record is absent.

Because settings are data, changing one takes effect subject to caching — expect an invalidation delay or
a recycle rather than an instant change.

[Site Settings Reference](Site-Settings-Reference.md) lists the settings the code reads, grouped by
feature. When you are unsure how to turn a feature off, look there first.

## Content authoring with front-side editing

The portal supports editing some content in place:

- The **`editable` Liquid tag** renders a field as editable markup for authorized users.
- Web page content (`adx_copy`), snippets and web files can be edited from the front side depending on the
  controls and permissions in use.
- The **content management** privileges on a web role (`adx_managecontentsnippets`,
  `adx_managesitemarkers`, `adx_manageweblinksets`, `adx_managepermissions`) govern what an author can
  change.

This lets you give authors a limited, safe editing surface without granting Dataverse access.

## Operational responsibilities

If you run the portal, you are responsible for:

| Area | What it means |
|---|---|
| **Availability** | Hosting, app-pool health, warmup after restarts. |
| **Cache and content propagation** | Making sure content changes reach the site (web notification plugin, portal bus, Event Hub). |
| **Search index** | Rebuilding the index when content or configuration changes. |
| **Authentication** | Provider configuration, registration policy, password and lockout policy, certificates and secrets. |
| **Backups** | Both Dataverse (content and configuration) and the environment-specific `Web.config` values. |
| **Upgrades** | Reading the changelog and following the upgrade checklist — see [Upgrading and Migration](Upgrading-and-Migration.md). |
| **Secrets** | Keeping connection strings, client secrets and payment credentials out of source control. |

## Common administrative tasks

| Task | Where |
|---|---|
| Add a page | Create an `adx_webpage` with a parent, partial URL, page template and publishing state. |
| Change what a page looks like | Edit the page template or its web template. |
| Add a navigation menu item | Add an `adx_weblink` to the relevant `adx_weblinkset`. |
| Show a list of records | Create an `adx_entitylist` and reference it from a page template or `{% entitylist %}`. |
| Let users submit a form | Create an `adx_entityform` and place it on a page. |
| Give a group access to a page | Add an `adx_webpageaccesscontrolrule` for the web role. |
| Let a group read a Dataverse table | Add an `adx_entitypermission` for the web role. |
| Turn registration off | Set `Authentication/Registration/OpenRegistrationEnabled` to `false`. |
| Change the sign-in providers | Configure provider settings and `Web.config` values. |
| Turn search on | Set `Search/Enabled` to `true` and build the index. |
| Enable another language | Add a site language and translate content. |
| Point a new domain at the site | Add a website binding and a server binding. |

## Where to go next

- **New to the content model?** Read [Content Management](Content-Management.md), then
  [Web Templates and Liquid](Web-Templates-and-Liquid.md).
- **Setting something up for the first time?** [Getting Started: Build](Getting-Started-Build.md) and
  [Deployment](Deployment.md) cover the technical setup.
- **Looking for a specific setting?** [Site Settings Reference](Site-Settings-Reference.md).
- **Confused by a term?** [Glossary](Glossary.md).
