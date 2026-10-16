# Project Overview

## What this is

xRM Portals Community Edition is an ASP.NET web application that renders a customer-facing or
partner-facing **portal** on top of **Microsoft Dataverse** (the data platform behind Dynamics 365 and
Power Platform). Portal content, configuration, templates, roles and data all live in Dataverse; the
web application reads them through the organization service and renders them as a website.

It is a **fork of the open-source release of Portal Capabilities for Microsoft Dynamics 365 version
8.3** — the software originally sold as Adxstudio Portals — which Microsoft released one time on the
Microsoft Download Center under the MIT license.

## Lineage

```
Adxstudio Portals (commercial, ~7.x and earlier)
        │  acquired by Microsoft, rebranded
        ▼
Portal Capabilities for Microsoft Dynamics 365 8.3  ── one-time open-source release (MIT)
        │  community fork
        ▼
xRM Portals Community Edition  (branch: main = release, dev = active)
        │  Microsoft's continuing product line
        ▼
Microsoft Power Pages   ← the supported future for new work
```

Because the code is an 8.3-era codebase, it carries a large amount of legacy: Web Forms, the CRM SDK
`OrganizationServiceProxy` path, OData/EDM client libraries, Lucene.Net 3, and so on. The `dev` branch
is progressively modernizing these (retargeting to .NET Framework 4.8.1, updating dependencies, removing
insecure libraries and dead features) while keeping the portal content model compatible.

## Objectives

1. **Provide a migration path.** Take an existing Adxstudio Portals 7 or Dynamics 365 Portals 8.3
   deployment, run it on a supported runtime, then migrate to Microsoft Power Pages on a timeline you
   control.
2. **Keep deployment flexible.** Run on Dynamics 365 online (Dataverse) or on-premises, hosted in IIS or
   Azure App Service.
3. **Stay forkable.** Developers can customize the code to suit specific business needs, which is the
   main reason to run a self-hosted portal rather than Power Pages.

## Scope and expectations

Be aware of the following before committing to this project:

- The project is developed **on an informal basis** after a six-year hiatus, out of personal interest.
- The `dev` branch is under active modernization. **Test coverage varies, and the branch is not
  guaranteed to compile or run without errors at any given commit.**
- The README states the code "requires substantial updates to be considered up to date and secure, and
  should only be used for hobby and research purposes for the foreseeable future."
- AI coding tools are used extensively with human oversight.
- **New portal implementations should use [Microsoft Power Pages](https://learn.microsoft.com/en-us/power-pages/).**
  This project is for maintaining or migrating existing deployments.

## What is in this repository

| Path | Contents |
|---|---|
| `Framework/` | The .NET class libraries that make up the portal engine |
| `Framework/Adxstudio.Xrm/` | The main portal framework assembly |
| `Framework/Microsoft.Xrm.Client/` | CRM client, caching and configuration layer |
| `Framework/Microsoft.Xrm.Portal/` | Portal context, CMS access, IdentityModel, routing |
| `Framework/SafeHtml/` | HTML sanitizer used by forums and other user-generated content |
| `Framework/Adxstudio.Xrm.Files/` | Embedded portal file resources (`xrm-adx`) |
| `Framework/Microsoft.Xrm.Portal.Files/` | Legacy embedded resources (`xrm`) |
| `Framework/Adxstudio.Xrm.Resources/` | Localized string resources for dozens of languages |
| `Samples/MasterPortal/` | The deployable ASP.NET web application |
| `Solutions/Portals/Portals.sln` | The Visual Studio solution |
| `Build/` | Shared build assets and the SharpZipLib guard target |
| `docs/` | Design notes, internal notes and this wiki |
| `.github/workflows/build.yml` | CI build validation |

What is **not** in this repository: the Dynamics 365 **solutions** and their data. Those are installed
separately (see [Deployment](Deployment.md)).

## Support status

- The `main` branch is considered the official release version and is updated from `dev` over time.
- The `dev` branch is the active development branch.
- Support is informal — contact is via the maintainer's LinkedIn profile (see the repository README).
- Community contributions are accepted through GitHub pull requests, one per issue, under the
  inbound=outbound model. See [Contributing](Contributing.md).

## Related documentation

- [Getting Started: Build](Getting-Started-Build.md) — build it yourself
- [Architecture](Architecture.md) — how it works internally
- [Upgrading and Migration](Upgrading-and-Migration.md) — moving an existing deployment forward
