# CRM Connection

The portal talks to **Dataverse** (or an on-premises Dynamics 365 organization) through the organization
service. This page covers how that connection is selected and configured.

## The two app settings that matter

Both live in `<appSettings>` in `Samples/MasterPortal/Web.config`.

### `OrganizationServiceType`

Selects which SDK client the portal uses.

| Value | Client | Use for |
|---|---|---|
| `ServiceClient` | `Microsoft.PowerPlatform.Dataverse.Client.ServiceClient` | **Dataverse / online.** The modern client, including certificate and client-secret auth. |
| `CrmServiceClient` | `Microsoft.Xrm.Tooling.Connector.CrmServiceClient` | **Dynamics 365 on-premises**, including IFD. |
| `OrganizationServiceProxy` | `Microsoft.Xrm.Sdk.Client.OrganizationServiceProxy` | The legacy client. Used when the setting is **omitted or blank**. |

The application does **not** infer the client from the URL and does **not** fall back to another client
after a connection failure. You must set this explicitly.

### `PortalBaseSolution`

Selects the base portal solution's unique name, used for base-solution detection and schema-version
filtering.

| Value | Meaning |
|---|---|
| `MicrosoftCrmPortalBase` | Default when the setting is omitted or blank. |
| `MicrosoftPortalBase` | Online environments using the legacy data model. |

**Restart the application after changing this setting.**

## The `Xrm` connection string

Add a connection string named `Xrm` using the syntax of the client you selected. Supply the organization
**base URL without the `/XRMServices/2011/Organization.svc` suffix**.

```xml
<connectionStrings>
  <add name="Xrm" connectionString="AuthType=ClientSecret;Url=https://organization.crm.dynamics.com;ClientId=YOUR-APP-ID;ClientSecret=YOUR-SECRET;TenantId=YOUR-TENANT-ID" />
</connectionStrings>
```

### Dataverse (online)

**Certificate authentication** (recommended for production):

```xml
<appSettings>
  <add key="OrganizationServiceType" value="ServiceClient" />
  <add key="PortalBaseSolution" value="MicrosoftPortalBase" />
</appSettings>
<connectionStrings>
  <add name="Xrm" connectionString="AuthType=Certificate;Url=https://organization.crm.dynamics.com;ClientId=YOUR-APP-ID;Thumbprint=YOUR-CERTIFICATE-THUMBPRINT" />
</connectionStrings>
```

To use this you must:

1. Register an application in Microsoft Entra ID.
2. Upload the certificate to the application registration.
3. Create a Dataverse **application user** for the application and assign it the security role the
   portal needs.
4. Install the certificate and private key where the portal process can read it (for example the
   `CurrentUser\My` or `LocalMachine\My` store).

**Client secret** — same shape with `AuthType=ClientSecret;...;ClientSecret=...`.

### On-premises

Active Directory authentication:

```xml
<appSettings>
  <add key="OrganizationServiceType" value="CrmServiceClient" />
</appSettings>
<connectionStrings>
  <add name="Xrm" connectionString="AuthType=AD;Url=https://crm.example.com/Organization;Domain=YOUR-DOMAIN;Username=YOUR-SERVICE-ACCOUNT;Password=YOUR-PASSWORD" />
</connectionStrings>
```

For IFD, use the corresponding `CrmServiceClient` authentication parameters.

### Connection-string placement

A connection string can be supplied in two places:

1. The standard `<connectionStrings>` section — the normal choice.
2. The `microsoft.xrm.client/connectionStrings` section of the `microsoft.xrm.client` configuration
   section.

The **`microsoft.xrm.client` section takes precedence** over the standard section. If you set both, the
one in the CRM configuration section wins.

## How the connection is created

- `OrganizationServiceType` is read by the configuration provider and selects which client is
  constructed for the `Xrm` organization service.
- Connections created from configured `ConnectionStringSettings` retain their **original connection
  string**, which is authoritative when creating an SDK client. Their cache identity is a SHA-256 hash of
  the raw connection string, so changing the string invalidates cached data keyed to the old string.
- Token refresh is handled by the SDK client for configured connection-string connections.
- Connection behaviour such as timeouts and proxy-type handling is controlled by the selected SDK
  client's native connection-string options.

