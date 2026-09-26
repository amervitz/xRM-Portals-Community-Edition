# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [Unreleased]

### Added

Build

- Added GitHub Actions Debug and Release build validation for pull requests and pushes to `dev`, with manual runs available for other branches.
- Added a build check, `Build/BlockSharpZipLib.targets`, imported by the MasterPortal project, that fails if SharpZipLib comes back, in its own or a referenced project's `packages.config` or `obj\project.assets.json`, or among the assemblies the portal deploys. Versions before 1.3.3 have known vulnerabilities, and installing, updating or reinstalling Lucene.Net, or migrating a project to `PackageReference`, would otherwise bring back 0.86.0. To use SharpZipLib on purpose, remove the import, delete the file, and reference version 1.4.2 or later.

Code

- Added the `OrganizationServiceType` app setting to select the legacy `OrganizationServiceProxy`, Dataverse `ServiceClient`, or on-premises `CrmServiceClient`.
- Added the `PortalBaseSolution` app setting for base-solution detection and schema version filtering. Supported values are `MicrosoftCrmPortalBase` (the default when unset) and `MicrosoftPortalBase` (for online environments using the legacy data model).

### Changed

Code

- Retargeted all projects to .NET Framework 4.8.1.
- Updated CRM SDK dependencies and binding redirects for both clients, preserving the portal's connection and caching wrappers.
- Made profile marketing lists optional through the `Profile/ShowMarketingListsPanel` site setting (defaults to `false`) because the `adx_website_list` relationship may not exist.
- Updated Common.Logging and Common.Logging.Core, dependencies of BoboBrowse.Net (the faceted search library built on Lucene.Net), from 3.0.0 to 3.4.1, with binding redirects from the 3.0.0.0 assemblies BoboBrowse.Net is compiled against.

Docs

- Clarified the project's current scope and noted that additional use cases may emerge as the renewed development initiative matures.
- Organized CRM connection configuration under design documentation.
- Updated build and runtime documentation for .NET Framework 4.8.1.
- Updated migration guidance to refer to Microsoft Power Pages.

### Fixed

Code

- Fixed cloud blob web files failing to download when `adx_cloudblobaddress` holds a fully qualified address. xRM Portals only understood a path relative to the storage account, which it concatenated onto the account's endpoint, so the fully qualified address format used by Power Pages produced a malformed URL. Both forms are now understood, so blob addresses used by either version are served.
- Fixed note attachments failing to save to Azure Blob Storage. After uploading the blob, the annotation was updated through a service context it was never attached to, which threw. The blob metadata is now written through the organization service, matching the create, and sends only the changed attribute.
- Fixed the previous blob not being removed when a note's Azure Blob Storage attachment is replaced. The blob was looked up under a hyphenated record id and under the stored file name, neither of which matches how the blob was written.
- Fixed deleting a note throwing a `NullReferenceException` during content map refresh. A deleted record is retrieved as null, which the annotation relationship check dereferenced.
- Fixed editing a note in the notes or timeline control throwing a `NullReferenceException` when the attachment is left unchanged. No file is posted in that case, so the check for whether a new attachment was supplied threw instead of reporting that there was none.
- Fixed building the search index failing in environments that also have the Power Pages enhanced data model. Its `mspp_*` virtual tables have their own "Portal Search" views, which the indexer picked up, and requesting `modifiedon` from them threw because virtual tables such as `mspp_webpage` don't have it. Views on `mspp_*` tables are now skipped.
- Fixed a request to Dataverse, and a caught exception, each time content access levels were checked in environments without the `adx_contentaccesslevel` table, which happens for every search and for knowledge articles in search results, entity lists, lookups and Liquid `fetchxml` tags. The table was checked with a metadata request that throws when the table is missing, and the organization service cache doesn't keep faults. It's now checked with a request that returns no metadata for a missing table, which is cached until the next metadata change or publish. Building the search index checks the table the same way.

### Removed

Code

- Removed legacy managed-code analysis, StyleCop analyzers, and shared ruleset configuration from all projects.
- Removed the entity list OData feed endpoint (`/_odata`).
- Removed customer journey tracking, which posted portal interaction telemetry to a Microsoft internal Dynamics Customer Insights (DCI) hub provisioned only by Microsoft's portal hosting. This also removes the `FCB.CustomerJourneyTracking` feature flag and the `PortalTracking` and `PortalTracking.*` app settings.
- Removed the Microsoft-internal IFx/MDM metrics pipeline (`AdxMetrics`, `MdmMetrics`, `IfxMetricsReporter`, and `MetricsReportingEvents`), which reported portal metrics to Microsoft's internal Geneva monitoring via IFx (the Microsoft Cloud Instrumentation Framework client API) into MDM (its multidimensional metrics backend), and was unreachable dead code because the framework package is not referenced by the project.
- Removed the unused `FCB.Web2Case` and `FCB.Categories` feature flags, which had no remaining feature checks in the codebase.
- Removed Azure Cloud Services (classic) hosting support, because Azure Cloud Services (classic) was retired on 1 September 2024.
- Removed Windows Live ID Web Authentication, which targeted a long-decommissioned sign-in protocol.
- Removed web page and web file tracking, matching Power Pages, where the feature is no longer available since version 9.3.4.x. Requests for web pages and web files with **Enable Tracking** set no longer create `adx_webpagelog` or `adx_webfilelog` records, and the field is no longer shown when editing either on the portal. This also removes the `asyncTrackingEnabled` attribute of the `adxstudio.xrm` configuration section, which must be deleted from any `Web.config` that sets it.
- Removed SharpZipLib, a dependency of Lucene.Net that the portal never uses. Lucene.Net loads it only to compress or decompress stored field values, and the search index never stores compressed fields.
- Removed C5, a dependency of BoboBrowse.Net that the portal never uses. BoboBrowse.Net uses it only in its path and virtual facet handlers, and the portal's facets use neither.
