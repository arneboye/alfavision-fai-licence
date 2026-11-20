# Contributing

Thank you for helping. This is a small library with a narrow job: read licences from FAI's database safely. Changes
that keep it small, read-only and secret-free are welcome.

## Build and test

```
dotnet build
dotnet test
```

Requires the .NET 10 SDK. The sample app (`samples/FaiLicence.Sample`) builds with the solution; it needs FAI
credentials to run (see its header comment), the tests do not.

## Ground rules

- **Read-only stays read-only.** Nothing may call anything but `licences` and `licence/{id}`. The guard
  (`FaiLicenceClient.AllowedPaths`) and its tests are not up for discussion.
- **No secrets, ever.** No credentials, no test-environment URLs, no personal data in code, tests, docs or commits.
  Error messages must never contain the request URL.
- **Code style** is enforced by `.editorconfig` at build time, and warnings are errors. Unused code fails the build.
- **Public API changes** need XML documentation on every public member (the build fails without it) and a line in
  `CHANGELOG.md` under *Unreleased*.
- **Tests** for every behaviour change; no test may call the real FAI API.

## Pull requests

1. Fork, branch from `main`, keep the change focused.
2. `dotnet build` and `dotnet test` green locally; CI runs the same.
3. Describe what changed and why in the PR. Link the issue if there is one.
4. One of the maintainers reviews and merges to `main`.

## Versioning and releases

The package follows [SemVer](https://semver.org/). Maintainers release by tagging `main` (`git tag v1.2.3 && git push --tags`);
the `publish.yml` workflow builds, tests and pushes to nuget.org through Trusted Publishing. See the README section
*Releasing*.

## Questions

Open a [discussion](https://github.com/arneboye/alfavision-fai-licence/discussions) for questions and ideas, an
[issue](https://github.com/arneboye/alfavision-fai-licence/issues) for bugs. Security problems: see `SECURITY.md`.
