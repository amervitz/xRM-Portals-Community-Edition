# Caching and Performance

Caching is the reason this portal can serve Dataverse data quickly — and the reason content changes
sometimes do not appear. Understanding the layers makes both the performance and the behaviour
predictable.

There are four distinct caches and one pre-warm mechanism.

---

## 1. The object cache

The in-process memory cache that holds Dataverse records. Configured in `microsoft.xrm.client`:

```xml
<objectCache default="Xrm">
  <add name="Xrm" type="Adxstudio.Xrm.Caching.OutputObjectCache, Adxstudio.Xrm" physicalMemoryLimitPercentage="70" />
</objectCache>
```

`OutputObjectCache` caps itself at 70% of physical memory. It is also the store that backs output caching.

`CacheInvalidation` (`Framework/Adxstudio.Xrm/Caching/CacheInvalidation.cs`) knows how to remove entries
when a record changes, and `CacheDependencyCalculator` (`Services/CacheDependencyCalculator.cs`) works out
which entries depend on which records.

---

## 2. The organization service cache chain

`microsoft.xrm.client/serviceCache` stacks caching layers in front of the raw organization service:

```xml
<serviceCache default="Xrm">
  <add name="Xrm" type="Adxstudio.Xrm.Services.PortalBusOrganizationServiceCache, Adxstudio.Xrm" innerServiceCacheName="Inner" />
  <add name="Inner" type="Adxstudio.Xrm.Services.ContentMapOrganizationServiceCache, Adxstudio.Xrm" />
</serviceCache>
```

| Cache | Role |
|---|---|
| `PortalBusOrganizationServiceCache` | Participates in portal-bus cache invalidation, so a change on one instance invalidates on the others. |
| `ContentMapOrganizationServiceCache` | Understands content-map dependencies specifically, so a change to a page invalidates the right cached entries. |

Other services in `Adxstudio.Xrm/Services` that matter here:

- `CachedOrganizationService`, `CompositeOrganizationServiceCache`, `OrganizationServiceCache` — the general
  caching machinery.
- `EventCachedOrganizationService` — invalidates from organization-service events.
- `DebugOrganizationServiceCache` — a diagnostic implementation.
- `CacheItemTelemetry`, `ServicesEventSource` — instrumentation.
- `CrmOnlineOrganizationService` with `CrmOnlineTransientErrorDetectionStrategy` /
  `CrmOnlineReadTransientErrorDetectionStrategy` — transient-fault handling for online environments.
- `CrmTokenManager` / `ICrmTokenManager` — token caching/refresh for the SDK client.

---

## 3. Output caching

ASP.NET output caching is routed to the object cache:

```xml
<caching>
  <outputCache defaultProvider="Xrm">
    <providers>
      <add name="Xrm" type="Adxstudio.Xrm.Caching.ObjectCacheOutputCacheProvider, Adxstudio.Xrm" />
    </providers>
  </outputCache>
  <outputCacheSettings>
    <outputCacheProfiles>
      <add name="Roles" duration="86400" varyByParam="*" varyByCustom="roles;language" ... />
      <add name="RolesShared" duration="86400" varyByParam="none" varyByCustom="roles;website;language" ... />
      <add name="User" duration="86400" varyByParam="*" varyByCustom="user;language" ... />
      <add name="UserShared" duration="86400" varyByParam="none" varyByCustom="user;website;language" ... />
    </outputCacheProfiles>
  </outputCacheSettings>
</caching>
```

The four profiles differ along two axes:

- **What it varies by:** `Roles` and `User`-based profiles serve different content to different audiences;
  `*Shared` profiles vary only by website and language, so they are shared more widely (faster, but only
  safe for anonymous/identical content).
- **Whether the query string matters:** `varyByParam="*"` versus `varyByParam="none"`.

`OutputCacheModule` implements the behaviour, and `Global.asax.cs::GetVaryByCustomString` implements the
custom variation keys: `roles`, `user`, `website`, `culture`, and combinations with `language`.

### The rule you must not break

