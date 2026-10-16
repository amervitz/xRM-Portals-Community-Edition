# Deployment

xRM Portals Community Edition is a set of .NET Framework class libraries plus an ASP.NET web application
(`MasterPortal`). Deployment means publishing that web application to IIS or Azure App Service and
pointing it at a Dataverse environment that already contains the portal solutions and data.

> The `Self-hosted_Installation_Guide_for_Portals.pdf` distributed with the Microsoft Download Center
> describes the **8.3** release. When deploying the `dev` branch, use the build, connection and runtime
> requirements in the repository README and this wiki instead — several legacy features have been removed.
> See [Upgrading and Migration](Upgrading-and-Migration.md).

## Runtime requirements

| Requirement | Detail |
|---|---|
| **OS** | 64-bit Windows Server 2022 or 2025, or Windows 11 for local development, on a Microsoft-supported release and edition. |
| **.NET Framework** | 4.8.1, installed. Included with Windows Server 2025 and Windows 11 22H2+. |
| **Web server** | IIS 10.0 with **ASP.NET 4.x** features enabled, or Azure App Service. |
| **Process bitness** | 64-bit. Set the IIS application pool to 64-bit; configure the App Service platform accordingly. |
| **TLS** | TLS 1.2 (or later) enabled for outbound connections to Dataverse and authentication services. Enabled by default on supported Windows versions. |
| **Outbound network** | HTTPS to the Dataverse organization URL, and to any identity providers and event hubs you configure. |

**Azure Cloud Services (classic) is not supported** — that hosting model was retired on 1 September 2024
and support was removed from the codebase. Use IIS or Azure App Service.

## What you deploy

The deployable artifact is the **`MasterPortal`** web application:

```
Samples/MasterPortal/
  Web.config              application configuration
  Global.asax             application start-up
  App_Data/               writable state (search index, cache warmup)
  App_Start/              routing, setup, bundles, OWIN auth
  Areas/                  MVC areas (Account, Commerce, Search, Setup, …)
  bin/                    compiled assemblies and NuGet dependencies
  Controls/ MasterPages/ Pages/ Views/   Web Forms and Razor markup
  css/ fonts/ img/ js/    static assets
```

Publish with Visual Studio's **Publish** command (Web Deploy, file system, or FTP). A file-system publish
produces a folder you copy to the server's site root.

### Configuration transforms

`Web.Debug.config` and `Web.Release.config` are transform files. Use them (or deployment-platform settings)
for per-environment values so that the committed `Web.config` never holds production secrets. In
particular, move the `Xrm` connection string, `Azure.Authentication.*` values and any payment credentials
into the target environment.

## Required Dataverse components

The portal needs these installed in the target environment before it will serve content:

1. The **Dynamics 365 portal solutions**, obtained from the Microsoft Download Center in
   `MicrosoftDynamics365PortalsSolutions.exe`. **These components are not covered by the MIT license and
   are not managed by this project.** Install them using the supplied deployment tooling.
2. A **base solution** matching the `PortalBaseSolution` app setting (`MicrosoftCrmPortalBase` or
   `MicrosoftPortalBase`).
3. **Portal content**: an `adx_website` record with website bindings, web pages, page templates,
   web templates, content snippets, web roles and site settings.
4. A **Dataverse application user** (for `ServiceClient`) or a service account (for `CrmServiceClient`)
   with the security role the portal requires.

