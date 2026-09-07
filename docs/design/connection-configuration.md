# CRM connection configuration

## Base solution selection

Set the `PortalBaseSolution` app setting to the base solution's unique name. For online environments using the legacy data model, use `<add key="PortalBaseSolution" value="MicrosoftPortalBase" />`. If the app setting is omitted or blank, it defaults to `MicrosoftCrmPortalBase`. Restart the application after changing this app setting.

## Client selection

Set the `OrganizationServiceType` app setting to `ServiceClient` for Dataverse, `CrmServiceClient` for Dynamics 365 on-premises, or `OrganizationServiceProxy` to explicitly retain the legacy client. If the setting is omitted or blank, the legacy client is used for backward compatibility. Add a connection string named `Xrm` using the chosen client's connection-string syntax. The application does not infer the client from the URL or fall back to another client after a connection failure.

## Connection examples

### Dataverse

Certificate authentication example:

```xml
<appSettings>
  <add key="OrganizationServiceType" value="ServiceClient" />
  <add key="PortalBaseSolution" value="MicrosoftPortalBase" />
</appSettings>
<connectionStrings>
  <add name="Xrm" connectionString="AuthType=Certificate;Url=https://organization.crm.dynamics.com;ClientId=YOUR-APP-ID;Thumbprint=YOUR-CERTIFICATE-THUMBPRINT" />
</connectionStrings>
```

Register an application in Microsoft Entra ID, upload the certificate, and create its Dataverse application user with the required security role. Install the certificate and private key where the portal process can access them. Alternatively, use `AuthType=ClientSecret;Url=...;ClientId=...;ClientSecret=...`.

### Dynamics 365 on-premises

Active Directory authentication example:

```xml
<appSettings>
  <add key="OrganizationServiceType" value="CrmServiceClient" />
</appSettings>
<connectionStrings>
  <add name="Xrm" connectionString="AuthType=AD;Url=https://crm.example.com/Organization;Domain=YOUR-DOMAIN;Username=YOUR-SERVICE-ACCOUNT;Password=YOUR-PASSWORD" />
</connectionStrings>
```

For IFD, use the corresponding `CrmServiceClient` authentication parameters.

## Connection behavior

Supply the organization base URL without the `/XRMServices/2011/Organization.svc` endpoint suffix. Configure these entries in the existing sections of `Web.config` or through the hosting platform. Protect secrets and escape XML special characters in configuration values. Configured connection strings are passed unchanged to a selected modern SDK client, so use that client's native connection-string parameters.

The existing `microsoft.xrm.client/connectionStrings` section remains supported and takes precedence over the standard `connectionStrings` section.

`CrmConnection` remains available for consumers. Connections created from configured `ConnectionStringSettings` retain their original connection string, which is authoritative when creating an SDK client. Their cache identity is a SHA-256 hash of the raw connection string. Parsed and object-initialized legacy connections continue to use the original proxy path. Token refresh is handled by the SDK clients for configured connection-string connections.

Connection behavior, including timeouts and proxy-type handling, is controlled by the selected SDK client's native connection-string options.

For supported connection-string parameters and authentication details, see [Microsoft's connection-string documentation](https://learn.microsoft.com/en-us/power-apps/developer/data-platform/xrm-tooling/use-connection-strings-xrm-tooling-connect) and [server-to-server authentication](https://learn.microsoft.com/en-us/power-apps/developer/data-platform/build-web-applications-server-server-s2s-authentication).
