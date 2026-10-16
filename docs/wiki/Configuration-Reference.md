# Configuration Reference

Everything in `Samples/MasterPortal/Web.config`, explained. Values shown come from the repository's
sample configuration; replace organization-specific values for your environment.

There are three configuration layers:

1. **`Web.config`** — app settings, connection strings, provider registrations, handlers, binding redirects.
2. **`Web.config` configuration sections** — `microsoft.xrm.client`, `microsoft.xrm.portal`, `adxstudio.xrm`.
3. **Portal site settings in Dataverse** — `adx_sitesetting` records read at runtime. See
   [Site Settings Reference](Site-Settings-Reference.md).

---

## Registered configuration sections

```xml
<configSections>
  <section name="system.identityModel" type="System.IdentityModel.Configuration.SystemIdentityModelSection, System.IdentityModel, Version=4.0.0.0, Culture=neutral, PublicKeyToken=B77A5C561934E089" />
  <section name="microsoft.xrm.client" type="Microsoft.Xrm.Client.Configuration.CrmSection, Microsoft.Xrm.Client" />
  <section name="microsoft.xrm.portal" type="Microsoft.Xrm.Portal.Configuration.PortalCrmSection, Microsoft.Xrm.Portal" />
  <section name="adxstudio.xrm" type="Adxstudio.Xrm.Configuration.CrmSection, Adxstudio.Xrm" />
</configSections>
```

---

## Application settings

### Connection and client selection

| Key | Values | Purpose |
|---|---|---|
| `OrganizationServiceType` | `OrganizationServiceProxy` \| `ServiceClient` \| `CrmServiceClient` | Selects the SDK client. Omitted/blank = legacy proxy. See [CRM Connection](CRM-Connection.md). |
| `PortalBaseSolution` | `MicrosoftCrmPortalBase` \| `MicrosoftPortalBase` | Base solution unique name. Omitted/blank = `MicrosoftCrmPortalBase`. Restart required after change. |
| `PortalConfigType` | `online` \| `onprem` \| `azure` | Hosting/connection mode. Any other value is treated as `online`. When `online` **and** no connection information exists, the connection-settings page is suppressed and you get a "The connection is undefined" error instead. |
| `OnPremCertificateThumbprint` | thumbprint | Certificate thumbprint for on-premises connections, when applicable. |
| `EncryptIndex` | `true` \| `false` | Whether the Lucene search index is encrypted. |

### OWIN and ASP.NET

| Key | Purpose |
|---|---|
| `owin:AppStartup` | Names the OWIN startup class (`Site.Startup,Site`). |
| `webpages:Version` | ASP.NET Web Pages assembly version. |
| `webpages:Enabled` | Enables/disables Web Pages. |
| `ClientValidationEnabled` | Enables unobtrusive client validation. |
| `UnobtrusiveJavaScriptEnabled` | Enables unobtrusive JavaScript. |
| `ValidationSettings:UnobtrusiveValidationMode` | Set to `None` so Web Forms validation uses the classic model. |
| `aspnet:MaxJsonDeserializerMembers` | Raises the JSON deserializer member limit (set to `150000`). |

### Portal behaviour and diagnostics

| Key | Purpose |
|---|---|
| `PortalWriteToDiagnosticsTrace` | Writes portal trace output to the ASP.NET diagnostic trace. |
| `PortalAllowStaleData` | Allows serving cached (stale) data when Dataverse is slow or unavailable. |
| `PortalWebProxyClientFailover` | Enables failover to the web proxy client. |
| `IgnoreUserAgent` | Comma-separated user agents excluded from telemetry (health probes, warmup, monitoring). |
| `IgnorePath` | Comma-separated paths excluded from telemetry. |
| `TelemetryFileTypes` | File extensions considered "page" telemetry. |
| `PortalSetupInProgress` | Set by the provisioning flow; drives the `Provisioning` view. |
| `PortalPackageName` | The website/package id used by the provisioning flow. |

### Authentication and Azure integration

