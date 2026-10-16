# Extensibility

This page is for developers extending the portal in C#. It covers the extension points that exist in the
codebase, in rough order of how often you will want them.

Decide first **which layer** you are changing:

| Layer | Change it when | Effort |
|---|---|---|
| **Content and Liquid** | You want different markup, layout or behaviour driven by data | Low — no deployment |
| **Custom ASP.NET pages and controls** | You need server-side logic or integration | Medium — deployment |
| **Providers and adapters** | You need to change how the framework resolves data or permissions | High — deployment, framework-level |
| **Service-layer customizations** | You need to change how the portal talks to Dataverse | High |

> The single best practice here: **prefer Liquid and content over code.** Content changes need no
> deployment, survive upgrades, and stay within the Power Pages migration path. Code customizations have
> no Power Pages equivalent and must be rebuilt if you migrate.

---

## 1. Liquid: templates, tags, filters and globals

### Templates and snippets

Override any shipped template by creating an `adx_webtemplate` with the same `adx_name`. Templates resolve
Dataverse-first, embedded-second, so an override needs no code change. See
[Web Templates and Liquid](Web-Templates-and-Liquid.md).

### Registering a global variable

`LiquidExtensions.RegisterGlobalVariable` adds a named value to the global rendering scope:

```csharp
LiquidExtensions.RegisterGlobalVariable("myFeature", html => new MyFeatureDrop(html));
```

The factory receives the current `HtmlHelper`, so your drop can reach the request context.

Registration belongs in a static initializer or application start-up path that runs before templates
render. The framework itself registers its globals this way in `LiquidExtensions.GetLiquidEnvironment`.

### Writing a drop

A drop is a DotLiquid-safe object. In this codebase the convention is:

- derive from `Drop` or from `PortalViewEntityDrop` when wrapping a Dataverse record,
- expose properties with lowercased names (the `InvariantCultureNamingConvention` lowercases and
  snake-cases member names),
- implement the constructor to take an `IPortalLiquidContext` so you can reach `PortalViewContext`, the
  URL helper and the organization service.

Study `Web/Mvc/Liquid/PageDrop.cs` and `Web/Mvc/Liquid/EntityDrop.cs` for the pattern. `BeforeMethod` is
the hook for dynamic member resolution — drops that need to resolve arbitrary attribute names override it.

### Registering a custom tag

Tags are registered in `LiquidExtensions`'s static constructor with `Template.RegisterTag<T>("name")`. To add
your own:

1. Implement a class deriving from `DotLiquid.Tag` (or `Block`).
2. Parse your markup in `Initialize`, using `DotLiquid.Util.R.Scan(..., DotLiquid.Liquid.TagAttributes, ...)`
   to read `key: value` attributes, exactly as the built-in tags do.
3. Register it with `Template.RegisterTag<MyTag>("mytag")`.

The built-in tags in `Web/Mvc/Liquid/Tags` are the reference implementations; `Rating.cs` and `Chart.cs`
show both patterns (a simple attribute-parsing tag, and one that writes HTML through
`IPortalLiquidContext`).

**Registration order matters:** the framework's `LiquidExtensions` static constructor deliberately calls
`DotLiquid.Liquid`'s constructor *first*, so portal tags and filters take precedence over any same-named
DotLiquid additions. Add your own registrations after that point so yours win.

### Registering a custom filter

1. Write a `public static` method whose first parameter is the input value.
2. Register the class with `Template.RegisterFilter(typeof(MyFilters))`.

Names are lowercased and snake-cased, so `FormatPhone` becomes `format_phone`. See
`Web/Mvc/Liquid/Filters.cs` and `UrlFilters.cs` for the pattern.

### Replacing a filter

Because registration order determines precedence, registering a class containing a method with the same
Liquid name as a built-in replaces the built-in for that name. This is how a customized `date` filter (to
parse in UTC for Power Pages parity) would be introduced.

---

## 2. Custom ASP.NET pages and controls

Deployment-based extension, used by the built-in listing, profile, search and web form experiences.

1. Add an `.aspx` page under `Samples/MasterPortal/Pages` (or an MVC area under `Samples/MasterPortal/Areas`).
2. Create an `adx_pagetemplate` with `adx_type = 756150000` (Rewrite) pointing at your page.
3. Assign the page template to the relevant `adx_webpage`.

Pages participate in the same pipeline: they run after OWIN has resolved the website, language, user and
content map, so `PortalCrmConfigurationManager.CreatePortalContext()` and the HTML helpers are available.

The built-in Web Forms pages (`Page.aspx`, `Listing.aspx`, `Profile.aspx`, `Search.aspx`, `WebForm.aspx`)
and the `Areas/Portal` controllers are your templates for this.

### Web controls

The portal exposes a control library in `Framework/Adxstudio.Xrm/Web/UI/WebControls`, registered in
`Web.config` under the `adx` tag prefix:

- `CrmEntityFormView` (and the entity form control), `CrmEntityListView` (grids, calendars),
  `EntityForm`, `WebForm`, `Snippet`, `Property`, `Captcha`, `TagCloud`.

