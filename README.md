# xRM Portals Community Edition

[![Build](https://github.com/amervitz/xRM-Portals-Community-Edition/actions/workflows/build.yml/badge.svg?branch=dev&event=push)](https://github.com/amervitz/xRM-Portals-Community-Edition/actions/workflows/build.yml)

Work on this project has resumed on an informal basis after a six-year hiatus, out of personal interest.

The code in this repo requires substantial updates to be considered up to date and secure, and should only be used for hobby and research purposes for the foreseeable future.

The `dev` branch is used for active development and is undergoing substantial modernization to improve usability, adopt current standards, and replace insecure libraries. Test coverage will vary, and the branch is not guaranteed to compile or run without errors.

Information in this README.md will be updated as the maturity level of the codebase increases.

AI coding tools will be used extensively, with human oversight.

The [main](https://github.com/amervitz/xRM-Portals-Community-Edition/tree/main) branch is considered the official release version and eventually will be updated with the changes made in the `dev` branch.

---

xRM Portals Community Edition is a fork of the open source release of [Portal Capabilities for Microsoft Dynamics 365](https://docs.microsoft.com/en-us/dynamics365/customer-engagement/portals/administer-manage-portal-dynamics-365) version 8.3. It continues from the [announced one-time release of Portals source code](https://roadmap.dynamics.com/?i=e2f80f10-118c-e711-8118-3863bb36dd08#) made available on the [Microsoft Download Center](https://www.microsoft.com/en-us/download/details.aspx?id=55789) under the MIT license.

xRM Portals Community Edition enables portal deployments for Dynamics 365 online and on-premises environments, and allows developers to customize the code to suit their specific business needs.

## Objectives

xRM Portals Community Edition allows for an intermediary migration path from Adxstudio Portals 7, to this open source version of portals, towards upgrading to [Microsoft Power Pages](https://learn.microsoft.com/en-us/power-pages/).

New portal implementations should use [Microsoft Power Pages](https://learn.microsoft.com/en-us/power-pages/). This project is intended only for maintaining or migrating existing deployments and is not recommended for new portal development.

Additional scenarios for using this project may emerge in the future as the renewed development initiative matures.

## TODO

- [ ] Add modern .NET analyzers to replace the removed legacy FxCop analysis.
- [ ] Replace the removed StyleCop rules with `.editorconfig` conventions and actively maintained analyzers where needed.
- [ ] Establish an analyzer warning baseline and enforce it in continuous integration.

## Disclaimers

This project is licensed under the [MIT license](https://opensource.org/licenses/MIT), which provides access to the source code free of charge and without warranty of any kind.

This project only contains the source code for the portal web application and its dependent class libraries. The associated Dynamics 365 solutions and their components are distributed separately.

## Building

To build the project, install [Git](https://git-scm.com/downloads) and [Visual Studio 2026](https://learn.microsoft.com/en-us/visualstudio/install/install-visual-studio) with the **ASP.NET and web development** workload and the [.NET Framework 4.8.1 development tools](https://dotnet.microsoft.com/en-us/download/dotnet-framework/net481).

For the current development version, clone the `dev` branch:

```sh
git clone --branch dev https://github.com/amervitz/xRM-Portals-Community-Edition.git
cd xRM-Portals-Community-Edition
```

Open `Solutions\Portals\Portals.sln` in Visual Studio, restore NuGet packages, and build the `Portals` solution or the `MasterPortal` project.

[GitHub Actions](.github/workflows/build.yml) restores packages and builds Debug and Release for pull requests and pushes to `dev`. Runtime validation requires a configured portal and CRM environment.

## Deployment

xRM Portals Community Edition is a set of .NET class libraries and an ASP.NET web application called `MasterPortal`. After building the project, `MasterPortal` is run using conventional ASP.NET website hosting methods such as using [IIS](https://www.iis.net/) in on-premise environments, and [Azure Web Apps](https://docs.microsoft.com/en-ca/azure/app-service-web/app-service-web-overview) in cloud environments.

The `MasterPortal` web application  deployment is dependent upon schema (solutions) and data being installed in a Dynamics 365 instance. These components are downloaded from the [Microsoft Download Center](https://www.microsoft.com/en-us/download/details.aspx?id=55789) in the file `MicrosoftDynamics365PortalsSolutions.exe`. The components in this download have not been released under the MIT license and are not managed by the xRM Portals Community Edition project.

A full description of the deployment process is described in the file `Self-hosted_Installation_Guide_for_Portals.pdf` available for download on the [Microsoft Download Center](https://www.microsoft.com/en-us/download/details.aspx?id=55789).

## Upgrading an existing deployment

The original installation guide describes the 8.3 release. When deploying the current `dev` branch, use the build, connection and runtime requirements in this README and review the [Unreleased changelog](CHANGELOG.md#unreleased) for changes that affect existing deployments.

The following legacy features have been removed to bring the project closer to the current Power Pages feature set, reduce reliance on obsolete components, and improve security and migration compatibility:

- The entity list OData feed (`/_odata`) has been removed to align with Power Pages' data access features. Integrations that call it need an alternative data access path.
- Web page and web file tracking has been removed to match Power Pages behavior. The **Enable Tracking** fields no longer cause the portal to create tracking records. Remove the `asyncTrackingEnabled` attribute from the `adxstudio.xrm` configuration section if it is present in your existing `Web.config`.
- Windows Live ID Web Authentication has been removed as an obsolete authentication integration. Existing deployments using it need to configure another provider compatible with their Power Pages migration plans.
- Azure Cloud Services (classic) hosting support has been removed as a retired hosting model. Use IIS or Azure App Service to host the web application.

## CRM connection configuration

Set the `OrganizationServiceType` app setting in `Web.config` to `ServiceClient` to connect through the Dataverse client, or `CrmServiceClient` for Dynamics 365 on-premises. An omitted or blank setting retains the legacy `OrganizationServiceProxy` client. Supply an `Xrm` connection string for the selected client and choose the base portal solution with the `PortalBaseSolution` app setting.

[Read the CRM connection configuration guide](docs/design/connection-configuration.md) for the `OrganizationServiceType` and `PortalBaseSolution` app settings and the `Xrm` connection string.

## System Requirements

The following requirements apply to the current `dev` branch and supersede older runtime and operating-system requirements in `Self-hosted_Installation_Guide_for_Portals.pdf`:

- Use x64 Windows Server 2022 or 2025 for server hosting, or Windows 11 for local development, with a release and edition still supported by Microsoft. .NET Framework 4.8.1 must be installed; it is included with Windows Server 2025 and Windows 11 version 22H2 and later. Windows 10 versions 20H2 through 22H2 are also compatible with .NET Framework 4.8.1, but should only be used while the installed edition remains covered by Microsoft support or Extended Security Updates ([download](https://dotnet.microsoft.com/en-us/download/dotnet-framework/net481), [system requirements](https://learn.microsoft.com/en-us/dotnet/framework/get-started/system-requirements)).

- For IIS hosting, enable the operating system's IIS 10.0 web server and ASP.NET 4.x features. These Windows versions provide a newer IIS version than the installation guide's IIS 7.5 prerequisite.

- TLS 1.2 must remain enabled for outbound connections to Dataverse and authentication services. It is supported and enabled by default on the Windows versions above, so the older Windows 7 / Windows Server 2008 R2 enablement instructions are no longer needed ([Windows TLS support](https://learn.microsoft.com/en-us/windows/win32/secauthn/protocols-in-tls-ssl--schannel-ssp-)).

- The website must be set to run in 64-bit mode:

  IIS Application Pool:
   
  ![image](https://user-images.githubusercontent.com/10599498/30821566-03ec5466-a1e3-11e7-80bd-bb0b1c724452.png)

  Azure Web App:
   
  ![image](https://user-images.githubusercontent.com/10599498/30821633-468576ae-a1e3-11e7-8b45-e55df1742629.png)

- File system permissions need to be set for general functionality and search indexing to work. Refer to the [File System Permissions](https://github.com/amervitz/xRM-Portals-Community-Edition/wiki/File-System-Permissions) wiki page for full instructions.

- A machine key should be added to the web.config file to ensure cryptographic operations always use the same settings rather than using auto-generated encryption keys (e.g. when hosted a web farm or after application restarts). This can be accomplished using IIS Manager as described in the MSDN blog post [Easiest way to generate MachineKey](https://blogs.msdn.microsoft.com/amb/2012/07/31/easiest-way-to-generate-machinekey/). Adxstudio Portals 7.x used the validation method `SHA1` and the encryption method `AES`.

## Support

Contact me on [LinkedIn](https://www.linkedin.com/in/alanmervitz/).

## License

This project uses the [MIT license](https://opensource.org/licenses/MIT).

## Contributions

This project accepts community contributions through GitHub, following the [inbound=outbound](https://opensource.guide/legal/#does-my-project-need-an-additional-contributor-agreement) model as described in the [GitHub Terms of Service](https://help.github.com/articles/github-terms-of-service/#6-contributions-under-repository-license):
> Whenever you make a contribution to a repository containing notice of a license, you license your contribution under the same terms, and you agree that you have the right to license your contribution under those terms.

Please submit one pull request per issue so that we can easily identify and review the changes.
