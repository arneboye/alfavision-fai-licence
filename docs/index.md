# AlfaVision.FaiLicence

A small, read-only .NET client for the **FAI Sporting Licences API** (the IGC licence register): search licences and
look up licence details. It never writes, it never logs your credentials, and it is used in production by
[Team](https://team.dsvu.dk), the national-team platform of the Danish Soaring Union.

| | |
|---|---|
| Package | [`AlfaVision.FaiLicence` on nuget.org](https://www.nuget.org/packages/AlfaVision.FaiLicence) |
| Source | [github.com/arneboye/alfavision-fai-licence](https://github.com/arneboye/alfavision-fai-licence) |
| License | MIT © AlfaVision |

## Where to start

- **[Getting started](../README.md)** — install, configure, search and look up a licence, with and without
  dependency injection.
- **[FAI API field notes](fai-api-notes.md)** — what FAI's manual does not tell you: authentication in the query
  string, how the number search really matches, yearly cards, names that differ, `1`/`0` versus `true`/`false`.
- **[API reference](xref:AlfaVision.FaiLicence)** — every public type and member, generated from the XML documentation.
- **[Changelog](../CHANGELOG.md)**, **[Contributing](../CONTRIBUTING.md)**, **[Security](../SECURITY.md)**.

## Try it

The repository contains a sample console app:

```
dotnet user-secrets set "FaiLicenceApi:BaseUrl"  "https://extranet.fai.org/fai/en/api/v1/" --project samples/FaiLicence.Sample
dotnet user-secrets set "FaiLicenceApi:Username" "<your API user>"                           --project samples/FaiLicence.Sample
dotnet user-secrets set "FaiLicenceApi:Password" "<your password>"                           --project samples/FaiLicence.Sample
dotnet run --project samples/FaiLicence.Sample -- --number 1234 --country DEN
```

## Getting FAI credentials

API access is granted per national airsport control (NAC) by FAI/IGC. Ask FAI for an API user for your NAC.