**Never output cache a page that emits an anti-forgery token.** The token is per-session; caching it serves
one visitor's token to another, which breaks sign-in. This branch fixed exactly that bug on the sign-in
page. See
[Security and Authentication](Security-and-Authentication.md#anti-forgery-and-output-caching).

Similarly, do not cache a page that embeds per-user data under a shared profile.

---

## 4. Content map and security caches

The **content map** (page tree, URLs, permissions) and **security decisions** are cached separately, with
their own dependency tracking:

- `ContentMapProvider` / `IContentMapProvider` build and hold the content map.
- `CrmEntityCacheDependencyTrace` and `CrmEntitySecurityCacheInfo` track what a cached decision depended
  on.
- `ApplicationCachingCrmEntitySecurityProvider`, `RequestCachingCrmEntitySecurityProvider` and
  `CachingCrmEntitySecurityProvider` cache permission results at different scopes.
- `Security/WebNotificationCryptography` protects the invalidation messages.

Security caching is keyed by user, roles, website and language, so a role change correctly produces a
different cache entry.

---

## Invalidation: how a change propagates

Three mechanisms exist; your deployment may use one or more.

### a. Web notifications (push)

Dataverse plugins call the portal's `WebNotification.axd` endpoint after a change. `WebNotificationHandler`
decrypts the notification (via `WebNotificationCryptography`), determines what changed, and invalidates the
dependent cache entries and content map.

Requirements:

- The `Adxstudio.Xrm.Plugins.WebNotification` plugin assembly must be registered and enabled for the
  tables whose changes matter.
- The endpoint must be reachable from Dataverse.
- The notification payload must be authentic — hence the cryptography.

### b. Portal bus (instance to instance)

`UsePortalBus<CacheInvalidationPortalBusMessage>` and `UsePortalBus<ApplicationRestartPortalBusMessage>`
broadcast invalidation and restart messages between web instances. `PortalBusOrganizationServiceCache`
participates, which is what keeps a multi-instance deployment consistent.

### c. Event Hub invalidation (poll/subscribe)

`app.ConfigureEventHubCacheInvalidation(...)` in `Startup.ConfigurePortal` wires the
`EventHubBasedInvalidation` subsystem. It maintains change tracking and feeds invalidations:

| Component | Role |
|---|---|
| `EventHubJob`, `EventHubJobManager`, `EventHubJobSettings` | The subscription jobs. `Startup` creates one cache subscription per **instance id** and one search subscription per **site name**. |
| `CacheInvalidationJob`, `CacheInvalidationJobSettings` | Applies invalidations. |
| `CrmChangeTrackingManager`, `EntityTrackingInfo`, `TimeBasedChangedData` | Track what changed since a point in time. |
| `NotificationMessageTransformer`, `NotificationUpdateManager` | Normalize and apply notifications. |
| `PortalCacheInvalidatorThread` | Background invalidation. |
| `SearchMetadataCache`, `SearchSavedQuery` | Search-side metadata cache. |
| `EventHubInvalidationErrorDetectionStrategy` | Transient-failure handling. |

This is the mechanism that matters most for **scale-out**: it is how each instance learns about changes.

---

## Cache warmup

A saved list of organization requests is replayed at start-up to prime the object cache so the first
requests are not slow:

```csharp
app.WarmupCache(ApplicationDbContext.Create, warmupCacheSettings);
```

The requests live in `App_Data/Adxstudio.Xrm.Services.Cache.Warmup` as `cache_<hash>.request` files, and
`WarmupCacheSettings` controls the warmup. **That directory must be readable by the application**; if it is
missing or unreadable, warmup is skipped and the site is slower rather than broken.

Because the files are a snapshot, they go stale as your portal evolves. Regenerating them is a
performance-tuning task, not a correctness one.

---

## Performance profiler and markers

`PerformanceProfiler` marks significant operations with a name, area and tag:

- `PerformanceMarkerName`, `PerformanceMarkerArea`, `PerformanceMarkerTagName`, `PerformanceMarkerType`.
- Liquid rendering is measured under `PerformanceMarkerName.LiquidExtension` /
  `PerformanceMarkerArea.Liquid`, with `LiquidSourceParsed` and `RenderLiquid` tags — useful when a
  template is slow.
- `EventSourcePerformanceLogger` and `IPerformanceLogger` emit the measurements.
- `PortalPerformance` is a trace source; raise it to see performance output.

Start-up work is measured under `PerformanceMarkerName.Startup` / `PerformanceMarkerArea.Crm` /
`StartUpConfiguration`.

---

## Performance tuning checklist

1. **Turn off debug compilation.** `debug="true"` in `Web.config` disables caching optimizations. Use
   `debug="false"` in production.
2. **Warm the cache.** Ensure `App_Data/Adxstudio.Xrm.Services.Cache.Warmup` is present and readable, and
   that `applicationInitialization` is configured (it is, in the sample `Web.config`).
3. **Add output caching deliberately.** Use the `RolesShared`/`UserShared` profiles where content really is
   shared, and remember the anti-forgery rule.
4. **Keep the search index out of the request path.** Index building should not run on user requests.
5. **Watch `PortalAllowStaleData`.** It trades correctness for availability: when Dataverse is slow, the
   portal serves cached data. That is usually the right trade for content, and the wrong one for anything
   transactional.
6. **Reduce Dataverse round trips.** Prefer `RetrieveSingleRequest`/`FetchMultipleRequest` batching over
   many small queries; the framework already caches aggressively.
7. **Instrument before optimizing.** Use `PerformanceProfiler` markers and the `PortalPerformance` trace
   source rather than guessing.
8. **In scale-out, fix invalidation first.** If instances hold divergent caches, no amount of caching tuning
   will make the site feel correct. Configure the portal bus or Event Hub properly.

---

## Diagnosing cache problems

| Symptom | Look at |
|---|---|
| Content changes never appear | Web notification plugin registration and network path to `WebNotification.axd`. |
| Changes appear on one instance only | Portal bus / Event Hub invalidation configuration and instance identifiers. |
| Every page is slow after restart | Cache warmup files missing or unreadable. |
| Wrong language or wrong user's data | An output-cache profile missing a `varyByCustom` key. |
| Memory pressure | Lower `physicalMemoryLimitPercentage` on the object cache (70% in the sample). |
| A permission change does not take effect | Security cache — verify the change is in Dataverse and invalidate. |

The quickest way to distinguish "a caching problem" from "a data problem" is to recycle the application
pool: if the symptom disappears, it was caching.

## Related pages

- [Architecture](Architecture.md) — where these components sit in the pipeline
- [Configuration Reference](Configuration-Reference.md) — the cache configuration blocks
- [Search](Search.md) — the index, a separate cache
- [Troubleshooting](Troubleshooting.md#content-changes-do-not-appear)
