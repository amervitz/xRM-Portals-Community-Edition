# Upgrading and Migration

This page is for existing deployments: Adxstudio Portals 7, Portal Capabilities for Microsoft Dynamics 365
8.3, or an earlier `xRM-Portals-Community-Edition` commit. It covers what changed, what was removed, and
how to plan a move to Microsoft Power Pages.

## The three-step path

```
Adxstudio Portals 7  →  xRM Portals Community Edition  →  Microsoft Power Pages
                        (intermediary migration step)
```

The project's stated objective is to be an **intermediary migration path**: run an existing Adxstudio
Portal on a supported runtime here, then move to Power Pages on a timeline you control, or stay on this
codebase if self-hosting is a requirement.

## What changed in the `dev` branch

Review the [Unreleased changelog](../../CHANGELOG.md#unreleased) before deploying — it is the authoritative
list. The themes are:

| Theme | Examples |
|---|---|
| **Runtime modernization** | All projects retargeted to **.NET Framework 4.8.1**; Visual Studio build prerequisites updated. |
| **Dependency updates** | CRM SDK, ASP.NET MVC/Razor/WebPages, ASP.NET Identity, DotLiquid, HtmlAgilityPack, Common.Logging. |
| **Dependency removal** | SharpZipLib, C5, JetBrains.Annotations, Bond, Microsoft.AspNet.WebApi.Client, BouncyCastle, SafeNuGet, JetBrains analyzers / StyleCop. |
| **Security improvements** | Sign-in page no longer output cached (anti-forgery fix); blobs and attachments fixed; public-key export moved to `System.Formats.Asn1`. |
| **Closer Power Pages parity** | Legacy features removed to reduce divergence from Power Pages. |

### Connection model change

The most significant operational change is the introduction of **`OrganizationServiceType`** and
**`PortalBaseSolution`**:

- Before: the legacy `OrganizationServiceProxy` path was implicit.
- Now: you select the client explicitly. **Omitted or blank keeps the legacy behaviour**, so existing
  deployments continue to work without changes.

If you are moving to Dataverse (online), set `OrganizationServiceType` to `ServiceClient` and provide a
client-secret or certificate connection string. See [CRM Connection](CRM-Connection.md).

## Removed features and what to do about them

These legacy features have been removed. Each removal requires action in an existing deployment.

### Entity list OData feed (`/_odata`)

**Removed.** The entity list OData feed aligned poorly with Power Pages data access features.

*Action:* identify integrations that call `/_odata` and replace them with an alternative data access path
(entity lists, web API through a custom endpoint, or a direct Dataverse integration).

### Web page and web file tracking

**Removed**, matching Power Pages, where tracking is no longer available since version 9.3.4.x.

- Requests to web pages and web files with **Enable Tracking** set no longer create `adx_webpagelog` or
  `adx_webfilelog` records.
- The field is no longer shown when editing either record type on the portal.
- The `asyncTrackingEnabled` attribute of the `adxstudio.xrm` configuration section no longer exists.

*Action:* if your `Web.config` sets `asyncTrackingEnabled`, **delete that attribute**. Reconcile any
reporting that consumed the tracking tables.

### Customer journey tracking

**Removed.** The portal used to post interaction telemetry to a Microsoft-internal Dynamics Customer
Insights (DCI) hub that only Microsoft's own portal hosting provisioned. Removed along with the
`FCB.CustomerJourneyTracking` feature flag and the `PortalTracking` / `PortalTracking.*` app settings.

*Action:* remove those settings from configuration; they have no effect.

### Microsoft-internal IFx/MDM metrics pipeline

**Removed** (`AdxMetrics`, `MdmMetrics`, `IfxMetricsReporter`, `MetricsReportingEvents`). It reported to
Microsoft's internal Geneva monitoring and was unreachable dead code because the framework package was
never referenced.

*Action:* none.

### Azure Cloud Services (classic) hosting

**Removed**, as Azure Cloud Services (classic) was retired on 1 September 2024.

*Action:* migrate hosting to **IIS** or **Azure App Service**. See [Deployment](Deployment.md).

### Windows Live ID Web Authentication

**Removed** as an obsolete sign-in protocol.

*Action:* configure another authentication provider compatible with your Power Pages migration plans. See
[Security and Authentication](Security-and-Authentication.md).

### Unused feature flags

`FCB.Web2Case` and `FCB.Categories` were removed because no feature checks remained.

*Action:* none.

### Build-time vulnerability check (SafeNuGet)

**Removed.** NuGet 6.10+ provides vulnerability auditing for `packages.config`, and GitHub Dependabot
provides alerts when enabled. A build-time **SharpZipLib guard**
(`Build/BlockSharpZipLib.targets`) replaced the general check for the one dependency that kept
reappearing.

*Action:* when updating Lucene.Net or migrating a project to `PackageReference`, watch for the guard
failing and remove SharpZipLib as instructed.

## Compatibility note: Liquid behaviour

xRM Portals aims to render Liquid the way Power Pages does, and to have **no Liquid features Power Pages
lacks**. As of 2026-09-27, with DotLiquid 2.0.385, 340 of 348 compatibility tests matched. The known
differences are:

| Difference | Effect | Status |
|---|---|---|
| Missing `html_safe_escape` filter | HTML that Power Pages sanitizes passes through **unsanitized** here | Planned — the most important gap |
| No default HTML encoding of `user` and `request` | Values from `user`/`request` are not HTML-encoded by default; there is no `encode` filter | Planned |
| Fractional seconds in `date_add_seconds` | Fractions are kept instead of dropped | Minor |
| Server time zone in date parsing | Date strings with a time zone, and `'now'`, are converted to **server local time**; Power Pages runs in UTC | Minor — set the server to UTC to match |

Full detail, including the test IDs and reproduction, is in
[`docs/internal/power-pages-compatibility.md`](../internal/power-pages-compatibility.md). If you are
migrating templates **to** Power Pages, review that page first: templates that rely on the gaps will behave
differently on each side.

## Upgrade procedure for an existing xRM Portals deployment

1. **Read the changelog.** Start with [`CHANGELOG.md`](../../CHANGELOG.md#unreleased).
2. **Audit for removed features** using the list above — especially `/_odata` consumers,
   `asyncTrackingEnabled`, `PortalTracking*` settings, and Windows Live ID configuration.
3. **Snapshot** the current deployment and database/solution state.
4. **Update `Web.config`.** Remove settings for removed features. Optionally add `OrganizationServiceType`
   and `PortalBaseSolution`. Adopt the security fixes (in particular, do not output-cache the sign-in page).
5. **Rebuild and publish** the new `MasterPortal`.
6. **Restart / recycle** the application pool.
7. **Verify** using the post-deployment checklist in [Deployment](Deployment.md#post-deployment-verification),
   with extra attention to authentication (the anti-forgery fix changes caching behaviour on the sign-in
   page) and search (the indexer now skips `mspp_*` views — see below).

### Search-index fix to be aware of

Building the search index used to fail in environments that also have the Power Pages **enhanced data
model**. The `mspp_*` virtual tables have their own "Portal Search" views, which the indexer picked up, and
requesting `modifiedon` from them threw because virtual tables such as `mspp_webpage` do not have that
column. Views on `mspp_*` tables are now skipped.

*Impact:* if your environment has the enhanced data model and index rebuilds were failing, this upgrade
fixes them. If you had worked around the problem by hiding those views, you can revert the workaround.

### Blob and attachment fixes

If you use **Azure Blob Storage** for web files or note attachments, this branch fixes several defects:

- Cloud blob web files with a fully qualified `adx_cloudblobaddress` (the format Power Pages uses) now
  download correctly; both the relative-path and fully-qualified forms are understood.
- Note attachments now save to blob storage (the annotation was being updated through an unattached
  service context).
- Replacing a note's blob attachment now removes the previous blob.
- Deleting or editing a note no longer throws a `NullReferenceException` in the notes/timeline controls.

## Migrating to Microsoft Power Pages

New portal implementations should use Power Pages; this project is a bridge. When you are ready to move:

1. **Inventory** the portal features you actually use. The wiki feature pages map directly onto Power
   Pages concepts (web pages, page templates, web templates/Liquid, entity lists → lists, entity forms →
   forms, table permissions, web roles, content snippets, site settings).
2. **Check Liquid compatibility** with the table above and the internal compatibility note. Remove reliance
   on features Power Pages lacks before or during migration.
3. **Review the gap list** in
   [`docs/internal/power-pages-compatibility.md`](../internal/power-pages-compatibility.md) for features
   that have not yet been comparison-tested: the Dataverse tags (`entitylist`, `entityview`, `fetchxml`,
   `searchindex`, `entityform`, `webform`, `chart`, `editable`), `include`/`extends`, the global objects,
   and record-driven filters such as `where`, `group_by`, `order_by`, `select`, `except` and `has_role`.
4. **Recreate** customizations that were made in C# here. Those have no equivalent in Power Pages and need
   to be rebuilt on Power Pages' extension points or replaced with supported functionality.

## Related pages

- [`docs/design/connection-configuration.md`](../design/connection-configuration.md) — the authority on connection configuration
- [`docs/internal/power-pages-compatibility.md`](../internal/power-pages-compatibility.md) — measured Liquid differences
- [`CHANGELOG.md`](../../CHANGELOG.md) — full change history
- [Deployment](Deployment.md)
- [Troubleshooting](Troubleshooting.md)