| Key | Purpose |
|---|---|
| `Azure.Authentication.ClientId` | Entra ID application (client) id. |
| `Azure.Authentication.TenantId` | Entra ID tenant id. |
| `Azure.Authentication.RootUrl` | Issuer root (for example `https://login.microsoftonline.com/`). |
| `Azure.Authentication.RedirectUri` | Redirect URI; **rewritten at runtime** to the current host plus `/signin-oidc`. |
| `Azure.Authentication.Caption` | Display text for the sign-in button. |
| `Azure.Certificate.ThumbprintPrimary` | Thumbprint of the chat signing certificate in the app pool's `CurrentUser/My` store. |

### Commerce legacy settings

| Key | Purpose |
|---|---|
| `ApiLogin`, `TransactionKey`, `MerchantHash` | Legacy Authorize.Net credentials placeholders. Prefer site settings and a secret store for real values; **do not commit real credentials**. |

> **Security.** Do not store real secrets in `Web.config` under source control. Use configuration
> transforms (`Web.Release.config`), the hosting platform's settings, or an external secret store.

---

## `<location>` scoping

`Web.config` wraps most of its content in `<location path="." inheritInChildApplications="false">`, and
provides separate locations for the reserved resource paths:

```xml
<location path="xrm" inheritInChildApplications="false"> ... </location>
<location path="xrm-adx" inheritInChildApplications="false"> ... </location>
```

These exist so the embedded-resource paths under `/xrm` and `/xrm-adx` get their own compression
settings and are never inherited by child applications.

---

## `<system.web>` highlights

### Expression builders

```xml
<expressionBuilders>
  <add expressionPrefix="Snippet" type="Adxstudio.Xrm.Web.Compilation.SnippetExpressionBuilder, Adxstudio.Xrm" />
  <add expressionPrefix="SiteSetting" type="Adxstudio.Xrm.Web.Compilation.SiteSettingExpressionBuilder, Adxstudio.Xrm" />
  <add expressionPrefix="CrmSiteMap" type="Microsoft.Xrm.Portal.Web.Compilation.CrmSiteMapExpressionBuilder, Microsoft.Xrm.Portal" />
  <add expressionPrefix="Context" type="Microsoft.Xrm.Portal.Web.Compilation.PortalContextExpressionBuilder, Microsoft.Xrm.Portal" />
  <add expressionPrefix="ResourceManager" type="Adxstudio.Xrm.Web.Compilation.ResourceManagerExpressionBuilder, Adxstudio.Xrm" />
</expressionBuilders>
```

These let ASP.NET markup resolve portal values with `<%$ Snippet:Name %>`,
`<%$ SiteSetting:Setting/Name %>`, `<%$ CrmSiteMap:... %>`, `<%$ Context:... %>` and
`<%$ ResourceManager:Key %>`.

### Custom errors

```xml
<customErrors mode="RemoteOnly" redirectMode="ResponseRewrite">
  <error statusCode="500" redirect="~/500.aspx" />
  <error statusCode="404" redirect="~/404.aspx" />
  <error statusCode="503" redirect="~/CrmConnectionError.aspx" />
</customErrors>
```

HTTP 503 maps to the CRM connection error page — that is the page users see when the Dataverse
connection fails.

### Trace

`<trace enabled="true" requestLimit="100" mostRecent="true" pageOutput="false" localOnly="true" />`
keeps a local request trace buffer. Combine with the `PortalWriteToDiagnosticsTrace` app setting for
server-side diagnosis.

### Providers

| Provider | Registration | Role |
|---|---|---|
| **Membership** | `defaultProvider="Xrm"` with all providers cleared | The portal does not use ASP.NET membership; Identity handles users. |
| **Role manager** | `CrmContactAccountRoleProvider` | Exposes portal roles derived from Contacts/Accounts. |
| **Site map** | `ContentMapCrmSiteMapProvider` (default), plus `ForumSiteMapProvider`, `EventSiteMapProvider`, `BlogSiteMapProvider` | Supplies the content map and the forum/blog/event page trees, with security trimming enabled. |
| **Profile** | `CrmContactProfileProvider` with a `FullName` property mapped to `fullname` | Stores simple profile values on the contact. |
| **Output cache** | `ObjectCacheOutputCacheProvider` (named `Xrm`) | Backs output caching with the portal object cache. |

### Output-cache profiles

