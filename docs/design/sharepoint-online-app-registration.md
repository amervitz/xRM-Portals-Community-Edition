# SharePoint Online app registration and certificate setup

Create the certificate on the portal host and register a single-tenant Microsoft Entra app in the tenant that owns the SharePoint site.

## Create the certificate

Run PowerShell as an administrator on the portal host to create a non-exportable certificate and export its public certificate for the Entra app registration. Set the expiration date according to your organization's certificate policy.

```powershell
$expires = (Get-Date).AddYears(1)
$certificate = New-SelfSignedCertificate `
    -Subject 'CN=xRM Portals SharePoint Integration' `
    -CertStoreLocation 'Cert:\LocalMachine\My' `
    -Provider 'Microsoft Software Key Storage Provider' `
    -Type Custom -KeyAlgorithm RSA -KeyLength 2048 `
    -HashAlgorithm SHA256 -KeyUsage DigitalSignature `
    -KeyExportPolicy NonExportable -NotAfter $expires
Export-Certificate -Cert $certificate -FilePath '.\SharePointPortal.cer'
$certificate.Thumbprint
```

## Grant the portal access to the private key

Run this in the same elevated PowerShell session. Set `$identity` to the account that runs the portal. The example uses an IIS application pool named `MasterPortal`. Use the recorded thumbprint to load the certificate from `Cert:\LocalMachine\My` if you start a new session.

```powershell
$identity = 'IIS AppPool\MasterPortal'
# Use the following when IIS Express runs as your current account.
# $identity = [System.Security.Principal.WindowsIdentity]::GetCurrent().Name

$rsa = [System.Security.Cryptography.X509Certificates.RSACertificateExtensions]::GetRSAPrivateKey($certificate)
try {
    $keyPath = Join-Path "$env:ProgramData\Microsoft\Crypto\Keys" $rsa.Key.UniqueName
    $acl = Get-Acl -LiteralPath $keyPath
    $rule = [System.Security.AccessControl.FileSystemAccessRule]::new($identity, 'Read', 'Allow')
    $acl.AddAccessRule($rule)
    Set-Acl -LiteralPath $keyPath -AclObject $acl
} finally {
    $rsa.Dispose()
}
```

For a certificate created with a different key provider, use **Manage Private Keys** under **Personal > Certificates** in `certlm.msc`.

## Register the app

1. In the Microsoft Entra admin center, open **App registrations > New registration** and select **Accounts in this organizational directory only**. Leave the redirect URI blank.
2. Copy the **Directory (tenant) ID** and **Application (client) ID** from **Overview**.
3. Under **Certificates & secrets > Certificates**, upload `SharePointPortal.cer`.
4. Under **API permissions > Add a permission**, choose **SharePoint > Application permissions > Sites.Selected**, then grant admin consent for the tenant.

[Microsoft's SharePoint app-only authentication guide](https://learn.microsoft.com/en-us/sharepoint/dev/solution-guidance/security-apponly-azuread)

### Register the app and grant admin consent with PowerShell

Run this once in a Microsoft Graph PowerShell session with an administrator who can create apps and grant application permissions. It registers a new app, uploads the public certificate, and grants the **SharePoint** application role `Sites.Selected`. The Graph scopes in `Connect-MgGraph` apply to the administrator's session.

```powershell
Install-Module Microsoft.Graph.Authentication -Scope CurrentUser
$tenantId = 'DIRECTORY-TENANT-ID'
$appName = 'xRM Portals SharePoint Integration'
$certificatePath = (Resolve-Path '.\SharePointPortal.cer').Path

Connect-MgGraph -TenantId $tenantId -Scopes 'Application.ReadWrite.All','AppRoleAssignment.ReadWrite.All'
$graph = 'https://graph.microsoft.com/v1.0'
$sharePointAppId = '00000003-0000-0ff1-ce00-000000000000'
$sharePoint = Invoke-MgGraphRequest -Method GET -Uri "$graph/servicePrincipals(appId='$sharePointAppId')"
$role = @($sharePoint.appRoles | Where-Object {
    $_.value -eq 'Sites.Selected' -and $_.isEnabled -and $_.allowedMemberTypes -contains 'Application'
})
if ($role.Count -ne 1) { throw 'Could not resolve the SharePoint Sites.Selected application role.' }

$app = Invoke-MgGraphRequest -Method POST -Uri "$graph/applications" -ContentType 'application/json' -Body (@{
    displayName = $appName
    signInAudience = 'AzureADMyOrg'
    keyCredentials = @(@{
        type = 'AsymmetricX509Cert'
        usage = 'Verify'
        key = [Convert]::ToBase64String([IO.File]::ReadAllBytes($certificatePath))
        displayName = 'SharePoint portal certificate'
    })
    requiredResourceAccess = @(@{
        resourceAppId = $sharePoint.appId
        resourceAccess = @(@{ id = $role[0].id; type = 'Role' })
    })
} | ConvertTo-Json -Depth 8)
$clientId = $app.appId
$principal = Invoke-MgGraphRequest -Method POST -Uri "$graph/servicePrincipals" -ContentType 'application/json' `
    -Body (@{ appId = $clientId } | ConvertTo-Json)
Invoke-MgGraphRequest -Method POST -Uri "$graph/servicePrincipals/$($principal.id)/appRoleAssignments" `
    -ContentType 'application/json' -Body (@{
        principalId = $principal.id
        resourceId = $sharePoint.id
        appRoleId = $role[0].id
    } | ConvertTo-Json)
Disconnect-MgGraph

[pscustomobject]@{ TenantId = $tenantId; ClientId = $clientId }
```

## Grant access to the SharePoint site

From a separate Microsoft Graph PowerShell admin session, grant the portal app `write` access to the specific site. The delegated admin session uses `Sites.FullControl.All` to create the grant. The portal app uses its SharePoint `Sites.Selected` permission at runtime. [Microsoft Graph site permission reference](https://learn.microsoft.com/en-us/graph/api/site-post-permissions?view=graph-rest-1.0)

```powershell
$tenantId = 'DIRECTORY-TENANT-ID'
$clientId = 'APPLICATION-CLIENT-ID'
$appName = 'xRM Portals SharePoint Integration'
$sharePointHost = 'contoso.sharepoint.com'
$sitePath = '/sites/Portal' # Use '/' for the tenant root site.

Connect-MgGraph -TenantId $tenantId -Scopes 'Sites.FullControl.All'
$graph = 'https://graph.microsoft.com/v1.0'
$siteEndpoint = if ($sitePath -eq '/') { 'root' } else { "${sharePointHost}:$sitePath" }
$site = Invoke-MgGraphRequest -Method GET -Uri "$graph/sites/$siteEndpoint"
if (([uri]$site.webUrl).Host -ne $sharePointHost) {
    throw 'The resolved site belongs to a different SharePoint host.'
}
$permission = @{
    roles = @('write')
    grantedToIdentities = @(@{
        application = @{ id = $clientId; displayName = $appName }
    })
} | ConvertTo-Json -Depth 5
Invoke-MgGraphRequest -Method POST -Uri "$graph/sites/$($site.id)/permissions" `
    -ContentType 'application/json' -Body $permission
Disconnect-MgGraph
```

## Configure MasterPortal

Use the SharePoint site URL, tenant ID, client ID and certificate thumbprint in the named `SharePoint` connection string. See [SharePoint connection configuration](sharepoint-connection-configuration.md#sharepoint-online).
