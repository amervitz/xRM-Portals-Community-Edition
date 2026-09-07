# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [Unreleased]

### Added

Build

- Added GitHub Actions Debug and Release build validation for pull requests and pushes to `dev`, with manual runs available for other branches.

Code

- Added the `OrganizationServiceType` app setting to select the legacy `OrganizationServiceProxy`, Dataverse `ServiceClient`, or on-premises `CrmServiceClient`.
- Added the `PortalBaseSolution` app setting for base-solution detection and schema version filtering. Supported values are `MicrosoftCrmPortalBase` (the default when unset) and `MicrosoftPortalBase` (for online environments using the legacy data model).

### Changed

Code

- Retargeted all projects to .NET Framework 4.8.1.
- Updated CRM SDK dependencies and binding redirects for both clients, preserving the portal's connection and caching wrappers.

Docs

- Clarified the project's current scope and noted that additional use cases may emerge as the renewed development initiative matures.
- Organized CRM connection configuration under design documentation.
- Updated build and runtime documentation for .NET Framework 4.8.1.
- Updated migration guidance to refer to Microsoft Power Pages.

### Removed

Code

- Removed legacy managed-code analysis, StyleCop analyzers, and shared ruleset configuration from all projects.