If the environment has no portal records, the application starts in setup mode. See
[Getting Started: Build](Getting-Started-Build.md#setup-mode).

## IIS deployment

1. **Create the application pool.** .NET CLR version **v4.0**, managed pipeline **Integrated**,
   **Enable 32-Bit Applications = False**.
2. **Create the site.** Point the physical path at the published folder and add an HTTPS binding with a
   valid certificate.
3. **Grant file system permissions** (next section).
4. **Configure `Web.config`** for the environment: connection string, `OrganizationServiceType`,
   `PortalBaseSolution`, `cookieRequireSSL="true"`, `debug="false"`, mail settings, and a machine key.
5. **Recycle the pool** and load the homepage.

### File system permissions

The application pool identity (for example `IIS AppPool\<PoolName>`) needs:

| Path | Access | Why |
|---|---|---|
| The site root and all files under it | **Read** (and execute for the `bin` assemblies) | Serving the application. |
| `App_Data` and its subfolders | **Modify** | The Lucene search index (`App_Data/Adxstudio.Xrm.Search`) and the cache warmup request store (`App_Data/Adxstudio.Xrm.Services.Cache.Warmup`) are written at runtime. |
| `%SystemRoot%\Microsoft.NET\Framework64\v4.0.30319\Temporary ASP.NET Files` | **Modify** | ASP.NET compiles pages and Razor views into a temporary folder, unless the application is precompiled. |

If you redirect the search index elsewhere with the `Adxstudio.Xrm.Search.Index.DirectoryPath` setting,
grant Modify there instead. Missing write access typically shows up as search never returning results, or
as warmup/caching errors in the trace log.

### Machine key

Add a `<machineKey>` to `Web.config` so cryptographic operations use stable keys across restarts and
across the servers of a web farm. Without it, ASP.NET auto-generates keys per process, so users are signed
out and encrypted values become unreadable after a restart or when a request lands on a different server.

Adxstudio Portals 7.x used validation method `SHA1` and encryption method `AES`; supply explicit keys of
appropriate length. IIS Manager can generate a machine key, or you can add one with a known generator.

## Azure App Service deployment

Azure App Service is supported for hosting the web application.

1. Publish `MasterPortal` to the App Service (Web Deploy, Zip Deploy, or from a build pipeline).
2. Set the platform to **64-bit**.
3. Set the .NET Framework version to **v4.8** (4.8.1 assemblies are compatible with the 4.8 runtime).
4. Enable **Always On** and **ARR affinity** as appropriate for your instance count.
5. Put application settings and the connection string in **Application settings** / **Connection strings**
   on the App Service, not in the deployed `Web.config`.
6. Ensure the search index and cache warmup directories are writable — App Service gives the site
   write access to its own content folder, but a scale-out plan with multiple instances means each
   instance has its own `App_Data`. Plan for search-index placement accordingly (see
   [Search](Search.md) and [Caching and Performance](Caching-and-Performance.md)).

`Samples/MasterPortal/AzureStartup/Startup.cmd` and `Startup.ps1` are retained from the Azure
Cloud Services era for reference; the supported Azure hosting model is App Service.

## Scale-out considerations

Running more than one instance changes the portal's behaviour:

- **Caches** are per-instance. Cache invalidation across instances is handled by the **portal bus** and
  the **Event Hub invalidation** subscriber configured in `Startup.cs`
  (`UsePortalBus<CacheInvalidationPortalBusMessage>` and `ConfigureEventHubCacheInvalidation`).
- **Search indexes** are per-instance files unless placed on shared storage or served by a single
  instance. Decide deliberately where the index lives.
- **Machine keys** must be identical on every instance.

See [Caching and Performance](Caching-and-Performance.md) for the mechanics.

## Multi-tenancy and multiple websites

One application instance can serve several `adx_website` records through **website bindings**. Requests
are matched to a website by host name at start-up, and language, settings, header/footer templates, search
provider and HSTS are resolved per website. Adding a host name means adding a binding record and, usually,
a binding on the web server.

## Post-deployment verification

Do not treat a successful publish as a working deployment. Verify through the real interface:

1. **Homepage** renders content from Dataverse (not the setup page, not a 500).
2. **`/_services/about`** returns product and build information.
3. **Authentication** — sign in with a portal contact, confirm the profile page, sign out, and confirm
   that signing out really clears the session.
4. **Authorization** — sign in as a user in a restricted web role and confirm restricted pages are denied
   *and* that a signed-out visitor sees the anonymous view, not the restricted content.
5. **Search** (if enabled) returns results, and rebuilding the index works.
6. **Forms and lists** render, submit and validate.
7. **Content changes** propagate — edit a web page in Dataverse and confirm the portal reflects it within
   the expected invalidation window.
8. **Static assets** load with correct content types, and no mixed-content or 404 errors appear in the
   browser console.
9. **Web notification endpoint** is reachable by Dataverse plugins, if you rely on push invalidation.

## Related pages

- [Configuration Reference](Configuration-Reference.md)
- [CRM Connection](CRM-Connection.md)
- [Upgrading and Migration](Upgrading-and-Migration.md)
- [Troubleshooting](Troubleshooting.md)
- [Security and Authentication](Security-and-Authentication.md)