Add your own controls to a custom assembly and register the tag prefix in `Web.config`.

### Custom master pages

`Samples/MasterPortal/MasterPages` contains `Default.master`, `Profile.master`, `WebForms.master`,
`WebFormsContent.master` and `WebLinkNavigation.master`. Replacements are a standard ASP.NET exercise:
create your master page and reference it from your pages or page templates.

---

## 3. Providers and adapters

These are the framework's dependency-injection seams. They are configured in `Web.config` and resolved
through the portal's `DependencyProvider`.

### The dependency provider

`microsoft.xrm.portal/portals/add/dependencyProvider` names the class that supplies framework
dependencies (default: `Adxstudio.Xrm.Web.Configuration.DependencyProvider`). It resolves, among others:

- `IEntityWebsiteProvider`
- `IEntityUrlProvider`
- `ICmsDataServiceQueryInterceptorProvider`
- `ICmsEntityEditingMetadataProvider` / `ICrmEntityEditingMetadataProvider`
- `ICmsDataServiceProvider` / `ICmsEntityServiceProvider`
- `IRedirectProvider` (composed of `RedirectProvider`, `CanonicalUrlRedirectProvider`,
  `UrlHistoryRedirectProvider`)
- `IPublishingStateTransitionSecurityProvider`
- `ICrmEntityFileAttachmentProvider` (default `NotesFileAttachmentProvider`)
- `IPortalRouteHandlerProvider`
- `IContentMapProvider`

Derive from `DependencyProvider`, override `GetDependency<T>`, and point the configuration at your class.
This is the highest-leverage extension point in the portal.

### Security providers

- `crmEntitySecurityProvider` on the portal element selects the entity security provider (default
  `Adxstudio.Xrm.Cms.Security.CmsCrmEntitySecurityProvider`).
- `IWebsiteAccessPermissionProvider`, `IPublishingStateTransitionSecurityProvider` and
  `ICacheSupportingCrmEntitySecurityProvider` are further seams in `Cms/Security` and `Security`.

If you need entitlement logic that is not expressible with table permissions, web page access control
rules and content access levels, this is where to implement it — and where the caching contract matters,
since security decisions are cached per user/roles/website/language.

### Search provider

`adxstudio.xrm/search` selects the search provider (`defaultProvider` is required, `providers` is a
collection). Implement `ICrmEntityIndexSearcher` / the provider contract to replace the search backend.
See [Search](Search.md).

### Attachment/file provider

`ICrmEntityFileAttachmentProvider` decides where attachments go. The default
`NotesFileAttachmentProvider` writes Dataverse notes; the Azure implementations live in `Notes`
(`AzureAnnotationFile`).

---

## 4. Service-layer customization

### Organization service events

The `adxstudio.xrm` section has an `events` collection of `IOrganizationServiceEventProvider` elements.
These let you hook create/update/delete/associate operations on the organization service — useful for
auditing, synchronizing external systems, or deriving values. Each element selects an
`ServiceEventInstanceMode`:

| Mode | Instances |
|---|---|
| `Static` | One static instance. |
| `PerName` | One instance per element name. |
| `PerRequest` | One instance per web request (equivalent to `PerInstance` when there is no `HttpContext`). |
| `PerInstance` | A new instance per invocation. |

### Caching behaviour

`microsoft.xrm.client` configures the cache chain. Custom caches implement the object-cache or
organization-service-cache contracts; see [Caching and Performance](Caching-and-Performance.md).

### Configuration provider

`adxstudio.xrm/sectionProviderType` selects the configuration provider (default
`Adxstudio.Xrm.Configuration.AdxstudioCrmConfigurationProvider`). Override it to change how portal
configuration is created.

### Routing

`IPortalRouteHandlerProvider` and the route handlers in `Web/Routing` (CMS entity routes, web resource
routes, attribute and relationship handlers) are how the content map's URLs are translated into handlers.
Custom routing needs here are rare and advanced.

---

## 5. The setup and provisioning hooks

`SetupConfig.GetSetupManager()` looks for a public static `GetSetupManager` method on `Site.Global`, and
falls back to `DefaultSetupManager`. Override that method to customize first-run setup and provisioning
behaviour without modifying the framework.

---

## Testing your extension

There is **no automated test project** in the repository, so verification is manual. For any extension:

1. Build Debug and Release (CI does exactly this).
2. Deploy to a configured environment.
3. Exercise the extension through the real interface: the page, form or list a user would use.
4. Check the trace output for errors (`ADXTrace`, and your own `TraceCategory` messages).
5. Verify permissions behaviour separately from data behaviour — a working feature that ignores
   permissions is not working.

## Related pages

- [Web Templates and Liquid](Web-Templates-and-Liquid.md) and the two Liquid reference pages
- [Framework Namespaces](Framework-Namespaces.md)
- [Architecture](Architecture.md)
- [Configuration Reference](Configuration-Reference.md)
- [Caching and Performance](Caching-and-Performance.md)
- [Contributing](Contributing.md)
