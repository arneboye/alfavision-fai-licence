# Changelog

## 0.2.0 (2026-11-20)

- `IFaiLicenceClient` — the client as an interface; `AddFaiLicenceClient` registers it alongside `FaiLicenceClient`.
- New namespace `AlfaVision.FaiLicence.Testing`: `FakeFaiLicenceClient` (in-memory, FAI-like filtering, records calls),
  `FaiLicenceTestHandler` (an `HttpMessageHandler` that answers like FAI's API) and `FaiLicenceTestData` (four cards).
- README section "Testing your code". No breaking changes.

## 0.1.0 (2026-11-20)

- First release. Read-only client for the FAI Sporting Licences API: `SearchAsync`, `GetDetailsAsync`, `BuildUrl`,
  `AddFaiLicenceClient`. Extracted from Team (team.dsvu.dk), where it has run against FAI's production API since
  September 2026.
