# Security and Authentication

Two independent security systems are in play, and confusing them causes most portal security bugs:

| System | Protects | Decided by |
|---|---|---|
| **Dataverse security** | What the *portal application* can read and write on behalf of the site | The Dataverse application user's security roles |
| **Portal security** | What a *visitor* may see and do on the site | Web roles, table permissions, web page access control rules, content access levels |

Portal security is evaluated **in addition to** Dataverse security. The portal always has broad Dataverse
access through its application user; the portal's own model then decides what each visitor is allowed to
see. When you grant a visitor something, you are granting it *within* what the application user can do.

---

## Authentication

Portal users are stored as **Contact** records with an `adx_identity` for credentials. Authentication
itself runs on **ASP.NET Identity over OWIN**, configured in `Startup.Auth.cs` and `IdentityConfig.cs`.

### The sign-in pipeline

`Startup.ConfigureAuth` builds the login path from
`Authentication/Registration/LoginButtonAuthenticationType`, then registers:

- an `ApplicationUserManager`, `ApplicationSignInManager`, `ApplicationInvitationManager` and
  `ApplicationStartupSettingsManager`;
- the sitemap-aware authentication cookie (`UseSiteMapAuthentication`);
- the application cookie and the external sign-in cookie;
- two-factor cookies (browser-remember and sign-in);
- `UseKentorOwinCookieSaver` (preserves the return URL across the OAuth round trip);
- finally `UsePortalsAuthentication`, which adds every configured external provider.

### Built-in and external providers

`UsePortalsAuthentication` adds each provider **only when it is configured**:

| Provider | Notes |
|---|---|
| **Microsoft Account** | `MicrosoftAccount` provider options. |
| **Twitter** | Consumer key/secret. |
| **Facebook** | Configured via `Authentication/OpenAuth/Facebook/*`. |
| **Google** | Client id/secret. |
| **LinkedIn** | Client id/secret. |
| **Yahoo** | Consumer key/secret. |
| **WS-Federation** | One middleware per configured options set — use for ADFS and other WS-Fed STS. |
| **SAML 2.0** | One middleware per configured options set, via the ITfoxtec SAML 2.0 stack (`Adxstudio.Xrm/Owin/Security/Saml2`). |
| **OpenID Connect** | One middleware per configured options set. |
| **Azure AD / Entra ID** | A specific OpenID Connect configuration (`manager.AzureAdOptions`). |

`UsePortalsAuthentication` also registers the `RSAPKCS1SHA256SignatureDescription` crypto algorithm so
SHA-256 RSA signatures validate (needed by modern federation metadata).

**Local (username/password) sign-in** is also available, governed by
`Authentication/Registration/LocalLoginEnabled` and `LocalLoginByEmail`.

### Azure AD / Entra ID configuration

The sample `Web.config` configures Azure AD through app settings:

| Key | Purpose |
|---|---|
| `Azure.Authentication.ClientId` | Application (client) id. |
| `Azure.Authentication.TenantId` | Tenant id. |
| `Azure.Authentication.RootUrl` | Issuer root, e.g. `https://login.microsoftonline.com/`. |
| `Azure.Authentication.RedirectUri` | Base redirect URI — **rewritten at runtime** to the current host plus `/signin-oidc`. |
| `Azure.Authentication.Caption` | Button caption, e.g. "Azure AD". |

The runtime rewrite is why a single configuration works on multiple host names. Azure AD B2C policy
redirects parse issuer query strings with `HttpUtility` (the previous WebApi.Client dependency was
removed).

### OpenID Connect provider settings

Generic providers are configured by name with a `{provider}/{setting}` pattern:

- `{provider}/{provider}/ClientId`, `ClientSecret`
- `{provider}/{provider}/AuthenticationType`, `Caption`, `Scope`, `CallbackPath`
- `{provider}/{provider}/SignInAsAuthenticationType`
- `{provider}/{provider}/BackchannelTimeout`
- `{provider}/{provider}/RegistrationEnabled`, `AllowContactMappingWithEmail`
- `{provider}/{provider}/LoginClaimsMapping`, `RegistrationClaimsMapping`
- `{provider}/{provider}/ExternalLogoutEnabled`
- `{provider}/{provider}/CertificateThumbprintValidator`, `CertificateSubjectKeyIdentifierValidator`

### Authentication site settings

Registration and sign-in policy is entirely site-settings driven:

