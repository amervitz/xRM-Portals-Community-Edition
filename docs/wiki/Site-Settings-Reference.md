# Site Settings Reference

Portal behaviour is configured largely through **`adx_sitesetting`** records in Dataverse. Each setting has
a **name** (a slashed path such as `Authentication/Registration/Enabled`) and a **value** (a string). a
setting is resolved per website at runtime, and the code supplies a default when the record is absent.

> **Scope of this page.** These are the settings the **portal code reads directly**. Portals are also
> configured by additional settings used only by the deployment/provisioning tooling or by the Power
> Pages-compatible configuration surface, which are not listed here. When a setting in this list is
> missing, the behaviour noted as the default applies.

**Values are strings.** Convert them in code (or with the Liquid `boolean`, `integer` and `decimal`
filters) rather than assuming types.

---

## Authentication

### Registration and sign-in policy

| Setting | Default | Purpose |
|---|---|---|
| `Authentication/Registration/Enabled` | | Master switch for the registration system. |
| `Authentication/Registration/LocalLoginEnabled` | | Username/password sign-in. |
| `Authentication/Registration/LocalLoginByEmail` | | Use email as the username. |
| `Authentication/Registration/OpenRegistrationEnabled` | | Anyone may register. |
| `Authentication/Registration/InvitationEnabled` | | Registration requires an invitation. |
| `Authentication/Registration/ExternalLoginEnabled` | | Offer external providers. |
| `Authentication/Registration/AzureADLoginEnabled` | | Offer Azure AD / Entra ID sign-in. |
| `Authentication/Registration/LoginButtonAuthenticationType` | | Force a single default provider (its absence shows the multi-provider page). |
| `Authentication/Registration/EmailConfirmationEnabled` | | Require confirmed email. |
| `Authentication/Registration/CaptchaEnabled` | | Captcha on registration. |
| `Authentication/Registration/TwoFactorEnabled` | | Two-factor authentication. |
| `Authentication/Registration/RememberMeEnabled` | | Offer "remember me". |
| `Authentication/Registration/RememberBrowserEnabled` | | Offer "remember this browser" for two-factor. |
| `Authentication/Registration/ResetPasswordEnabled` | | Allow password reset. |
| `Authentication/Registration/ResetPasswordRequiresConfirmedEmail` | | Require confirmation before reset. |
| `Authentication/Registration/SignOutEverywhereEnabled` | | Allow global sign-out. |
| `Authentication/Registration/TriggerLockoutOnFailedPassword` | | Lock out after failed attempts. |
| `Authentication/Registration/ProfileRedirectEnabled` | | Send incomplete profiles to the profile page. |
| `Authentication/Registration/MobilePhoneEnabled` | | Phone number support. |
| `Authentication/Registration/IsDemoMode` | | Demo behaviour. |
| `Authentication/LoginTrackingEnabled` | | Record successful sign-ins. |
| `Authentication/UserStore/DeleteByStatusCode` | | How account deletion is recorded. |

### Application cookie

| Setting | Purpose |
|---|---|
| `Authentication/ApplicationCookie/CookieName` | Cookie name. |
| `Authentication/ApplicationCookie/CookieDomain` | Cookie domain. |
| `Authentication/ApplicationCookie/CookiePath` | Cookie path. |
| `Authentication/ApplicationCookie/CookieHttpOnly` | HttpOnly flag. |
| `Authentication/ApplicationCookie/CookieSecure` | Secure flag. |
| `Authentication/ApplicationCookie/ExpireTimeSpan` | Cookie lifetime. |
| `Authentication/ApplicationCookie/SlidingExpiration` | Sliding expiry. |
| `Authentication/ApplicationCookie/LoginPath` | Sign-in path. |
| `Authentication/ApplicationCookie/LogoutPath` | Sign-out path. |
| `Authentication/ApplicationCookie/ReturnUrlParameter` | Return-URL parameter name. |
| `Authentication/ApplicationCookie/AuthenticationType` | Authentication type. |
| `Authentication/ApplicationCookie/SecurityStampValidator/RefreshInterval` | Security stamp refresh interval. |
| `Authentication/ApplicationCookie/SecurityStampValidator/ValidateInterval` | Security stamp validation interval. |

