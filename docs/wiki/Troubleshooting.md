# Troubleshooting

Diagnosis steps for the failures people actually hit. Most issues fall into one of five buckets:
connection, caching, permissions, markup/templating, or authentication.

## First moves

Whatever the symptom, gather these before guessing:

1. **Turn on tracing.** Set `PortalWriteToDiagnosticsTrace` to `true` and raise the relevant
   `<system.diagnostics>` source from `Error` to `Verbose` — `ADXTrace`, `PortalWeb`, `PortalServices`,
   `PortalCache`, `PortalFeatureTrace`, `PortalPerformance`. See
   [Diagnostics and Logging](Diagnostics-and-Logging.md).
2. **Look at `trace.axd`** (local requests only, per the `<trace>` settings) and the Windows application
   event log.
3. **Check the custom error pages.** `500.aspx`, `404.aspx` and `CrmConnectionError.aspx` map to
   `~/500.aspx`, `~/404.aspx` and `~/CrmConnectionError.aspx`. An HTTP 503 means the Dataverse connection
   failed.
4. **Hit `/_services/about`** — if it fails, the problem is at application start-up, not in content.
5. **Reproduce with a fresh application pool** so you are not fighting stale cache.

---

## The site shows a connection settings page instead of the portal

**Cause:** the application is unconfigured and has entered setup mode.

`SetupConfig.InitialSetupIsRunning()` returns `true` when there is no `Xrm` connection string **and** no
setup record **and** setup is enabled. `Application_Start` then registers the setup route and returns
before registering portal routes.

**Fix:** supply an `Xrm` connection string (or complete the setup wizard). See
[CRM Connection](CRM-Connection.md). Note the `PortalConfigType` interaction: if it is `online` and no
connection information exists anywhere, you get "The connection is undefined" instead of the settings page.

---

## "The connection is undefined"

Thrown as a `ConfigurationErrorsException` from `SetupConfig.GetConnectionString()` when
`SetupManager.GetConnectionString()` returns null.

**Check:**

- Is there a `<add name="Xrm" ...>` in `<connectionStrings>`?
- Is there one in the `microsoft.xrm.client/connectionStrings` section? (It **takes precedence**.)
- Is `PortalConfigType` set to `online` while no connection information exists? That combination
  suppresses the settings page.

---

## HTTP 503 / `CrmConnectionError.aspx`

The portal could not talk to Dataverse.

**Check, in order:**

1. **Client selection.** Does `OrganizationServiceType` match the connection string syntax? A
   `ServiceClient` connection string with `CrmServiceClient` selected (or vice versa) fails. The portal
   does **not** infer the client and does **not** fall back.
2. **URL shape.** Requirement: the organization base URL **without**
   `/XRMServices/2011/Organization.svc`.
3. **Credentials.** For `ServiceClient` with `AuthType=Certificate`, is the certificate installed where
   the process can read it, with its private key, and is its thumbprint correct? For
   `AuthType=ClientSecret`, is the secret current?
4. **Application user.** Does the registered application have a Dataverse application user with an
   appropriate security role?
5. **Outbound network.** Can the server reach the organization URL over TLS 1.2+? Check a proxy or
   firewall.
6. **TLS.** Outbound TLS 1.2 must be enabled.

---

## Start-up loops or the application restarts constantly

`Startup.ConfigurePortal` restarts the web application when website resolution throws
(`Adxstudio.Xrm.Web.Extensions.RestartWebApplication()`), so a persistent content problem appears as an
endless restart cycle.

**Likely causes:**

- No `adx_website` record matches the request host — check website bindings.
- The base solution is missing or mismatched with `PortalBaseSolution`.
- Dataverse is reachable but rejects the request (permissions, missing tables).

Raise `ADXTrace` and `PortalWeb` to `Verbose` and look at the start-up trace.

---

## Content changes do not appear

This is almost always caching. Invalidation arrives from one of three sources:

1. **Web notifications** from Dataverse plugins hitting `WebNotification.axd`.
2. **Portal bus** messages between instances.
3. **Event Hub invalidation** subscriptions configured in `Startup.cs`.

**Check:**

- Is the `Adxstudio.Xrm.Plugins.WebNotification` plugin assembly registered and enabled in Dataverse?
- Is `WebNotification.axd` reachable from Dataverse (network, path, and no authentication blocking it)?
- If you run multiple instances, is the portal bus / Event Hub configured, or is the change landing on
  another instance's cache?
- Is `PortalAllowStaleData` set to `true`? That deliberately serves stale data when Dataverse is slow.

**Immediate workaround:** recycle the application pool, or restart the application, to clear caches.

See [Caching and Performance](Caching-and-Performance.md).

---

## The anti-forgery error on sign-in

Symptoms:

- `The anti-forgery cookie token and form field token do not match.`
- `The required anti-forgery cookie "__RequestVerificationToken" is not present.`

**Cause (fixed in this branch):** when a visitor who already had the anti-forgery cookie loaded the
sign-in page, the response did not include a fresh `__RequestVerificationToken` cookie, so the page was
output cached and then served to later visitors, who got the first visitor's token with no matching
cookie.

**What to do:**

