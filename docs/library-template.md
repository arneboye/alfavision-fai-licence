# Checklist for a new AlfaVision library

Copy this list when starting the next shared library. `alfavision-fai-licence` is the reference implementation of
every item; copy its files rather than retyping them.

## Names

- GitHub repository: `alfavision-<name>` (lower case, hyphens), owner `arneboye` (move to an organisation later
  without changing the package).
- Package id and root namespace: `AlfaVision.<Name>` (PascalCase). Check that the id is free:
  `https://api.nuget.org/v3-flatcontainer/<id lower case>/index.json` answers 404 when it is.
- One library per repository. Tests in `tests/`, a runnable sample in `samples/`, documentation in `docs/`.

## Repository files

| File | Purpose |
|---|---|
| `Directory.Build.props` | net10.0, nullable, warnings as errors, `EnforceCodeStyleInBuild`, deterministic builds; **ownership** (`Company` AlfaVision, `Authors`, `Copyright © year AlfaVision`, `PackageProjectUrl`, `RepositoryUrl`, MIT `PackageLicenseExpression`, `PackageReadmeFile`, `PackageTags`); Source Link (`PublishRepositoryUrl`, `EmbedUntrackedSources`) and `snupkg` symbols. |
| `.editorconfig` | Code style and dead-code rules, enforced at build. |
| `.gitattributes` | LF everywhere. |
| `README.md` | Packed into the NuGet package: what it is, install, use with and without DI, configuration, security, who uses it, releasing. |
| `CHANGELOG.md` | *Unreleased* section on top; dated at release. |
| `LICENSE` | MIT, "Copyright (c) <year> AlfaVision". |
| `CONTRIBUTING.md`, `SECURITY.md`, `.github/ISSUE_TEMPLATE/*.md` | Short; security reports to arne@alfavision.dk. |

## Code

- `GenerateDocumentationFile` on; XML docs on every public member (the build fails otherwise). They feed IntelliSense
  and the API reference.
- A `ServiceCollection` extension (`Add<Name>Client`) that registers the client the safe way; options class with a
  `SectionName` constant and `IsConfigured`.
- Error messages never contain secrets. Cover it with a test.
- Tests never call the real external service.
- Expose the client as an interface (`I<Name>Client`) and ship test helpers in a `<Name>.Testing` namespace (an
  in-memory fake and an `HttpMessageHandler` that answers like the real service, plus deterministic test data), so
  consumers can test their own code without the network. Document them in a README section "Testing your code".

## CI and release

- `.github/workflows/ci.yml`: push/PR → restore, build Release, test, `dotnet pack` as a smoke test.
- `.github/workflows/publish.yml`: on tag `v*` → version from the tag, pack, upload artifact, `NuGet/login@v1`
  (`user: arneboye`, `permissions: id-token: write`, `environment: nuget`), `dotnet nuget push --skip-duplicate`.
- nuget.org **Trusted Publishing** policy (account menu → Trusted Publishing → Add):
  - Package owner: **AlfaVision**
  - CI/CD provider: GitHub Actions; repository owner `arneboye`; repository `alfavision-<name>`; workflow file
    `publish.yml`; environment `nuget`
  - Scopes: Push → "Push new packages and package versions" until the first release, then "only new package
    versions"; glob pattern = the exact package id.
- Release: update `CHANGELOG.md`, `git tag v0.1.0 && git push --tags`. The package is on nuget.org a few minutes later.
- Versioning: SemVer, not dates.

## Documentation site

- `docs/docfx.json` (metadata from `src`, content = `index.md`, field notes, README, CHANGELOG, CONTRIBUTING,
  SECURITY), `docs/toc.yml`, `docs/index.md`.
- `.github/workflows/docs.yml`: on push to `main` → `dotnet tool install -g docfx --version <pinned>`, `docfx
  docs/docfx.json`, `actions/upload-pages-artifact` + `actions/deploy-pages`.
- Enable GitHub Pages with source **GitHub Actions** (`gh api -X POST repos/arneboye/alfavision-<name>/pages -f build_type=workflow`).
- `docs/_site` and `docs/api/*.yml` are generated; keep them out of git.
- Link the site from the README ("Documentation").

## Repository settings

- Issues and Discussions on; Wiki off (docs live in the repo).
- Branch `main` only; releases from tags.