### Two-factor cookie

| Setting | Purpose |
|---|---|
| `Authentication/TwoFactorCookie/AuthenticationType` | Two-factor authentication type. |
| `Authentication/TwoFactorCookie/ExpireTimeSpan` | Two-factor cookie lifetime. |

### User manager (password, lockout, validation)

| Setting | Purpose |
|---|---|
| `Authentication/UserManager/PasswordValidator/EnforcePasswordPolicy` | Enable the password policy. |
| `Authentication/UserManager/PasswordValidator/RequiredLength` | Minimum length. |
| `Authentication/UserManager/PasswordValidator/RequireDigit` | Require a digit. |
| `Authentication/UserManager/PasswordValidator/RequireLowercase` | Require a lowercase letter. |
| `Authentication/UserManager/PasswordValidator/RequireUppercase` | Require an uppercase letter. |
| `Authentication/UserManager/PasswordValidator/RequireNonLetterOrDigit` | Require a symbol. |
| `Authentication/UserManager/MaxFailedAccessAttemptsBeforeLockout` | Failures before lockout. |
| `Authentication/UserManager/DefaultAccountLockoutTimeSpan` | Lockout duration. |
| `Authentication/UserManager/UserLockoutEnabledByDefault` | Lockout enabled for new users. |
| `Authentication/UserManager/UserValidator/AllowOnlyAlphanumericUserNames` | Restrict usernames. |
| `Authentication/UserManager/UserValidator/RequireUniqueEmail` | Require unique email addresses. |

### External providers (Facebook)

| Setting | Purpose |
|---|---|
| `Authentication/OpenAuth/Facebook/AppSecret` | Facebook app secret. |
| `Authentication/OpenAuth/Facebook/ClientSecret` | Facebook client secret. |
| `Authentication/OpenAuth/Facebook/AuthenticationType` | Facebook authentication type. |

### Azure AD (Entra ID) OpenID Connect

| Setting | Purpose |
|---|---|
| `Authentication/OpenIdConnect/AzureAD/Caption` | Sign-in button caption. |
| `Authentication/OpenIdConnect/AzureAD/RegistrationEnabled` | Allow registration through Azure AD. |
| `Authentication/OpenIdConnect/AzureAD/RegistrationClaimsMapping` | Claims used during registration. |
| `Authentication/OpenIdConnect/AzureAD/LoginClaimsMapping` | Claims used at sign-in. |
| `Authentication/OpenIdConnect/AzureAD/AllowContactMappingWithEmail` | Map contacts by email. |
| `Authentication/OpenIdConnect/AzureAD/ObjectIdentifierAsNameIdentifierClaimEnabled` | Use the object identifier as the name identifier. |
| `Authentication/OpenIdConnect/AzureAD/ExternalLogoutEnabled` | Enable external sign-out. |

### Generic provider pattern

Named external providers are configured with `{provider}/{setting}` keys:

| Pattern | Purpose |
|---|---|
| `{provider}/{provider}/ClientId` | Client id. |
| `{provider}/{provider}/ClientSecret` | Client secret. |
| `{provider}/{provider}/AuthenticationType` | Authentication type. |
| `{provider}/{provider}/Caption` | Button caption. |
| `{provider}/{provider}/Scope` | Requested scopes. |
| `{provider}/{provider}/CallbackPath` | Callback path. |
| `{provider}/{provider}/SignInAsAuthenticationType` | Sign-in authentication type. |
| `{provider}/{provider}/BackchannelTimeout` | Back-channel timeout. |
| `{provider}/{provider}/RegistrationEnabled` | Allow registration through this provider. |
| `{provider}/{provider}/AllowContactMappingWithEmail` | Map contacts by email. |
| `{provider}/{provider}/LoginClaimsMapping`, `RegistrationClaimsMapping` | Claim mappings. |
| `{provider}/{provider}/ExternalLogoutEnabled` | External sign-out. |
| `{provider}/{provider}/CertificateThumbprintValidator` | Expected certificate thumbprint. |
| `{provider}/{provider}/CertificateSubjectKeyIdentifierValidator` | Expected key identifier. |

---

## UI and security

