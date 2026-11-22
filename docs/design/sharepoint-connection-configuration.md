# SharePoint connection configuration

Add a connection string named `SharePoint` to the existing `<connectionStrings>` section in the portal's `Web.config`. Use the setup for your SharePoint deployment.

## SharePoint Online

The self-hosted portal uses a Microsoft Entra application and certificate to access SharePoint Online. Register the app in the SharePoint tenant, upload the public certificate, and grant the **SharePoint** API application permission `Sites.Selected` with admin consent. Grant the app access to the specific SharePoint site. Use the `write` role for grids that upload, create folders or delete files. The grid can also create a folder when a record has no folder yet.

Install the certificate with its private key in the web server's **Local Computer → Personal** certificate store (`LocalMachine/My`). Give the IIS application pool identity read access to the private key. Add this entry to the existing `<connectionStrings>` section in the portal's `Web.config`.

```xml
<add name="SharePoint"
     connectionString="Url=https://contoso.sharepoint.com/sites/Portal;TenantId=TENANT-GUID;ClientId=APP-GUID;CertificateThumbprint=CERTIFICATE-THUMBPRINT;" />
```

For a national cloud, add `Authority=<cloud base URL>;` to the same connection string. The portal appends `TenantId` to that URL.

| Microsoft 365 cloud | `Authority` |
| --- | --- |
| Commercial and US Government GCC | `https://login.microsoftonline.com` (default) |
| US Government GCC High and DoD | `https://login.microsoftonline.us` |
| China operated by 21Vianet | `https://login.partner.microsoftonline.cn` |

Supplying Entra settings selects app-only authentication. Provide `TenantId`, `ClientId` and `CertificateThumbprint` together.

See [SharePoint Online app registration and certificate setup](sharepoint-online-app-registration.md) for the complete setup.

## SharePoint Server on-premises

Connections without Entra settings use Windows authentication. Supply a Windows account that has access to the document library.

```xml
<add name="SharePoint" connectionString="Url=https://sharepoint.contoso.com/sites/Portal;Domain=CONTOSO;UserName=portal-service;Password=YOUR_PASSWORD;" />
```

## Connection string keys

Keys are case-insensitive. This table lists the settings that affect authentication or requests.

| Key | Authentication | What it configures | Default when omitted |
| --- | --- | --- | --- |
| `Url` | Both | Absolute URL of the SharePoint site that contains the document library. Entra authentication requires HTTPS. | Required in the named `SharePoint` connection string. |
| `TenantId` | Entra ID | Microsoft Entra tenant ID as a GUID. | Unset. Required for Entra app-only authentication. |
| `ClientId` | Entra ID | Microsoft Entra application (client) ID as a GUID. | Unset. Required for Entra app-only authentication. |
| `CertificateThumbprint` | Entra ID | Thumbprint of the app certificate with a private key in `LocalMachine/My`. | Unset. Required for Entra app-only authentication. |
| `Authority` | Entra ID | Microsoft Entra cloud base URL. See [SharePoint Online](#sharepoint-online) for cloud values. | `https://login.microsoftonline.com`. |
| `Domain` | Windows | Windows domain for the supplied account. | Unset. |
| `UserName` | Windows | SharePoint Server account name. Supply it together with `Password`. Aliases are `User Name`, `UserId`, and `User Id`. | No explicit credentials. Windows-mode CSOM uses the process's default Windows credentials. Direct downloads have no credentials assigned. |
| `Password` | Windows | Password for the supplied `UserName`. | No explicit credentials if `UserName` is also omitted. An incomplete username and password pair produces a configuration error. |
| `ApplicationName` | Both | Application name reported by CSOM. Also accepted as `Application Name`. | `.NET Library`. |
| `RequestTimeout` | Both | Timeout in milliseconds for CSOM and direct downloads. Aliases are `Request Timeout` and `Timeout`. | `180000` for CSOM requests and `100000` for direct downloads. |
| `ValidateOnClient` | Both | Whether CSOM validates parameters on the client. Values are `true` and `false`. Also accepted as `Validate On Client`. | `true`. |
