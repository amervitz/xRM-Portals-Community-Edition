# Contributing

How to contribute to xRM Portals Community Edition.

## Ground rules

- **One pull request per issue**, so changes are easy to identify and review.
- Contributions are accepted under the **inbound=outbound** model: by contributing, you license your work
  under the same terms as the project and confirm you have the right to do so (per the GitHub Terms of
  Service).
- The project is licensed under the [MIT license](https://opensource.org/licenses/MIT).
- Work targets the **`dev`** branch. `main` is the release branch and is updated from `dev` periodically.

## Branch and build workflow

1. Fork or branch from `dev`.
2. Restore and build locally:
   ```sh
   nuget restore Solutions\Portals\Portals.sln -NonInteractive -ConfigFile Solutions\Portals\.nuget\NuGet.Config
   msbuild Solutions\Portals\Portals.sln /m /nologo /verbosity:minimal /property:Configuration=Debug /property:Platform="Any CPU"
   ```
3. Build **both** Debug and Release — that is what CI does.
4. Open a pull request against `dev`.

## Continuous integration

[`.github/workflows/build.yml`](../../.github/workflows/build.yml) runs on:

- pushes to `dev`,
- pull requests targeting `dev`,
- manual runs (`workflow_dispatch`) for any branch.

It restores packages and builds **Debug and Release** on `windows-2022` with a 20-minute timeout, `fail-fast: false`.

**CI compiles; it does not test.** There is no automated test project, and runtime validation needs a
configured portal and CRM environment. A green build means the code compiles in both configurations and
passes the build guards — nothing more. If your change is behavioural, say in the pull request how you
verified it (which environment, which page, what you observed).

### The SharpZipLib guard

`Build/BlockSharpZipLib.targets` is imported by `MasterPortal` and fails the build if SharpZipLib appears
anywhere in the dependency graph below version 1.4.2 — in its own `packages.config`, a referenced project's
`packages.config` or `obj\project.assets.json`, or the deployed assemblies. It exists because Lucene.Net
3.0.3 declares a dependency on the vulnerable 0.86.0, which NuGet would otherwise restore.

If you legitimately need SharpZipLib, reference **1.4.2 or later**, remove the import from
`MasterPortal.csproj`, and delete the target file — as the target's comment instructs.

## Commit messages

Follow [Conventional Commits 1.0.0](https://www.conventionalcommits.org/en/v1.0.0/) with these types:

`feat`, `fix`, `build`, `ci`, `docs`, `perf`, `refactor`, `revert`, `style`, `test`, `chore`.

Write the subject in the **imperative mood**. Examples from the project's history:

```
build(deps): update DotLiquid to 2.0.385, a best-guess match for Power Pages
fix: stop output caching the sign-in page to prevent anti-forgery errors
docs: update README build requirements and deployment guidance
build: remove unused References.targets file
```

Do not include co-author trailers unless explicitly requested.

## Changelog

**Every notable change gets an entry** in [`CHANGELOG.md`](../../CHANGELOG.md) under the `[Unreleased]`
section, in the appropriate category:

| Category | Use for |
|---|---|
| `Added` | New capability, new setting, new dependency. |
| `Changed` | Behaviour changes, dependency updates, retargeting, documentation updates. |
| `Deprecated` | Features marked for removal. |
| `Removed` | Features, dependencies, settings, or files removed. |
| `Fixed` | Bug fixes. |
| `Security` | Security-specific changes. |

The file follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/). Entries are grouped under bold
sub-headings that identify the area — `Build`, `Code`, `Dependencies`, `Docs` — which the project uses
consistently:

```markdown
### Fixed

Code

- Fixed sign-in sometimes failing with `The anti-forgery cookie token and form field token do not match.`
  When a visitor who already had the anti-forgery cookie loaded the sign-in page, the response didn't
  include the `__RequestVerificationToken` cookie, so it was output cached and shown to later visitors,
  who then got that visitor's anti-forgery token and no cookie. The sign-in page is no longer output cached.
```

**Write the why, not just the what.** The existing entries explain the cause and the consequence, which is
what makes the changelog useful when upgrading.

## Documentation

- Design notes live in `docs/design/`.
- Measured/technical notes live in `docs/internal/`.
- User and developer documentation lives in `docs/wiki/` (this wiki).

If you change behaviour, update the wiki page that documents it — and if you change a setting, update
[Site Settings Reference](Site-Settings-Reference.md) and
[Configuration Reference](Configuration-Reference.md) as applicable.

## Code conventions

The codebase is .NET Framework 4.8.1 C# in the original Adxstudio style:

- **Tabs** for indentation in C# files; file-scoped `using` directives inside the namespace in most files.
- **Braces on their own line** (Allman style).
- Explicit access modifiers on members.
- A license header comment at the top of framework source files:
  ```csharp
  /*
    Copyright (c) Microsoft Corporation. All rights reserved.
    Licensed under the MIT License. See License.txt in the project root for license information.
  */
  ```
- Null-argument checks at the start of public methods (`if (x == null) throw new ArgumentNullException("x");`).
- XML documentation comments on public API, especially in framework assemblies.

### What is no longer there

Legacy **managed-code analysis (FxCop), StyleCop analyzers and shared ruleset configuration were removed**
from all projects in this branch, and the `README` TODO list tracks replacing them with modern analyzers
and `.editorconfig` conventions. Do not reintroduce StyleCop rulesets; match the surrounding code instead.

### Dependency changes

- Projects use **`packages.config`, not `PackageReference`.** Keep that model.
- When you update a NuGet package, update the matching `<bindingRedirect>` entries in
  `Samples/MasterPortal/Web.config`.
- Adding a dependency means a `CHANGELOG` entry under `Dependencies`.
- Removing an unused dependency is encouraged, with the reasoning captured in the changelog (see how the
  removals of SharpZipLib, C5, Bond, JetBrains.Annotations and BouncyCastle were documented).
- NuGet 6.10+ provides vulnerability auditing for `packages.config`; GitHub Dependabot provides alerts.
  The old build-time SafeNuGet check was removed in favour of these.

### Removing features

The project deliberately removes legacy features to converge with Power Pages. When you remove one:

1. Remove the code **and** the configuration it read (app settings, site settings, feature flags).
2. Fix the compile break for consumers inside the repository.
3. Document the removal in `CHANGELOG.md` under `Removed`, explaining what it did and what existing
   deployments must do about it.
4. Update the relevant wiki page, and add an entry to
   [Upgrading and Migration](Upgrading-and-Migration.md) if deployments need to act.

The removals of `/_odata`, web page/web file tracking, Windows Live ID, and Azure Cloud Services (classic)
support are the models to follow.

## Security

- **Never commit secrets.** Connection strings, client secrets, certificate private keys, payment
  credentials and encryption keys belong in untracked configuration or a secret store. Note that the
  sample `Web.config` in this repository contains placeholder-shaped values; treat any real values as a
  problem to fix, not a pattern to copy.
- Do not weaken security to make a check pass — no disabling validation, no bypassing permission
  evaluation, no swallowing exceptions.
- The HTML sanitizer (`SafeHtml.SafeHtmSanitizer`) is the sanctioned way to clean user-generated content.

## Pull request checklist

- [ ] Branched from `dev`, one issue per pull request.
- [ ] Builds in **Debug and Release** (and CI is green).
- [ ] No new dependencies without a `Dependencies` changelog entry and binding redirects.
- [ ] `CHANGELOG.md` `[Unreleased]` updated with the *why*.
- [ ] Documentation updated (wiki pages, `docs/design`, `docs/internal`) where behaviour changed.
- [ ] Removed features' configuration, flags and settings are removed too, and upgrade guidance added.
- [ ] Behaviour verified manually, with the verification described in the pull request.
- [ ] No secrets, and no weakening of validation or permissions.

## Related pages

- [Getting Started: Build](Getting-Started-Build.md)
- [Architecture](Architecture.md)
- [Upgrading and Migration](Upgrading-and-Migration.md)
- [`CHANGELOG.md`](../../CHANGELOG.md)