For the full list of supported parameters, see Microsoft's documentation on
[connection strings for XRM tooling](https://learn.microsoft.com/en-us/power-apps/developer/data-platform/xrm-tooling/use-connection-strings-xrm-tooling-connect)
and [server-to-server authentication](https://learn.microsoft.com/en-us/power-apps/developer/data-platform/build-web-applications-server-server-s2s-authentication).

## Service-layer configuration

The connection is wrapped by the `microsoft.xrm.client` configuration section in `Web.config`:

```xml
<microsoft.xrm.client mutexProviderType="Adxstudio.Xrm.Threading.MonitorLockProvider, Adxstudio.Xrm">
  <contexts default="Xrm">
    <add name="Xrm" type="Microsoft.Xrm.Client.CrmOrganizationServiceContext, Microsoft.Xrm.Client" />
  </contexts>
  <services default="Xrm">
    <add name="Xrm" type="Adxstudio.Xrm.Services.CrmOnlineOrganizationService, Adxstudio.Xrm" />
  </services>
  <serviceCache default="Xrm">
    <add name="Xrm" type="Adxstudio.Xrm.Services.PortalBusOrganizationServiceCache, Adxstudio.Xrm" innerServiceCacheName="Inner" />
    <add name="Inner" type="Adxstudio.Xrm.Services.ContentMapOrganizationServiceCache, Adxstudio.Xrm" />
  </serviceCache>
  <objectCache default="Xrm">
    <add name="Xrm" type="Adxstudio.Xrm.Caching.OutputObjectCache, Adxstudio.Xrm" physicalMemoryLimitPercentage="70" />
  </objectCache>
</microsoft.xrm.client>
```

This is what layers caching and portal-bus invalidation over the raw client. See
[Caching and Performance](Caching-and-Performance.md).

## Portal context configuration

`<microsoft.xrm.portal>` registers the portal context and the security provider:

```xml
<microsoft.xrm.portal>
  <portals>
    <add name="Xrm" type="Adxstudio.Xrm.PortalContext, Adxstudio.Xrm">
      <dependencyProvider type="Adxstudio.Xrm.Web.Configuration.DependencyProvider, Adxstudio.Xrm" />
      <crmEntitySecurityProvider type="Adxstudio.Xrm.Cms.Security.CmsCrmEntitySecurityProvider, Adxstudio.Xrm" />
    </add>
  </portals>
  <cachePolicy>
    <embeddedResource maxAge="7.00:00:00" />
    <annotation maxAge="01:00:00" />
  </cachePolicy>
</microsoft.xrm.portal>
```

`DependencyProvider` supplies the data adapters used across the portal; `CmsCrmEntitySecurityProvider` is
the default security provider that evaluates table permissions and web page access control rules. Replacing
either is the main seam for deep customization — see [Extensibility](Extensibility.md).

## Multiple websites in one environment

A single application can serve several `adx_website` records. Website resolution at start-up
(`websiteManager.Find(...)`) picks the website for the request host, using website bindings. Per-website
behaviour — languages, settings, search provider, header settings, HSTS — is configured from the resolved
website, not globally. If no website matches the request, OWIN restarts the web application.

## Verifying the connection

Signs that the connection is healthy:

- The portal renders Dataverse content instead of the setup page.
- `_services/about` reports product information.
- The Windows application event log has no organization-service errors on start-up.

Signs of connection trouble:

- The site shows `CrmConnectionError.aspx` (HTTP 503 custom error).
- Start-up logs an organization-service exception and restarts the application.
- Every request costs a full round trip because caching never warms.

See [Troubleshooting](Troubleshooting.md) for diagnosis steps.

## Security notes

- **Never commit real connection strings or client secrets.** Prefer configuration transforms, deployment
  platform settings, or an external secret store, and keep production values out of source control.
- Escape XML-special characters in configuration values.
- The portal caches aggressively; a wrong connection string can appear to "work" briefly from cache after
  being changed. Restart the application after changing connection configuration.

## Related pages

- [Configuration Reference](Configuration-Reference.md) — all app settings and config sections
- [Deployment](Deployment.md) — environment-specific configuration
- [Security and Authentication](Security-and-Authentication.md) — portal-side auth, distinct from the CRM connection
