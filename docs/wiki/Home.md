# xRM Portals Community Edition — Wiki

Welcome. This wiki is the complete documentation for **xRM Portals Community Edition**, a fork of the
open-source release of **Portal Capabilities for Microsoft Dynamics 365 8.3** (originally Adxstudio
Portals). It covers what the product does, how to set it up, how portal administrators use it, and how
developers build and extend it.

> **Read this first.** This project exists to maintain and migrate *existing* portals. It is **not**
> recommended for new portal development — new implementations should use
> [Microsoft Power Pages](https://learn.microsoft.com/en-us/power-pages/). See
> [Project Overview](Project-Overview.md) for scope, support status and disclaimers.

---

## Start here

| I want to… | Go to |
|---|---|
| Understand what this project is and whether it fits | [Project Overview](Project-Overview.md) |
| Understand the words used in portal documentation | [Glossary](Glossary.md) |
| Understand how the code is put together | [Architecture](Architecture.md) |
| Build the solution from source on my machine | [Getting Started: Build](Getting-Started-Build.md) |
| Connect the portal to Dataverse / Dynamics 365 | [CRM Connection](CRM-Connection.md) |
| Look up every `Web.config` setting | [Configuration Reference](Configuration-Reference.md) |
| Deploy to IIS or Azure App Service | [Deployment](Deployment.md) |
| Upgrade from 8.3 / Adxstudio 7 / migrate to Power Pages | [Upgrading and Migration](Upgrading-and-Migration.md) |
| Administer a running portal | [Portal Administration](Portal-Administration.md) |
| Write Liquid templates | [Web Templates and Liquid](Web-Templates-and-Liquid.md) |
| Look up a Liquid object, tag or filter | [Liquid Objects Reference](Liquid-Objects-Reference.md), [Liquid Tags and Filters Reference](Liquid-Tags-and-Filters-Reference.md) |
| Look up a portal site setting | [Site Settings Reference](Site-Settings-Reference.md) |
| Look up a `adx_*` / `mspp_*` table | [Portal Data Model](Portal-Data-Model.md) |
| Extend the portal in C# | [Extensibility](Extensibility.md) |
| Something is broken | [Troubleshooting](Troubleshooting.md) |

---

## Documentation map

### Orientation

- [Project Overview](Project-Overview.md) — history, lineage, objectives, scope, support status
- [Glossary](Glossary.md) — portal, website, web page, web template, entity list, and the rest
- [Architecture](Architecture.md) — projects, layers, request pipeline, runtime components

### Build, configure, deploy

- [Getting Started: Build](Getting-Started-Build.md) — prerequisites, clone, restore, build, run locally
- [Configuration Reference](Configuration-Reference.md) — every `Web.config` section, app setting and connection string
- [CRM Connection](CRM-Connection.md) — client selection, connection strings, base solution
- [Deployment](Deployment.md) — IIS and Azure App Service, machine keys, file permissions
- [Upgrading and Migration](Upgrading-and-Migration.md) — 8.3 → `dev`, removed features, Power Pages parity
- [Troubleshooting](Troubleshooting.md) — common failures and how to diagnose them

### Portal administration (end-user documentation)

- [Portal Administration](Portal-Administration.md) — how the admin surface is organised
- [Content Management](Content-Management.md) — web pages, web files, snippets, navigation, publishing states
- [Web Templates and Liquid](Web-Templates-and-Liquid.md) — the templating model and a Liquid tutorial
- [Liquid Objects Reference](Liquid-Objects-Reference.md) — `page`, `user`, `request`, `website`, `settings`, …
- [Liquid Tags and Filters Reference](Liquid-Tags-and-Filters-Reference.md) — every tag and filter
- [Entity Lists](Entity-Lists.md) — grids, calendars, maps, views, actions, filters
- [Entity Forms](Entity-Forms.md) — forms, metadata, modes, steps, redirects
- [Web Forms](Web-Forms.md) — multi-step forms with entity/logic steps
- [Web Files and Attachments](Web-Files-and-Attachments.md) — file storage, cloud storage, notes
- [Security and Authentication](Security-and-Authentication.md) — providers, web roles, table permissions, content access
- [Users, Profiles and Invitations](Users-Profiles-and-Invitations.md) — contacts, registration, profile, invitations
- [Search](Search.md) — Lucene index, faceted search, configuration
- [Multi-Language](Multi-Language.md) — portal languages, fallback, localized URLs
- [Community Features](Community-Features.md) — forums, blogs, ideas, issues, comments, ratings, tags, badges
- [Cases and Knowledge](Cases-and-Knowledge.md) — case management, help desk, knowledge articles
- [Events and Conferences](Events-and-Conferences.md) — events, schedules, speakers, sponsors, registration
- [Commerce](Commerce.md) — shopping cart, checkout, payment providers, discounts
- [SharePoint Integration](SharePoint-Integration.md) — document libraries and grids
- [Ads, Polls and Marketing](Ads-Polls-and-Marketing.md) — ads, polls, marketing lists, feedback

### Reference and developer documentation

- [Site Settings Reference](Site-Settings-Reference.md) — portal settings read by the code, grouped by feature
- [Portal Data Model](Portal-Data-Model.md) — the `adx_*` / `mspp_*` tables the portal uses
- [Extensibility](Extensibility.md) — custom Liquid tags, filters and globals, providers, plugins
- [Framework Namespaces](Framework-Namespaces.md) — tour of the `Adxstudio.Xrm` assembly
- [Caching and Performance](Caching-and-Performance.md) — caches, output cache, invalidation, warmup
- [Diagnostics and Logging](Diagnostics-and-Logging.md) — trace sources, handlers, health endpoints
- [Contributing](Contributing.md) — branch, build validation, changelog and commit conventions

---

## Source of truth

This wiki describes the **`dev` branch**. Feature areas are documented from the code in
`Framework/Adxstudio.Xrm` and `Samples/MasterPortal`. Where behaviour is configurable, the
configuration surface is listed in [Site Settings Reference](Site-Settings-Reference.md) and
[Configuration Reference](Configuration-Reference.md).

Two internal notes are kept outside the wiki and are the authority on narrow topics:

- [`docs/design/connection-configuration.md`](../design/connection-configuration.md) — CRM connection detail
- [`docs/internal/power-pages-compatibility.md`](../internal/power-pages-compatibility.md) — measured Liquid differences from Power Pages

---

## Publishing this wiki

The files in `docs/wiki/` use GitHub-wiki conventions: `Home.md` is the landing page and
`_Sidebar.md` / `_Footer.md` provide navigation. To publish, copy the contents of `docs/wiki/` into the
repository's wiki (for example by cloning `xRM-Portals-Community-Edition.wiki.git` and copying the
files in). Links are written so they resolve both on the GitHub wiki and when reading the files directly
in the repository.