```xml
<outputCacheProfiles>
  <add name="Roles" duration="86400" varyByParam="*" varyByCustom="roles;language" varyByContentEncoding="gzip;deflate" location="Server" />
  <add name="RolesShared" duration="86400" varyByParam="none" varyByCustom="roles;website;language" varyByContentEncoding="gzip;deflate" location="Server" />
  <add name="User" duration="86400" varyByParam="*" varyByCustom="user;language" varyByContentEncoding="gzip;deflate" location="Server" />
  <add name="UserShared" duration="86400" varyByParam="none" varyByCustom="user;website;language" varyByContentEncoding="gzip;deflate" location="Server" />
</outputCacheProfiles>
```

The `varyByCustom` keys are implemented in `Global.asax.cs::GetVaryByCustomString`. Recognised keys:
`roles`, `user`, `website`, `culture`, and the `language` and combined forms (`roles;language`,
`roles;website`, `user;website`, `website;language`, `user;language`, and the `...;website;language`
variants).

**Pages that emit anti-forgery tokens must not use these profiles** — see
[Security and Authentication](Security-and-Authentication.md#anti-forgery-and-output-caching).

### Authentication and session

Authentication is not configured through a `<system.web>` `<authentication>` element — there is none in
the sample. It is handled entirely by ASP.NET Identity over OWIN, started by the `owin:AppStartup` app
setting and configured in `Startup.Auth.cs`.

```xml
<httpCookies httpOnlyCookies="true" />
<anonymousIdentification enabled="true" cookieName="Dynamics365PortalAnalytics" cookieTimeout="129600" cookiePath="/" cookieRequireSSL="false" cookieSlidingExpiration="true" cookieProtection="All" cookieless="UseCookies" />
```

Set `cookieRequireSSL="true"` for production HTTPS sites. The `Dynamics365PortalAnalytics` cookie is the
anonymous-profile cookie.

### Other notable settings

| Setting | Purpose |
|---|---|
| `<httpRuntime maxRequestLength="102400" requestValidationMode="2.0" encoderType="System.Web.Security.AntiXss.AntiXssEncoder" enableVersionHeader="false" />` | 100 MB upload limit, AntiXSS-performed output encoding, no `X-AspNet-Version` header. |
| `<pages viewStateEncryptionMode="Always" validateRequest="true">` | Encrypted view state and request validation on. |
| `<compilation targetFramework="4.8.1">` | Target framework. Set `debug="false"` for production. |
| `<sessionState>` | Not configured in the sample; ASP.NET in-process defaults apply. |

---

## `<system.webServer>`

### Modules and handlers

```xml
<modules runAllManagedModulesForAllRequests="true">
  <add name="CookieUpdateSecure" preCondition="managedHandler" type="Adxstudio.Xrm.Web.Modules.UpdateCookieModule, Adxstudio.Xrm" />
</modules>
<handlers>
  <add name="WebNotification" verb="*" path="WebNotification.axd" preCondition="integratedMode" type="Adxstudio.Xrm.Web.Handlers.WebNotificationHandler, Adxstudio.Xrm" />
  <add name="PaymentProcessing" verb="*" path="/PaymentProcessing.axd" type="Adxstudio.Xrm.Commerce.PaymentProcessingRequestHandlerFactory" />
  <add name="Diag" verb="*" path="Diag.axd" preCondition="integratedMode" type="Adxstudio.Xrm.Web.Handlers.DiagnosticsHandler, Adxstudio.Xrm" />
  <add name="Telerik_Web_UI_WebResource_axd" path="Telerik.Web.UI.WebResource.axd" type="Telerik.Web.UI.WebResourceSession" verb="*" preCondition="integratedMode" />
</handlers>
```

`UpdateCookieModule` reapplies the secure flag to cookies after they are issued. `WebNotification.axd`
receives cache-invalidation notifications from Dataverse; `Diag.axd` is the diagnostics endpoint;
`PaymentProcessing.axd` receives payment-provider callbacks.

### Static content, compression and errors

| Setting | Purpose |
|---|---|
| `<staticContent>` | 7-day client cache, plus a `.woff2` MIME type registration. |
| `<urlCompression doStaticCompression="true" doDynamicCompression="true" dynamicCompressionBeforeCache="true" />` | Gzip/brotli compression. |
| `<httpErrors errorMode="DetailedLocalOnly" existingResponse="PassThrough" />` | Local requests see detailed errors; portal responses pass through. |
| `<applicationInitialization doAppInitAfterRestart="true"><add initializationPage="/" /></applicationInitialization>` | Warms the application after an app-pool restart. |
| `<httpProtocol><customHeaders><remove name="X-Powered-By" /></customHeaders></httpProtocol>` | Removes the `X-Powered-By` header. |

---

## `microsoft.xrm.client` section

See [CRM Connection](CRM-Connection.md#service-layer-configuration) for the full block. It configures:

- `mutexProviderType` — `MonitorLockProvider` for cross-thread locking.
- `contexts` — the `CrmOrganizationServiceContext` used for typed queries.
- `services` — the organization service implementation.
- `serviceCache` — the nested cache chain (`PortalBusOrganizationServiceCache` →
  `ContentMapOrganizationServiceCache`).
- `objectCache` — `OutputObjectCache` with `physicalMemoryLimitPercentage="70"`.

---

## `microsoft.xrm.portal` section

Configures the portal context, its dependency provider, its security provider, and the cache policy for
embedded resources and annotations. See [CRM Connection](CRM-Connection.md#portal-context-configuration).

The `cachePolicy` values are used by the cached embedded-resource and annotation file providers.

---

## `adxstudio.xrm` section

Declared in `Web.config` but not populated in the sample. When present it can configure:

| Element | Purpose |
|---|---|
| `sectionProviderType` | The configuration provider type. Default `Adxstudio.Xrm.Configuration.AdxstudioCrmConfigurationProvider, Adxstudio.Xrm`. |
| `contentLicensesEnabled` | Whether CRM-stored content licenses are loaded. Default `true`. |
| `cacheFeed` | The cache feed handler configuration. |
| `events` | A collection of organization-service event elements (outbound event hooks). |
| `search` | The search provider configuration (see [Search](Search.md)). |
| `outputObjectCacheName` | Name of the object cache the output-cache provider manages. Default `adxstudio.xrm`. |
| `cdnEnabled` | Enables Azure CDN URL routing rules. Default `true`. |
| `contentMap` | Content map configuration element. |

A `<cacheFeed>` becomes reachable at the `CacheFeedHandler` endpoint; `cdnEnabled` should be set to
`false` when you are not using an Azure CDN, otherwise CDN URL rules are applied to resource URLs.

---

## `system.diagnostics`

Eight trace sources are wired to the ASP.NET `WebPageTraceListener`, all at `Error` level by default:

`Microsoft.IdentityModel.Clients.ActiveDirectory`, `Framework`, `ADXTrace`, `PortalFeatureTrace`,
`PortalPerformance`, `PortalWeb`, `PortalServices`, `PortalCache`.

Raise a source's `switchValue` (for example to `Verbose` or `Information`) to diagnose that subsystem in
the trace output. See [Diagnostics and Logging](Diagnostics-and-Logging.md).

---

## `system.net` / `<mailSettings>`

The sample routes mail to `127.0.0.1` with a `donotreply@contoso.com` sender. Configure a real SMTP host
before enabling email confirmation, invitations or notifications.

---

## `<runtime>` binding redirects

The file contains ~80 assembly binding redirects that align the many assemblies in the deployment —
CRM SDK, OData/EDM, OWIN, Identity, Newtonsoft.Json, Microsoft.Identity.Client, the `Microsoft.Extensions.*`
stack, and the `System.*` support assemblies. These are generated and maintained alongside
`packages.config`; when you update a package, update the matching redirect (and keep the
SharpZipLib guard in mind).

---

## Production checklist

1. `<compilation debug="false" />`.
2. `cookieRequireSSL="true"` on `<anonymousIdentification>` and secure auth cookies.
3. Add a `<machineKey>` so encryption is stable across restarts and servers.
4. Replace all placeholder credentials; move secrets out of source control.
5. Set `cdnEnabled="false"` unless you actually use an Azure CDN.
6. Confirm `httpCookies httpOnlyCookies="true"` is retained.
7. Verify write access to `App_Data` (search index and cache warmup).
8. Set `IgnoreUserAgent` / `IgnorePath` to match your monitoring probes.

## Related pages

- [CRM Connection](CRM-Connection.md)
- [Site Settings Reference](Site-Settings-Reference.md)
- [Deployment](Deployment.md)
- [Diagnostics and Logging](Diagnostics-and-Logging.md)
