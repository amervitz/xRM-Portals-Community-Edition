# Diagnostics and Logging

How to see what the portal is doing, and what to look at when something is wrong.

---

## Trace sources

`Web.config` configures eight named trace sources, all wired to the ASP.NET `WebPageTraceListener` at
`Error` level:

| Source | Covers |
|---|---|
| `ADXTrace` | General framework tracing, including Liquid parse/render errors. |
| `Framework` | Framework-level messages. |
| `PortalWeb` | Web layer, including start-up. |
| `PortalServices` | Organization service operations. |
| `PortalCache` | Cache activity. |
| `PortalFeatureTrace` | Feature/telemetry events, including session start. |
| `PortalPerformance` | `PerformanceProfiler` output. |
| `Microsoft.IdentityModel.Clients.ActiveDirectory` | Token acquisition. |

```xml
<system.diagnostics>
  <sources>
    <source name="ADXTrace" switchValue="Error">
      <listeners><add name="Web" /></listeners>
    </source>
    <!-- … -->
  </sources>
  <sharedListeners>
    <add name="Web" type="System.Web.WebPageTraceListener, System.Web, Version=2.0.3600.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a" />
  </sharedListeners>
  <trace autoflush="false" useGlobalLock="false" />
</system.diagnostics>
```

**Raise a source's `switchValue`** to `Verbose` (or `Information`/`Warning`) to see that subsystem. Typical
recipes:

| Problem | Raise |
|---|---|
| Liquid template not rendering | `ADXTrace` |
| Slow page | `PortalPerformance` |
| Start-up / website resolution failure | `ADXTrace` + `PortalWeb` |
| Dataverse query failures, throttling | `PortalServices` |
| Stale content | `PortalCache` + `PortalWeb` |
| Token/authentication failures | `ADXTrace` + `Microsoft.IdentityModel.Clients.ActiveDirectory` |

### Code tracing

Framework code writes through `ADXTrace.Instance.TraceInfo` / `TraceError` with a `TraceCategory`
(`Application`, `Exception`, …). Entity form controls use `Tracing.FrameworkError`. When you add code,
trace the same way so it appears in the same stream.

### The diagnostics-trace switch

`PortalWriteToDiagnosticsTrace` (`true` in the sample) writes portal tracing into the ASP.NET diagnostic
trace. With `<trace enabled="true" requestLimit="100" mostRecent="true" pageOutput="false" localOnly="true" />`,
you can then read **`trace.axd`** from the local machine.

### Unhandled exceptions

`Global.asax.cs::Application_Error` logs the last error via
`WebEventSource.Log.UnhandledException(Server.GetLastError())`. Exceptions thrown during OWIN start-up are
also logged through `WebEventSource` before being rethrown. That means the ASP.NET/Windows event log is
the first place to look for a hard start-up failure.

---

## Endpoints

| Endpoint | Handler | Purpose |
|---|---|---|
| `Diag.axd` | `DiagnosticsHandler` | Diagnostic information. |
| `WebNotification.axd` | `WebNotificationHandler` | Receives change notifications from Dataverse. Useful to confirm reachability and that plugins fire. |
| `_services/about` (or `{area}/about`) | `AboutProductHandler` | Product and build information. |
| Health / app-info middleware | `HealthMiddleware`, `AppInfoMiddleware` | Registered via `app.UseHealth()` and `app.UseAppInfo()`. |
| `CacheFeed` | `CacheFeedHandler` | The cache feed, configured by the `cacheFeed` element of the `adxstudio.xrm` section. |

`IgnoreUserAgent` should include your monitoring probes so health checks do not pollute telemetry; the
sample already lists common ones.

---

## Telemetry and tracing in code

| Mechanism | Location |
|---|---|
| `ADXTrace` | Framework-wide tracing. |
| `WebEventSource` | Web-level errors and exceptions. |
| `CmsEventSource`, `SearchEventSource`, `ServicesEventSource`, `CacheEventSource` | Feature-specific event sources. |
| `PortalFeatureTrace` / `FeatureTraceCategory` | Feature telemetry (`FeatureTraceCategory.SessionStart`, …). |
| `PerformanceProfiler` / `PerformanceMarker*` | Performance markers. |
| `ScaleOutMiddleware`, `ETWMiddleware` | Elapsed-time-while and scale-out telemetry. |

`Session_Start` in `Global.asax.cs` emits session telemetry when `TelemetryState.IsTelemetryEnabledUserAgent()`
and `TelemetryState.IsTelemetryEnabledRequestPage()` both allow it and the portal is configured.

> Removed in this branch: the Microsoft-internal **IFx/MDM metrics pipeline** (`AdxMetrics`, `MdmMetrics`,
> `IfxMetricsReporter`, `MetricsReportingEvents`) and **customer journey tracking** to the Microsoft-internal
> DCI hub. If you are upgrading, the related app settings (`PortalTracking`, `PortalTracking.*`) and the
> `FCB.CustomerJourneyTracking` feature flag no longer exist. See [Upgrading and Migration](Upgrading-and-Migration.md).

---

## Error pages

`<customErrors mode="RemoteOnly" redirectMode="ResponseRewrite">` maps:

| Status | Page | Meaning |
|---|---|---|
| 500 | `~/500.aspx` | Unhandled server error. |
| 404 | `~/404.aspx` | Not found. |
| 503 | `~/CrmConnectionError.aspx` | The Dataverse connection failed. |

`<httpErrors errorMode="DetailedLocalOnly" existingResponse="PassThrough" />` means local requests get
detailed HTTP errors while remote requests get the portal's pages.

`AccessDenied.aspx` is the portal's access-denied page, distinct from HTTP 401/403.

---

## What to capture when reporting a problem

1. **The exact URL** and whether the request was authenticated.
2. **The HTTP status** and the page rendered.
3. **`trace.axd` output** for the request (raise the relevant source first).
4. **The Windows application event log** entries around the time of the error.
5. **The result of `_services/about`** — proves whether the application started.
6. **Whether recycling the app pool changes the symptom** — separates cache problems from data problems.

---

## Health and readiness

- `applicationInitialization` in `Web.config` (with `initializationPage="/"`) warms the application after
  an app-pool restart.
- `HealthMiddleware` gives a lightweight health endpoint.
- Cache warmup runs as part of start-up; a slow first request usually means warmup did not run.
- `StrictTransportSecurityMiddleware` and `RequireSslMiddleware` can redirect or reject requests, so a
  health probe must be reachable over the scheme the site expects.

---

## Related pages

- [Configuration Reference](Configuration-Reference.md#systemdiagnostics)
- [Troubleshooting](Troubleshooting.md)
- [Caching and Performance](Caching-and-Performance.md)
- [Architecture](Architecture.md) — start-up sequence and middleware order
