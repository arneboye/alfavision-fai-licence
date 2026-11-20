# Security

## Reporting a vulnerability

Please report security problems privately to **arne@alfavision.dk**, not in a public issue. Include what you found, how
to reproduce it and which version is affected. You will get an answer within a week, and a fix or a mitigation plan as
soon as we have one. We will credit you in the changelog unless you prefer otherwise.

## Supported versions

Only the latest release on nuget.org receives fixes.

## What the library does to protect you

- **Credentials never appear in exceptions.** FAI's API takes the user name and password as query-string parameters,
  so every request URL is a secret. The client's error messages contain neither the URL nor the credentials, and that
  is covered by tests.
- **No request logging by default.** `AddFaiLicenceClient` registers the `HttpClient` with `RemoveAllLoggers()`,
  because .NET's default HTTP logging writes full request URLs at Information level.
- **Read-only by construction.** Only the search and details endpoints can be called. The API's write endpoint cannot be
  reached through this client even by mistake.
- **No telemetry, no network calls other than to the base URL you configure.**

## What you must do yourself

- Keep the password in a secret store (user secrets, environment variables, a vault) and out of source control.
- If you register the `HttpClient` yourself, disable request logging as the extension method does, and never log a
  URL from `BuildUrl`.
- Check your own proxies and web-server access logs; they may record outgoing URLs.
- Treat search results as personal data of real people.
