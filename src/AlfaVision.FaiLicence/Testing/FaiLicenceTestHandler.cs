using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Web;

namespace AlfaVision.FaiLicence.Testing;

/// <summary>
/// An <see cref="HttpMessageHandler"/> that behaves like FAI's licence API over HTTP, for tests that want to exercise
/// the real <see cref="FaiLicenceClient"/> (URL building, credentials, JSON parsing) without the network:
/// <c>new FaiLicenceClient(new HttpClient(new FaiLicenceTestHandler()), options)</c>.
/// <para>
/// It answers <c>licences?…</c> with the cards in <see cref="Licences"/> filtered as FAI does (see
/// <see cref="FakeFaiLicenceClient.Filter"/>), <c>licence/{id}</c> with the matching card's details, <c>403</c> when
/// the credentials do not match <see cref="Username"/>/<see cref="Password"/> or the path is one of the write
/// endpoints, and <c>404</c> for unknown paths or ids. Every request URI is kept in <see cref="Requests"/>.
/// JSON is shaped like FAI's answers (field names <c>idlicence</c>, <c>licencenumber_lic</c>, …; expiry as <c>1</c>/<c>0</c>).
/// </para>
/// </summary>
public sealed class FaiLicenceTestHandler : HttpMessageHandler
{
    /// <summary>Cards served by the fake API. Defaults to <see cref="FaiLicenceTestData.Cards"/>.</summary>
    public List<FaiLicence> Licences { get; } = [.. FaiLicenceTestData.Cards];

    /// <summary>Details per licence id. Defaults to <see cref="FaiLicenceTestData.Details"/>.</summary>
    public Dictionary<int, FaiLicenceDetails> Details { get; } = new(FaiLicenceTestData.Details);

    /// <summary>Expected <c>auth_username</c>. Null = any user name is accepted.</summary>
    public string? Username { get; set; }

    /// <summary>Expected plain password (the handler compares against the base64 the client sends). Null = any password is accepted.</summary>
    public string? Password { get; set; }

    /// <summary>When set, every request answers this status with an empty body (simulate an outage or a block).</summary>
    public HttpStatusCode? FailWith { get; set; }

    /// <summary>Every request the client made, in order. Contains credentials — do not log it outside tests.</summary>
    public List<Uri> Requests { get; } = [];

    /// <inheritdoc />
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var uri = request.RequestUri ?? throw new InvalidOperationException("Request without URI.");
        Requests.Add(uri);
        if (FailWith is { } fail) return Task.FromResult(new HttpResponseMessage(fail));

        var query = HttpUtility.ParseQueryString(uri.Query);
        if (!CredentialsOk(query)) return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Forbidden));

        var path = uri.AbsolutePath.TrimEnd('/');
        var last = path[(path.LastIndexOf('/') + 1)..];
        var parent = path[..path.LastIndexOf('/')];
        var parentName = parent[(parent.LastIndexOf('/') + 1)..];

        if (last == "licences")
        {
            var parameters = query.AllKeys.Where(k => k is not null).ToDictionary(k => k!, k => (string?)query[k]);
            var cards = FakeFaiLicenceClient.Filter(Licences, parameters).Select(ToJson).ToList();
            return Task.FromResult(Json(JsonSerializer.Serialize(cards)));
        }
        if (parentName == "licence" && int.TryParse(last, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id))
        {
            if (!Details.TryGetValue(id, out var d)) return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
            return Task.FromResult(Json(JsonSerializer.Serialize(new
            {
                idlicence = d.IdLicence, licencenumber_lic = d.LicenceNumber, validuntil_lic = DateText(d.ValidUntil),
                is_expired = d.IsExpired ? 1 : 0, suspended_lip = d.Suspended ? 1 : 0, idissue_lic = d.Issuer,
            })));
        }
        // The write endpoints exist on the real API; the client must never reach them. Answer like a refused call.
        if (last is "create" or "update" or "delete") return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Forbidden));
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
    }

    private bool CredentialsOk(System.Collections.Specialized.NameValueCollection query)
    {
        if (Username is not null && query["auth_username"] != Username) return false;
        if (Password is not null && query["auth_password"] != Convert.ToBase64String(Encoding.UTF8.GetBytes(Password))) return false;
        return true;
    }

    private static object ToJson(FaiLicence c) => new
    {
        idlicence = c.IdLicence, idlicencee = c.IdLicencee, IOC = c.Ioc, licencenumber_lic = c.LicenceNumber,
        givenname_lip = c.GivenName, surname_lip = c.Surname, birthdate_lip = DateText(c.BirthDate), Sport = c.Sport,
        dateissued_lic = DateText(c.DateIssued), validuntil_lic = DateText(c.ValidUntil), is_expired = c.IsExpired ? 1 : 0,
    };

    private static string? DateText(DateTime? d) => d?.ToString("yyyy-MM-dd 00:00:00", CultureInfo.InvariantCulture);

    private static HttpResponseMessage Json(string text) =>
        new(HttpStatusCode.OK) { Content = new StringContent(text, Encoding.UTF8, "application/json") };
}