| Setting | Default | Purpose |
|---|---|---|
| `Liquid/Enabled` | `true` | Render Liquid in templates. When `false`, template source is emitted verbatim. |
| `BaseURL` | | An explicit base URL used when building absolute URLs. |
| `Cookies/DisableAnalyticsCookie` | | Suppress the analytics cookie. |
| `RadCaptcha/ErrorMessage` | | Message shown when the Telerik RadCaptcha fails. |
| `Site/EnableDefaultHtmlEncoding` | `true` | Power Pages-compatible setting for default HTML encoding of `user`/`request`. **This codebase does not implement that rewrite** — see the compatibility note. |
| `EntitySecurity/Record/CreateAccessDeniedMessage` | | Message when create is denied. |
| `EntitySecurity/Record/ReadAccessDeniedMessage` | | Message when read is denied. |
| `EntitySecurity/Record/WriteAccessDeniedMessage` | | Message when write is denied. |
| `Profile/ShowMarketingListsPanel` | `false` | Show the marketing-list panel on the profile. |

### Display modes

| Setting | Purpose |
|---|---|
| `DisplayModes/Facebook/HostName` | Host name that triggers the Facebook display mode. |
| `DisplayModes/Iframe/HostName` | Host name that triggers the iframe display mode. |

### Lookup modal

These control the modal lookup control's text and messages:

`Portal/Lookup/Modal/Title`, `Size`, `PrimaryButtonText`, `CancelButtonText`, `NewValueButtonText`,
`RemoveValueButtonText`, `DefaultErrorMessage`, `DismissButtonSrText`, `Grid/PageSize`,
`Grid/EmptyMessage`, `Grid/ErrorMessage`, `Grid/LoadingMessage`, `Grid/AccessDeniedMessage`,
`Grid/ToggleFilterText`, `Grid/Search/PlaceholderText`, `Grid/Search/TooltipText`.

---

## Content and navigation

| Setting | Default | Purpose |
|---|---|---|
| `cms/defaultPageCommentPolicy` | | Default comment policy for pages. |
| `forums/forum/templatepath` | | Template used to render a forum. |
| `forums/forumthread/templatepath` | | Template used to render a forum thread. |
| `MultiLanguage/Fallback` | | Language used when a translation is missing. |
| `MultiLanguage/DisplayLanguageCodeInURL` | | Include the language code in URLs. |

---

## Search

| Setting | Default | Purpose |
|---|---|---|
| `Search/Enabled` | | Enable portal search. |
| `Search/IndexQueryName` | | The query used to build the index. |
| `Search/RecordTypeFacetsEntities` | | Entities contributing record-type facets. |
| `Adxstudio.Xrm.Search.Index.DirectoryPath` | `App_Data/Adxstudio.Xrm.Search` | Index location. |
| `EncryptIndex` | `false` | (App setting) encrypt the Lucene index. |

---

## Files and storage

| Setting | Default | Purpose |
|---|---|---|
| `WebFiles/StorageLocation` | | Where web files are stored (Dataverse or cloud). |
| `WebFiles/CloudStorageAccount` | | Storage account for web files. |
| `WebFiles/MaxFileSizeErrorMessage` | | Message when the upload size limit is exceeded. |
| `FileStorage/CloudStorageAccount` | | Storage account for file storage. |
| `FileStorage/CloudStorageContainerName` | | Container name for file storage. |

---

## Forums and community

| Setting | Default | Purpose |
|---|---|---|
| `Forums/PostsPerPage` | 20 | Posts per page in a thread. |

---

## Knowledge

| Setting | Default | Purpose |
|---|---|---|
| `KnowledgeManagement/Article/Language` | | Article language handling. |
| `KnowledgeManagement/ContentAccessLevel/Enabled` | | Enable content-access-level gating for articles. |
| `KnowledgeManagement/DisplayNotes` | | Show annotations/notes. |
| `KnowledgeManagement/NotesFilter` | | Filter applied to notes. |
| `KnowledgeManagement/Article/CaptureViewCount` | | Count article views. |
| `KnowledgeManagement/Analytics/CaptureKnowledgeArticleReferrer` | | Record the referrer. |
| `KnowledgeManagement/Analytics/ExternalSearchEngines` | | Search engines counted as external referrals. |

---

## Cases and support

