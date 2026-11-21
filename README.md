# AlfaVision.FaiLicence

A small, read-only .NET client for the **FAI Sporting Licences API** (the IGC licence database): search licences and look
up licence details. It never writes. It was extracted from [Team](https://team.dsvu.dk), the national-team platform of
the Danish Soaring Union (DSvU), where it has checked pilots' sporting licences against FAI's production database since
September 2026.

[![CI](https://github.com/arneboye/alfavision-fai-licence/actions/workflows/ci.yml/badge.svg)](https://github.com/arneboye/alfavision-fai-licence/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/AlfaVision.FaiLicence.svg)](https://www.nuget.org/packages/AlfaVision.FaiLicence)

## The FAI API in short

What we learned while building Team. FAI's own manual is the authority; ask FAI for it together with your credentials.

| | |
|---|---|
| Production | `https://extranet.fai.org/fai/en/api/v1/` |
| Test environment | FAI runs one with old data; ask FAI for the address |
| Authentication | `auth_username` and `auth_password` as **query-string parameters** on every GET. The password is sent **base64-encoded**; this client encodes the plain password for you. |
| Search | `licences?…` with `search_number`, `search_name`, `country` (IOC code), `discipline`, `include_invalid`, `valid_from` / `valid_to`, `limit_start` / `limit_length`. FAI asks for at most 100 per page. Results come **oldest first**. |
| Details | `licence/{idlicence}` — adds `suspended_lip`, the issuer and the licencee history. |
| Writes | The API also has `create` (creates **and** updates, even via GET). API users typically have write access. **This client blocks every path except search and details.** |

Observations from FAI's data:

- A **new licence card (new `idlicence`) is issued every year** with the same licence number. The one with the latest `validuntil_lic` is the current one.
- `search_number` matches the NAC licence number, the FAI licence id **and** the FAI licencee id, and it is **not exact per country** — filter the results on `IOC` (and name) yourself.
- There is **no stable person id**: the same person can have several `idlicencee` values.
- Non-ASCII letters may come back as `?` (e.g. `Boye-M?ller`).
- `is_expired` is `1`/`0` in the manual and `true`/`false` in the test environment. Both are accepted.

## Install

```
dotnet add package AlfaVision.FaiLicence
```

Targets .NET 10.

## Use

With dependency injection:

```csharp
builder.Services.AddFaiLicenceClient(o =>
{
    o.BaseUrl = builder.Configuration["FaiLicenceApi:BaseUrl"];
    o.Username = builder.Configuration["FaiLicenceApi:Username"];
    o.Password = builder.Configuration["FaiLicenceApi:Password"];   // keep it in user secrets / environment, never in git
});
```

```csharp
public sealed class LicenceCheck(IFaiLicenceClient fai)
{
    public async Task<FaiLicence?> CurrentCardAsync(string licenceNumber, string ioc, CancellationToken ct)
    {
        var cards = await fai.SearchAsync(new Dictionary<string, string?>
        {
            ["search_number"] = licenceNumber,
            ["country"] = ioc,
            ["discipline"] = "Gliding",
            ["include_invalid"] = "1",
            ["limit_length"] = "100",
        }, ct);
        return cards.Where(c => c.Ioc == ioc && c.LicenceNumber == licenceNumber)
                    .OrderByDescending(c => c.ValidUntil)
                    .FirstOrDefault();
    }
}
```

Without DI:

```csharp
var options = Options.Create(new FaiLicenceOptions { BaseUrl = "...", Username = "...", Password = "..." });
var client = new FaiLicenceClient(new HttpClient(), options, NullLogger<FaiLicenceClient>.Instance);   // or a real ILogger<FaiLicenceClient>
var details = await client.GetDetailsAsync(idLicence);
```

`FaiLicenceException` is thrown for HTTP errors, timeouts and unparsable answers; `InvalidOperationException` when the
client is not configured or a non-allowed path is requested.

Depend on `IFaiLicenceClient` rather than the concrete class — the registration provides both — so your own code can
be tested without FAI (next section).

## Testing your code

Three options, from lightest to most realistic. None of them touches the network.

**1. Mock the interface** (NSubstitute, Moq, …):

```csharp
var fai = Substitute.For<IFaiLicenceClient>();
fai.SearchAsync(Arg.Any<IDictionary<string, string?>>(), Arg.Any<CancellationToken>())
   .Returns(new[] { FaiLicenceTestData.AnneValid });
var sut = new LicenceCheck(fai);
```

**2. `FakeFaiLicenceClient`** (namespace `AlfaVision.FaiLicence.Testing`) — an in-memory implementation that filters like
FAI (`search_number`, `search_name`, `country`, `discipline`, `include_invalid`, paging) and records every call:

```csharp
var fake = new FakeFaiLicenceClient();
fake.Licences.AddRange(FaiLicenceTestData.Cards);          // two Danish pilots, one Australian; one expired card
fake.Error = new FaiLicenceException("The FAI licence database answered 503.");   // optional: simulate an outage
services.AddSingleton<IFaiLicenceClient>(fake);            // after AddFaiLicenceClient — the last registration wins
```

**3. `FaiLicenceTestHandler`** — an `HttpMessageHandler` that answers like FAI's API (JSON field names, `1`/`0` expiry
flags, `403` on wrong credentials or write endpoints, `404` on unknown ids), so the real `FaiLicenceClient` runs end to end:

```csharp
var handler = new FaiLicenceTestHandler { Username = "nac-api-user", Password = "secret" };
var client = new FaiLicenceClient(new HttpClient(handler), Options.Create(options), NullLogger<FaiLicenceClient>.Instance);
var cards = await client.SearchAsync(new Dictionary<string, string?> { ["search_number"] = "DSvU1002", ["include_invalid"] = "1" });
```

`FaiLicenceTestData` holds the ready-made cards (`AnneValid`, `BoExpired`, `BoValid`, `JaneAus`) and their details.

**Asserting what gets logged** — the client takes a `Microsoft.Extensions.Logging.ILogger<FaiLicenceClient>`, so use
`FakeLogger<T>` from the `Microsoft.Extensions.Diagnostics.Testing` package to check the lines (and that no line ever
contains your password):

```csharp
var log = new FakeLogger<FaiLicenceClient>();
var client = new FaiLicenceClient(new HttpClient(handler), Options.Create(options), log);
await client.SearchAsync(new Dictionary<string, string?> { ["search_number"] = "DSvU1002" });
Assert.Contains(log.Collector.GetSnapshot(), r => r.Level == LogLevel.Information && r.Message.Contains("returned 1 licences"));
Assert.DoesNotContain(log.Collector.GetSnapshot(), r => r.Message.Contains(options.Password!));
```

## Try it

The repository contains a sample console app (`samples/FaiLicence.Sample`). Put your FAI credentials in user secrets
(never in a file that is committed) and search:

```
dotnet user-secrets set "FaiLicenceApi:BaseUrl"  "https://extranet.fai.org/fai/en/api/v1/" --project samples/FaiLicence.Sample
dotnet user-secrets set "FaiLicenceApi:Username" "<your API user>"                           --project samples/FaiLicence.Sample
dotnet user-secrets set "FaiLicenceApi:Password" "<your password>"                           --project samples/FaiLicence.Sample
dotnet run --project samples/FaiLicence.Sample -- --number 1234 --country DEN
dotnet run --project samples/FaiLicence.Sample -- --name Jensen --country DEN
```

Environment variables work too (`FaiLicenceApi__BaseUrl`, `FaiLicenceApi__Username`, `FaiLicenceApi__Password`).

## Configuration

| Option | Meaning | Default |
|---|---|---|
| `BaseUrl` | API base URL (production or FAI's test environment) | — |
| `Username` | API user name issued by FAI | — |
| `Password` | Plain password; base64-encoded by the client | — |
| `PauseMs` | Suggested pause between lookups in bulk checks | 400 |

Without `BaseUrl`, `Username` and `Password` the client reports `IsConfigured == false` and refuses to call anything.

## Logging

The client logs through the standard `ILogger<FaiLicenceClient>` (category `AlfaVision.FaiLicence.FaiLicenceClient`), so it
lands in whatever provider your application uses — Serilog, the console, Application Insights. With DI nothing needs
to be configured; without DI, pass a logger or `NullLogger<FaiLicenceClient>.Instance`.

| Level | When | Example |
|---|---|---|
| Debug | a call starts | `FAI licence search starting with parameters [country,include_invalid,search_number]` |
| Information | a call completes | `FAI licence search [...] returned 3 licences in 412 ms`, `FAI licence details for licence 101: expired=False suspended=False in 230 ms` |
| Warning | a call fails | `FAI licence database answered 403 (licences)`, `… did not answer in time (licence/{id}, timeout 20 s)`, `… not configured` |

What is **never** logged: the request URL, the user name, the password, search *values* (a `search_name` is a person's
name) or anything from the response body. Only parameter names, counts, status codes, durations and licence ids.
Set the category to `Warning` in production if the per-call Information lines are too chatty.

## Security

- **Credentials in the URL.** That is the only mechanism the API offers. `AddFaiLicenceClient` registers the
  `HttpClient` with `RemoveAllLoggers()` because .NET's default HTTP logging writes the full request URL at
  Information level. If you register the client yourself, do the same, and never log a URL from `BuildUrl`.
- **Error messages never contain the URL, the user name or the password** (covered by tests).
- **Read-only.** Only `licences` and `licence/{digits}` can be called; everything else throws before any request is made.
- **Personal data.** Search results contain names and dates of birth of real people. Keep only what you need, and do
  not log or persist date of birth of people who are not your own members.

## Documentation

- **[Documentation site](https://arneboye.github.io/alfavision-fai-licence/)** — API reference generated from the XML
  docs, plus the pages below.
- **[FAI API field notes](https://arneboye.github.io/alfavision-fai-licence/fai-api-notes.html)** ([source](https://github.com/arneboye/alfavision-fai-licence/blob/main/docs/fai-api-notes.md)) — what the manual does not tell you.
- **[Changelog](CHANGELOG.md)** · **[Contributing](CONTRIBUTING.md)** · **[Security](SECURITY.md)**.

## Getting FAI credentials

API access is granted per national airsport control (NAC) by FAI/IGC. Ask FAI for an API user for your NAC; do not
share another NAC's credentials. FAI can also give you access to the test environment.

## Releasing (maintainers)

Publishing is automated with GitHub Actions and nuget.org **Trusted Publishing** — no API key is stored anywhere.

One-time setup on nuget.org (account `arneboye`):

1. Sign in to nuget.org → account menu → **Trusted Publishing** → **Add**.
2. Repository owner `arneboye`, repository `alfavision-fai-licence`, workflow file `publish.yml`, environment `nuget`
   (optional but recommended; the workflow sets it). Package: `AlfaVision.FaiLicence`.
3. Until the package exists on nuget.org the policy is created as a *pending* policy that becomes permanent at the first
   successful publish. Pending policies expire after a while, so publish 0.1.0 soon after creating it.

Each release:

```
git tag v0.1.0
git push --tags
```

The `publish.yml` workflow builds, tests, packs with the version from the tag, logs in to nuget.org with
[`NuGet/login`](https://github.com/nuget/login) (OIDC) and pushes the package and its symbols. Add a line to
`CHANGELOG.md` first.

## Who uses it

- [Team](https://team.dsvu.dk) — Danish Soaring Union (DSvU).

## License

MIT © AlfaVision. Issues and pull requests are welcome.