- Make sure you are on a build that includes the fix (the sign-in page is no longer output cached).
- Confirm no page that emits an anti-forgery token uses a `Roles`/`User` output-cache profile. These pages
  must not be output cached, because the token is per-session.
- Clear the output cache after deploying the fix.

See [Security and Authentication](Security-and-Authentication.md#anti-forgery-and-output-caching).

---

## Search returns no results

**Check, in order:**

1. Is `Search/Enabled` set to `true` in site settings? If search is disabled the provider throws and the
   search UI shows nothing.
2. Does the index exist? It is written to `App_Data/Adxstudio.Xrm.Search` (unless redirected by the
   `Adxstudio.Xrm.Search.Index.DirectoryPath` setting).
3. Did the index build succeed? Rebuilds fail if Dataverse rejects a query. In environments that also have
   the Power Pages **enhanced data model**, the indexer used to fail because it picked up the
   `mspp_*` "Portal Search" views and asked them for `modifiedon`. This branch skips views on `mspp_*`
   tables — if you are on an older build, that is the fix.
4. Are the indexed tables actually permitted to the portal's Dataverse user?

See [Search](Search.md).

---

## Uploads fail, or attachments and web files do not work

**Check:**

1. **Max request length.** `<httpRuntime maxRequestLength="102400" />` caps uploads at 100 MB. Increase it
   if your files are larger.
2. **Storage location.** `WebFiles/StorageLocation` selects where web files are stored. If it is cloud
   storage, `WebFiles/CloudStorageAccount` must be configured.
3. **Blob address format.** Fully qualified blob addresses are supported in this branch; older builds
   mishandled them. Both relative and fully qualified `adx_cloudblobaddress` values work now.
4. **`WebFiles/MaxFileSizeErrorMessage`** is the message shown when the configured maximum is exceeded —
   if users see the wrong message, that setting is unset or wrong.
5. **Note attachments** — deleting or editing a note threw `NullReferenceException` on older builds in the
   notes/timeline controls. That is fixed here.

See [Web Files and Attachments](Web-Files-and-Attachments.md).

---

## Liquid renders nothing, or prints an error message

Liquid parse errors are written **into the page** as text rather than thrown: `InternalRenderLiquid`
catches `SyntaxException` and writes `e.Message` to the output. Rendering errors are logged to `ADXTrace`
as `Liquid rendering error`.

**Check:**

- Look for a trailing-colon message in the rendered HTML — that is DotLiquid telling you the syntax is
  wrong.
- Raise `ADXTrace` to `Verbose` to see parse and render errors with the template identifier.
- Confirm the template language is as expected: DotLiquid uses an **invariant-culture naming convention**
  here (`InvariantCultureNamingConvention`), so member names are lowercased with underscores. A template
  written for a different naming convention may not resolve members.
- Check whether `Liquid/Enabled` is set to `false` — when it is, the template source is written out
  verbatim instead of being rendered. That looks like "Liquid is not working" but is intentional.

See [Web Templates and Liquid](Web-Templates-and-Liquid.md).

---

## A page is blank or 404s though the web page exists

**Check:**

1. **Publishing state.** Draft or unpublished pages are not in the content map. Publish the page.
2. **Parent chain.** Every ancestor must be published and have a partial URL that builds the expected
   path.
3. **Hidden from sitemap / partial URL.** A page marked hidden or with a duplicate or empty partial URL
   may not route.
4. **Web page access control rules.** A rule can deny Read, producing an access-denied page or a 404-like
   result.
5. **Stale content map.** After content changes, the content map is invalidated; if invalidation is not
   working, recycle the pool.

See [Content Management](Content-Management.md) and
[Security and Authentication](Security-and-Authentication.md).

---

## Build failures

| Symptom | Cause and fix |
|---|---|
| Build errors about SharpZipLib | The guard in `Build/BlockSharpZipLib.targets` fired. Lucene.Net reintroduced SharpZipLib (a version below 1.4.2). Remove it, or deliberately reference 1.4.2+ and remove the guard as described in the target file. |
| Missing packages / types | Run the restore with the repository's config file: `nuget restore Solutions\Portals\Portals.sln -ConfigFile Solutions\Portals\.nuget\NuGet.Config`. |
| Version-conflict or binding errors at runtime after updating a package | Update the matching `<bindingRedirect>` in `Web.config`. |
| `The type or namespace name 'Site' ...` | Build the whole `Portals` solution, not an individual project, so project references resolve. |
| `.NET Framework 4.8.1 targeting pack` errors | Install the .NET Framework 4.8.1 developer pack / development tools. |

---

## IIS Express will not start

`Samples/StartExpressPortal.cmd` launches IIS Express on port **7800**. If it fails:

- Close any IIS Express instance in the system tray.
- Change the port in `Samples/applicationHost.config` (`*:7800:localhost`).
- Run from an elevated-enough prompt, or use Visual Studio's own IIS Express instead.

---

## Related pages

- [Diagnostics and Logging](Diagnostics-and-Logging.md)
- [Caching and Performance](Caching-and-Performance.md)
- [CRM Connection](CRM-Connection.md)
- [Deployment](Deployment.md)
- [Search](Search.md)