| Setting | Effect |
|---|---|
| `Authentication/Registration/Enabled` | Master switch for the registration system. |
| `Authentication/Registration/LocalLoginEnabled` | Username/password sign-in. |
| `Authentication/Registration/LocalLoginByEmail` | Sign in with email instead of username. |
| `Authentication/Registration/OpenRegistrationEnabled` | Anyone may register. |
| `Authentication/Registration/InvitationEnabled` | Registration requires an invitation. |
| `Authentication/Registration/ExternalLoginEnabled` | External providers are offered. |
| `Authentication/Registration/AzureADLoginEnabled` | Azure AD sign-in is offered. |
| `Authentication/Registration/LoginButtonAuthenticationType` | Forces a single provider as the default action (its absence shows the multi-provider page). |
| `Authentication/Registration/EmailConfirmationEnabled` | Requires confirmed email. |
| `Authentication/Registration/CaptchaEnabled` | Captcha on registration. |
| `Authentication/Registration/TwoFactorEnabled` | Two-factor authentication. |
| `Authentication/Registration/RememberMeEnabled`, `RememberBrowserEnabled` | "Remember me" options. |
| `Authentication/Registration/ResetPasswordEnabled`, `ResetPasswordRequiresConfirmedEmail` | Password reset. |
| `Authentication/Registration/SignOutEverywhereEnabled` | Global sign-out. |
| `Authentication/Registration/TriggerLockoutOnFailedPassword` | Lockout on failures. |
| `Authentication/Registration/ProfileRedirectEnabled` | Redirect incomplete profiles to the profile page. |
| `Authentication/Registration/IsDemoMode` | Demo behaviour. |
| `Authentication/Registration/MobilePhoneEnabled` | Phone number support (and phone code two-factor). |
| `Authentication/LoginTrackingEnabled` | Records successful sign-ins (`adx_lastsuccessfullogon`). |

### Password, lockout and cookie policy

| Setting | Effect |
|---|---|
| `Authentication/UserManager/PasswordValidator/EnforcePasswordPolicy` | Enable the password policy. |
| `.../PasswordValidator/RequiredLength`, `RequireDigit`, `RequireLowercase`, `RequireUppercase`, `RequireNonLetterOrDigit` | Password rules. |
| `Authentication/UserManager/MaxFailedAccessAttemptsBeforeLockout`, `DefaultAccountLockoutTimeSpan`, `UserLockoutEnabledByDefault` | Lockout policy. |
| `Authentication/UserManager/UserValidator/AllowOnlyAlphanumericUserNames`, `RequireUniqueEmail` | Username/email rules. |
| `Authentication/ApplicationCookie/*` | Cookie name, domain, path, `CookieHttpOnly`, `CookieSecure`, `ExpireTimeSpan`, `SlidingExpiration`, `LoginPath`, `LogoutPath`, `ReturnUrlParameter`, and security-stamp validation intervals. |
| `Authentication/TwoFactorCookie/*` | Two-factor cookie type and lifetime. |
| `Authentication/UserStore/DeleteByStatusCode` | How account deletion is recorded. |

### Anti-forgery and output caching

The portal sets `AntiForgeryConfig.CookieName = "__RequestVerificationToken"` (both the cookie and the
form field use this name because the AJAX handler depends on it) and suppresses the X-Frame-Options header
that anti-forgery would otherwise add.

> **Important:** pages that emit an anti-forgery token **must not be output cached**. A cached
> anti-forgery token is shared across visitors, which breaks sign-in. This branch fixed a bug where the
> sign-in page was output cached and visitors received another visitor's token (producing
> "The anti-forgery cookie token and form field token do not match" or
> "The required anti-forgery cookie … is not present"). If you add caching to a form page, make sure it
> does not carry an anti-forgery token.

---

## Authorization: web roles

A **web role** (`adx_webrole`) is assigned to portal users (and to accounts) and is the unit of
authorization. Roles drive:

- table permissions (what records a user may touch),
- web page access control rules (which pages a user may see),
- `{% if user | has_role: 'Name' %}` checks in templates,
- content management privileges for front-side editing.

Two roles are special and always exist conceptually:

- the **anonymous users role** (`adx_anonymoususersrole`), applied to signed-out visitors,
- the **authenticated users role** (`adx_authenticatedusersrole`), applied to every signed-in user.

Assigning a permission to the authenticated-users role grants it to everyone who signs in; assigning to
the anonymous-users role grants it to everyone, signed in or not.

Content management privileges on a role:

| Column | Grants |
|---|---|
| `adx_managecontentsnippets` | Edit snippets from the front side. |
| `adx_managesitemarkers` | Manage site markers. |
| `adx_manageweblinksets` | Manage web link sets. |
| `adx_managepermissions` | Manage permissions. |

---

## Authorization: table permissions

A **table permission** (`adx_entitypermission`) grants a web role rights on a Dataverse table. In Power
Pages terms, this is the *table permission*.

Rights are **Create, Read, Write, Delete, Append, Append To**. A permission scopes access by:

- the **table** (`adx_entityname`),
- the **web role**,
- optionally a **scope** — contact, account, parent, global, or self — so that a user sees only their own
  records, their account's records, or everything.

Additional columns such as `adx_parententitypermission` express inheritance between permissions.

`CrmEntityPermissionProvider` evaluates permissions and caches the result per user, role set, website and
language. The relevant classes are in `Framework/Adxstudio.Xrm/Security`:

- `CrmEntityPermissionProvider` — the evaluator.
- `CachingCrmEntitySecurityProvider`, `RequestCachingCrmEntitySecurityProvider`,
  `ApplicationCachingCrmEntitySecurityProvider` — caching layers.
