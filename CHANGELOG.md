# Changelog

## 0.3.0 (2026-11-21)

- **Logging.** `FaiLicenceClient` logs its activity through `Microsoft.Extensions.Logging.ILogger<FaiLicenceClient>`: each call at
  Debug (start) and Information (endpoint, result count, duration), failures at Warning. Never the URL, the credentials,
  search values or response content — only parameter names, counts, status codes and licence ids.
- **Breaking:** the constructor now requires the logger: `new FaiLicenceClient(httpClient, options, logger)`. With DI nothing
  changes (`AddFaiLicenceClient` resolves it). Without logging, pass `NullLogger<FaiLicenceClient>.Instance`
  (`Microsoft.Extensions.Logging.Abstractions`, now a dependency of the package).
- README: sections "Logging" and "Testing your code" (asserting log lines with `FakeLogger`); sample app logs to the console.

## 0.2.0 (2026-11-20)

- `IFaiLicenceClient` — the client as an interface; `AddFaiLicenceClient` registers it alongside `FaiLicenceClient`.
- New namespace `AlfaVision.FaiLicence.Testing`: `FakeFaiLicenceClient` (in-memory, FAI-like filtering, records calls),
  `FaiLicenceTestHandler` (an `HttpMessageHandler` that answers like FAI's API) and `FaiLicenceTestData` (four cards).
- README section "Testing your code". No breaking changes.

## 0.1.0 (2026-11-20)

- First release. Read-only client for the FAI Sporting Licences API: `SearchAsync`, `GetDetailsAsync`, `BuildUrl`,
  `AddFaiLicenceClient`. Extracted from Team (team.dsvu.dk), where it has run against FAI's production API since
  September 2026.
