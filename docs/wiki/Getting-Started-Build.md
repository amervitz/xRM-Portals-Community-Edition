# Getting Started: Build

This page takes you from a clean machine to a portal running on your own IIS Express instance. It covers
building the solution from source. For hosting it for real, see [Deployment](Deployment.md).

## Prerequisites

| Requirement | Notes |
|---|---|
| **Windows** | Windows 11 or Windows Server 2022/2025 for a supported configuration. |
| **.NET Framework 4.8.1** | Included with Windows 11 22H2+ and Windows Server 2025; otherwise install the developer pack from Microsoft. |
| **Visual Studio 2026** | Install the **ASP.NET and web development** workload and the **.NET Framework 4.8.1 development tools**. Visual Studio 2022 with the same workload also works. |
| **Git** | To clone the repository. |
| **IIS Express** | Ships with Visual Studio; used by the sample launch script (`Samples/StartExpressPortal.cmd`). |
| **NuGet** | Handled inside Visual Studio, or available as the `nuget.exe` CLI for scripted restores. |
| **64-bit process** | The web application must run in 64-bit mode. See [Deployment](Deployment.md). |

You also need a **Dataverse / Dynamics 365 organization** with the portal **solutions** installed before
the application will render anything. Without a connection string and portal data, the application starts
in **setup mode** instead (see [Setup mode](#setup-mode) below).

## Get the code

The active development branch is `dev`:

```sh
git clone --branch dev https://github.com/amervitz/xRM-Portals-Community-Edition.git
cd xRM-Portals-Community-Edition
```

## Restore packages

NuGet restore is driven by `Solutions/Portals/.nuget/NuGet.Config`, which sets the repository path to
`packages/` at the repository root:

```xml
<config>
  <add key="repositoryPath" value="../../../packages" />
</config>
```

Restore from the command line (this is exactly what CI does):

```sh
nuget restore Solutions\Portals\Portals.sln -NonInteractive -ConfigFile Solutions\Portals\.nuget\NuGet.Config
```

In Visual Studio, opening `Solutions\Portals\Portals.sln` and letting it restore on load is equivalent.
Keep the lockfile-free `packages.config` model intact — these projects are not migrated to
`PackageReference`.

## Build

Open `Solutions\Portals\Portals.sln` and build the `Portals` solution, or the `MasterPortal` project.
Build order is handled by project references.

From the command line:

```sh
msbuild Solutions\Portals\Portals.sln /m /nologo /verbosity:minimal /property:Configuration=Debug /property:Platform="Any CPU"
```

Use `Release` for deployment builds. Both configurations are validated in CI
(`.github/workflows/build.yml`, on `windows-2022`, ~20-minute timeout).

### The SharpZipLib guard

`Build/BlockSharpZipLib.targets` is imported by the `MasterPortal` project and **fails the build** if
SharpZipLib is present — in its own `packages.config`, in a referenced project's `packages.config` or
`obj\project.assets.json`, or among the deployed assemblies. Lucene.Net 3.0.3 declares a dependency on
SharpZipLib 0.86.0, which has known vulnerabilities, and NuGet picks the lowest satisfying version, so
this guard stops it from silently returning. To use SharpZipLib deliberately you must reference version
1.4.2 or later, remove the import from `MasterPortal.csproj`, and delete the target file.

## Run locally

The repository includes an IIS Express configuration and launcher:

- `Samples/applicationHost.config` defines a site named **Adxstudio Portals** with the physical path
  `Samples/MasterPortal`, bound to `*:7800:localhost`.
- `Samples/StartExpressPortal.cmd` starts IIS Express with that configuration and opens
  `http://localhost:7800`.

Run the script from a Developer Command Prompt, or start IIS Express manually:

```sh
"%PROGRAMFILES%\IIS Express\iisexpress.exe" /config:"Samples\applicationHost.config" /site:"Adxstudio Portals"
```

If IIS Express fails to start, close any IIS Express instance in the system tray or change the port in
`applicationHost.config`.

In Visual Studio you can instead set `MasterPortal` as the startup project and press F5; Visual Studio
uses its own IIS Express configuration.

### Configure the connection before the first run

The application needs a connection to Dataverse. Provide an `Xrm` connection string and select the
organization service client — see [CRM Connection](CRM-Connection.md) and
[Configuration Reference](Configuration-Reference.md). Local settings are edited in
`Samples/MasterPortal/Web.config`.

## Setup mode

If the portal is not configured, `SetupConfig` (`Samples/MasterPortal/App_Start/SetupConfig.cs`) switches
the whole application into setup mode:

- `SetupConfig.IsPortalConfigured()` is `false` when there is no `Xrm` connection string **and** no setup
  record in Dataverse **and** setup is enabled.
- `Application_Start` then registers the `SetupRoute` for the `Setup` area (`/{area}/{controller}/{action}`
  defaulting to `Setup/Setup/Index`) and returns before registering the normal portal routes.
- When provisioning is in progress (`PortalSetupInProgress` app setting plus a package/website interview
  against Dataverse), the `Provisioning` action is shown instead, and the app polls for
  `PackageImportComplete` and the `Adxstudio.Xrm.Plugins.WebNotification` plugin assembly before
  releasing into normal operation.

So: a freshly built `MasterPortal` with no connection string will show a **connection settings page**,
not the portal. That is expected. `PortalConfigType` influences this behaviour: any value other than
`online`, `onprem` or `azure` is treated as `online`, and `online` with no connection information
suppresses the connection-settings page and yields a "The connection is undefined" error instead.

## Verify the build

A successful build alone does not prove the portal works. To verify:

1. The site responds on `http://localhost:7800` without a 500 error.
2. `_services/about` returns product/build information.
3. A page from your Dataverse content tree renders — this proves the connection, the content map and
   Liquid rendering all work.
4. If search is enabled, a search query returns results; this proves the Lucene index is buildable and
   `App_Data` is writable.

## What CI checks and what it does not

`.github/workflows/build.yml` restores packages and builds **Debug and Release** for pull requests and
pushes to `dev`, with manual runs available for other branches. It runs on `windows-2022`.

CI does **not** run the portal. There is no automated test project in the repository, and runtime
validation requires a configured portal and CRM environment, so functional verification is manual.
Treat a green CI run as "it compiles", not "it works".

## Next steps

- [Configuration Reference](Configuration-Reference.md) — every setting you can tune
- [CRM Connection](CRM-Connection.md) — connect to Dataverse
- [Deployment](Deployment.md) — host it for real
- [Architecture](Architecture.md) — understand what you just built