- `CacheSupportingCrmEntitySecurityProvider` / `ICacheSupportingCrmEntitySecurityProvider` — cache
  contracts.

Entity lists and forms enable enforcement with `adx_entitypermissionsenabled`.

> **Do not treat query-level scoping as security.** `adx_filteraccount` / `adx_filterportaluser` on an
> entity list shape the query; they do not replace table permissions. Always configure permissions.

---

## Authorization: web page access control rules

A **web page access control rule** (`adx_webpageaccesscontrolrule`) grants a web role rights on a
specific web page:

| Right | Meaning |
|---|---|
| **Read** | The page is visible in navigation and accessible. |
| **Restricted Read** | The page is accessible by direct URL but **hidden from listings and navigation**. |
| **Write** | The user may edit the page's content. |
| **Create** | The user may create child pages. |
| **Delete** | The user may delete the page. |
| **Append / Append To** | The user may relate content to this page. |

Implementation: `CmsCrmEntitySecurityProvider` (registered in `microsoft.xrm.portal`) and
`WebPageAccessControlSecurityProvider` in `Framework/Adxstudio.Xrm/Cms/Security`.

Rules compose with website access permissions (`adx_websiteaccess`) and publishing-state transition
rules.

---

## Authorization: content access levels

**Content access levels** (`adx_contentaccesslevel`) are a higher-level gate that applies across web
pages, web files and knowledge articles — typically used for licensing or entitlement, where a customer's
purchased tier determines what content they can reach.

Relevant pieces:

- `ContentAccessProvider` and `ContentAccess` folder in the framework.
- Link records such as `adx_knowledgearticlecontentaccesslevel` and `adx_webpageaccesscontrolrule`.
- Settings: `KnowledgeManagement/ContentAccessLevel/Enabled`.

Because a content access level is a *positive* gate, it is normally used to restrict premium content
while everything else stays open.

---

## Website access

`adx_websiteaccess` restricts a whole website to specific roles or users. This is how you host a
partner-only site on the same application as a public one. `IWebsiteAccessPermissionProvider` /
`WebsiteAccessPermissionProvider` implement it.

---

## Transport and headers

| Control | Source |
|---|---|
| **HTTPS redirection** | `RequireSslMiddleware` with `RequireSslOptions`, driven by website settings. |
| **HSTS** | `StrictTransportSecurityMiddleware` with per-website `StrictTransportSecurityOptions`. |
| **Security headers** | `WebsiteHeaderSettingsMiddleware`. |
| **Secure cookies** | `cookieRequireSSL` in `Web.config`, `CookieSecure` in cookie settings, and `UpdateCookieModule` which reapplies the secure flag. |
| **HttpOnly cookies** | `httpCookies httpOnlyCookies="true"`. |
| **Anti-XSS output encoding** | `encoderType="System.Web.Security.AntiXss.AntiXssEncoder"`. |
| **Request validation** | `validateRequest="true"` on pages. |
| **View state encryption** | `viewStateEncryptionMode="Always"`. |

Set `cookieRequireSSL="true"` for any HTTPS deployment.

---

## Signature validation and secrets

- `WebNotification` requests from Dataverse are protected by `WebNotificationCryptography`
  (`Framework/Adxstudio.Xrm/Security`).
- Chat authentication public-key export uses `System.Formats.Asn1` (BouncyCastle was removed).
- Certificate handling for token signing lives in `Framework/Adxstudio.Xrm/Configuration`
  (`CertificateExtensions`, `CertificateNotFoundException`, `X509FindCondition`).
- A `<machineKey>` must be configured for stable encryption across restarts and web-farm nodes.

**Never commit real secrets.** Connection strings, client secrets, certificates with private keys and
payment credentials belong in the hosting platform's secret store or in untracked configuration.

---

## Auditing your security posture

1. Enumerate web roles and their permissions (`adx_entitypermission`,
   `adx_webpageaccesscontrolrule`, content access levels).
2. Confirm the anonymous-users and authenticated-users roles carry only what you intend — these are the
   easiest to over-grant.
3. Confirm every sensitive table has a table permission; a table with no permission is invisible to
   portal users, but any that *should* be readable and is not will surface as an empty list.
4. Confirm registration policy settings match your intent — especially `OpenRegistrationEnabled` and
   `InvitationEnabled`.
5. Confirm HTTPS redirection, HSTS and secure cookies are on in production.
6. Confirm the machine key is set and identical across instances.
7. Review the Dataverse application user's security roles — the portal's own ceiling.

## Related pages

- [Users, Profiles and Invitations](Users-Profiles-and-Invitations.md)
- [Entity Lists](Entity-Lists.md) and [Entity Forms](Entity-Forms.md) — `adx_entitypermissionsenabled`
- [Site Settings Reference](Site-Settings-Reference.md#authentication)
- [Configuration Reference](Configuration-Reference.md) — cookie, encoder and caching settings
- [Deployment](Deployment.md) — machine key and transport security
