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

To build the project, ensure that you have [Git](https://git-scm.com/downloads) installed to obtain the source code, and [Visual Studio 2026](https://learn.microsoft.com/en-us/visualstudio/install/install-visual-studio) installed with the .NET Framework 4.8.1 Developer Pack to compile the source code.

- Clone the repository using Git:
  ```sh
  git clone https://github.com/amervitz/xRM-Portals-Community-Edition.git
  ```
- Open the `Solutions\Portals\Portals.sln` solution file in Visual Studio
- Build the `Portals` solution or the `MasterPortal` project in Visual Studio

## Deployment

xRM Portals Community Edition is a set of .NET class libraries and an ASP.NET web application called `MasterPortal`. After building the project, `MasterPortal` is run using conventional ASP.NET website hosting methods such as using [IIS](https://www.iis.net/) in on-premise environments, and [Azure Web Apps](https://docs.microsoft.com/en-ca/azure/app-service-web/app-service-web-overview) in cloud environments.

The `MasterPortal` web application  deployment is dependent upon schema (solutions) and data being installed in a Dynamics 365 instance. These components are downloaded from the [Microsoft Download Center](https://www.microsoft.com/en-us/download/details.aspx?id=55789) in the file `MicrosoftDynamics365PortalsSolutions.exe`. The components in this download have not been released under the MIT license and are not managed by the xRM Portals Community Edition project.

A full description of the deployment process is described in the file `Self-hosted_Installation_Guide_for_Portals.pdf` available for download on the [Microsoft Download Center](https://www.microsoft.com/en-us/download/details.aspx?id=55789).

## CRM connection configuration

[Read the CRM connection configuration guide](docs/design/connection-configuration.md) for the `OrganizationServiceType` and `PortalBaseSolution` app settings and the `Xrm` connection string.

## System Requirements

The following system requirements are additional to those listed in `Self-hosted_Installation_Guide_for_Portals.pdf`:

- .NET Framework 4.8.1 must be installed ([download](https://www.microsoft.com/net/download/dotnet-framework-runtime/net481), [system requirements](https://docs.microsoft.com/en-us/dotnet/framework/get-started/system-requirements)).

- The website must be set to run in 64-bit mode:

  IIS Application Pool:
   
  ![image](https://user-images.githubusercontent.com/10599498/30821566-03ec5466-a1e3-11e7-80bd-bb0b1c724452.png)

  Azure Web App:
   
  ![image](https://user-images.githubusercontent.com/10599498/30821633-468576ae-a1e3-11e7-8b45-e55df1742629.png)

- IIS 7.5 (Windows 7 or Windows Server 2008 R2) requires the installation of the [IIS Application Initialization module](https://www.iis.net/downloads/microsoft/application-initialization). Use the `x64` download link at the [bottom of the page](https://www.iis.net/downloads/microsoft/application-initialization#additionalDownloads).

- TLS 1.2 needs to be enabled on older operating systems when connecting to Dynamics 365 CE Online 9.0. Refer to the [Enable TLS 1.2 and 1.1 support on older operating systems](https://github.com/amervitz/xRM-Portals-Community-Edition/wiki/Enable-TLS-1.2-and-1.1-support-on-older-operating-systems) wiki page for full instructions.

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

### Acceptable Bug fixes

Bug fixes will only be accepted for bugs that are **not** reproducable in the online portals version. In other words, if a bug exists in this project and in online portals, it must first be fixed in the online version before a fix will be included in this project. Pull requests for bug fixes can be made, but will be left open until the bug is confirmed to be fixed in online portals. This position is necessary because a bug fix that introduces a behavioral difference with online portals would effectively become a breaking change and compromise the migration path for users of this project to the online version, because users would become depend on the fixed behavior in this project that would then stop working after migrating.
