# FAI Sporting Licences API — field notes

What we learned while integrating the FAI Sporting Licences database (the IGC licence register) into
[Team](https://team.dsvu.dk), the national-team platform of the Danish Soaring Union. FAI's own API manual is the
authority; this page collects the things the manual does not say, or says differently from what the service does.
Ask FAI for the manual together with your credentials.

Everything here is observed behaviour as of autumn 2026 and may change without notice.

## Access

- Access is granted **per national airsport control (NAC)** by FAI/IGC. Ask FAI for an API user for your NAC. Do not
  reuse another NAC's credentials.
- FAI runs a **test environment** with old data next to production. Its address and credentials come from FAI; they
  are not published here.
- The production base URL is `https://extranet.fai.org/fai/en/api/v1/`.

## Authentication

- Credentials travel as **query-string parameters on every request**: `auth_username` and `auth_password`.
- The password is sent **base64-encoded**. The client encodes the plain password; configure it unencoded.
- Consequence: the full request URL is a secret. Make sure nothing logs it. The .NET `HttpClient` logs request URLs at
  Information level by default, which is why `AddFaiLicenceClient` registers the client with `RemoveAllLoggers()`.
  Proxies and web-server access logs on *your* side can log outgoing URLs too; check them.

## Endpoints the client uses

| Endpoint | Purpose |
|---|---|
| `licences?…` | Search. Returns a JSON array of licence cards. |
| `licence/{idlicence}` | Details of one card: adds the issuer, `suspended_lip` and the licencee history. |

### Search parameters

| Parameter | Meaning | Notes |
|---|---|---|
| `search_number` | Licence number | Matches the **NAC licence number, the FAI licence id and the FAI licencee id**. It is also **not exact per country**: a number can hit cards from other NACs even with `country` set. Filter on `IOC` (and name) yourself. |
| `search_name` | Name | Free text against given name and surname. There is **no search on date of birth**. |
| `country` | IOC country code | e.g. `DEN`, `AUS`. |
| `discipline` | Sport | e.g. `Gliding`. |
| `include_invalid` | `1` to include expired cards | Without it only currently valid cards are returned, so a pilot whose card expired looks like "not found". |
| `valid_from`, `valid_to` | Validity window | Dates. |
| `limit_start`, `limit_length` | Paging | FAI asks for **at most 100 per page**. Results come **oldest first**, so page through all of them before deciding which card is current. |

### The write endpoint

The API also has `create`, which creates **and updates** licences, and it accepts GET. API users typically have write
access (`editable: true` in the user record). A read-only integration must therefore guard itself: this client only
ever calls `licences` and `licence/{digits}` and throws before any request for anything else. Keep that guard if you
copy the code.

## What the data looks like

- **A new card is issued every year.** Each year gives a new `idlicence` with the same licence number. The card with the
  latest `validuntil_lic` is the current one. "Does this pilot hold a valid licence?" therefore means: take all cards
  for the number and country, pick the newest, and check its validity.
- **There is no stable person id.** The same person can have several `idlicencee` values over the years. Do not key
  your own data on it.
- **Names can differ from yours.** Order of given names, married names and double surnames differ between the NAC's
  register and FAI's. Team matches on surname first and, when that fails, on at least two shared name parts in any
  order; the mismatch is shown to the user, and FAI's spelling is never stored.
- **Non-ASCII letters may come back as `?`** (for example `Boye-M?ller`). Compare names leniently.
- **`is_expired` is `1`/`0` in the manual and `true`/`false` in the test environment.** The client accepts both; if you
  parse the JSON yourself, do the same.
- **Dates** are plain `yyyy-MM-dd` strings.
- **Personal data.** A search result contains names and dates of birth of real people, including people who are not
  your members (same surname, other NAC). Keep only what you need, and do not display or store other people's dates of
  birth. Team stores status fields only, never FAI's e-mail, address or date of birth.

## Errors

- HTTP errors (403 for wrong credentials, 5xx) and timeouts surface as `FaiLicenceException`. The message never
  contains the request URL, the user name or the password.
- An id with no card returns an empty answer rather than 404; the client returns `null` from `GetDetailsAsync`.
- Answers that are not a JSON array (HTML error pages, for example) are reported as an unparsable answer, again without
  the URL.

## Being a good citizen

- Look up one pilot at a time in bulk checks and pause between calls (`PauseMs`, default 400 ms). The database is
  small and shared by every NAC.
- Cache your results. Team reuses a check from the last 24 hours unless a user asks for a fresh one.
- Do not poll. Licences change once a year.

## Checklist for a new integration

1. Get an API user and the manual from FAI for your NAC.
2. Keep the credentials in a secret store; never in source control or logs.
3. Search with `include_invalid=1`, page through everything, filter on `IOC` and number/name yourself.
4. Pick the newest card; fetch its details for suspension.
5. Store status, not personal data.
6. Keep the client read-only.