| Setting | Default | Purpose |
|---|---|---|
| `HelpDesk/SupportPlanNameFormat` | | Format for the support plan name. |
| `CustomerSupport/DisplayAllUserActivitiesOnTimeline` | | Show all user activities on the timeline. |

---

## Commerce

| Setting | Default | Purpose |
|---|---|---|
| `Ecommerce/PaymentProvider` | `PayPal` | Active payment provider. |
| `Ecommerce/CreateInvoiceOnVerification` | | Create an invoice when payment is verified. |
| `Ecommerce/DefaultPriceLevelName` | | Default price list. |
| `Ecommerce/Paypal/AccountEmail` | | PayPal account email. |
| `Ecommerce/Paypal/PaypalBaseUrl` | | PayPal endpoint. |
| `Ecommerce/PayPal/PDTIdentityToken` | | PayPal Payment Data Transfer token. |

### Product filtering

| Setting | Purpose |
|---|---|
| `ProductFiltering/Enabled` | Filter the catalog by the user's relationships. |
| `ProductFiltering/AccountToProductRelationshipNames` | Account-to-product relationship names. |
| `ProductFiltering/ContactToProductRelationshipNames` | Contact-to-product relationship names. |
| `ProductFiltering/DisplayArticlesWithoutAssociatedProducts` | Show products with no association. |

---

## Entity lists: maps

| Setting | Purpose |
|---|---|
| `BingMap/Key` | Bing Maps key. |
| `BingMap/RestUrl` | Bing Maps REST endpoint. |
| `BingMap/IncludeNeighborhood` | Include neighbourhood in results. |
| `BingMap/UserLocation/Latitude` | Default user latitude. |
| `BingMap/UserLocation/Longitude` | Default user longitude. |
| `Bingmaps/credentials`, `Bingmaps/restURL` | Legacy map credentials and endpoint. |
| `EntityList/Map/DistanceInputLabel` | Label for the distance input. |
| `EntityList/Map/LocationInputLabel` | Label for the location input. |
| `EntityList/Map/SearchButtonLabel` | Label for the search button. |
| `EntityList/Map/GetDirections/Title` | Directions dialog title. |
| `EntityList/Map/GetDirections/ButtonLabel` | Directions button label. |
| `EntityList/Map/GetDirections/FromLabel` | "From" label. |
| `EntityList/Map/GetDirections/ToLabel` | "To" label. |

---

## SharePoint

| Setting | Purpose |
|---|---|
| `SharePoint/URL` | The SharePoint site URL. |

---

## Marketing

| Setting | Purpose |
|---|---|
| `Marketing/EncryptionKey` | Key used to encrypt marketing data. **Treat as a secret.** |

---

## Careers

| Setting | Purpose |
|---|---|
| `Careers/Feed/Title` | Job feed title. |
| `Careers/Feed/Description` | Job feed description. |

---

## Application settings that are not site settings

These live in `Web.config` `<appSettings>`, not in Dataverse, but are frequently needed alongside site
settings. See [Configuration Reference](Configuration-Reference.md) for the full list.

| Key | Purpose |
|---|---|
| `OrganizationServiceType` | SDK client selection. |
| `PortalBaseSolution` | Base solution unique name. |
| `PortalConfigType` | Hosting/connection mode. |
| `OnPremCertificateThumbprint` | On-premises certificate thumbprint. |
| `EncryptIndex` | Encrypt the search index. |
| `PortalWriteToDiagnosticsTrace` | Write portal traces to the ASP.NET trace. |
| `PortalAllowStaleData` | Serve stale cached data when Dataverse is slow. |
| `PortalWebProxyClientFailover` | Enable web proxy client failover. |
| `IgnoreUserAgent`, `IgnorePath`, `TelemetryFileTypes` | Telemetry exclusions and classification. |
| `PortalSetupInProgress`, `PortalPackageName` | Provisioning state. |

---

## Related pages

- [Configuration Reference](Configuration-Reference.md) — `Web.config`
- [Security and Authentication](Security-and-Authentication.md)
- [Search](Search.md), [Multi-Language](Multi-Language.md), [Commerce](Commerce.md)
- [`docs/internal/power-pages-compatibility.md`](../internal/power-pages-compatibility.md)
