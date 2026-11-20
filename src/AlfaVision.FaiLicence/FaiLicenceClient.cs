using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace AlfaVision.FaiLicence;

/// <summary>
/// Client for the FAI Sporting Licences database. <b>Read-only by design</b>: only the search endpoint (<c>licences</c>)
/// and the details endpoint (<c>licence/{id}</c>) can be called. The API also exposes <c>create</c> (which creates AND
/// updates licences, even via GET); <see cref="AllowedPaths"/> blocks everything but search and details.
/// <para>
/// Credentials travel as query-string parameters because that is the only mechanism the API offers. Register the
/// <see cref="HttpClient"/> without request logging (see <see cref="ServiceCollectionExtensions.AddFaiLicenceClient"/>),
/// and never log or echo a URL built by this client. Error messages never contain the URL or the credentials.
/// </para>
/// </summary>
public sealed class FaiLicenceClient(HttpClient http, IOptions<FaiLicenceOptions> options)
{
    /// <summary>The only paths this client will ever call: the search list and <c>licence/{digits}</c>.</summary>
    public static readonly IReadOnlyList<string> AllowedPaths = ["licences", "licence/"];

    /// <summary>True when base URL, user name and password are configured.</summary>
    public bool IsConfigured => options.Value.IsConfigured;

    /// <summary>
    /// Searches licences. Common parameters: <c>search_number</c> (matches the NAC licence number, the FAI licence id
    /// and the FAI licencee id), <c>search_name</c>, <c>country</c> (IOC code), <c>discipline</c>, <c>include_invalid</c>,
    /// <c>valid_from</c>/<c>valid_to</c>, <c>limit_start</c>/<c>limit_length</c> (FAI asks for at most 100 per page;
    /// results come oldest first).
    /// </summary>
    public async Task<IReadOnlyList<FaiLicence>> SearchAsync(IDictionary<string, string?> parameters, CancellationToken ct = default)
    {
        using var doc = await GetAsync("licences", parameters, ct);
        if (doc.RootElement.ValueKind != JsonValueKind.Array) return [];
        return [.. doc.RootElement.EnumerateArray().Select(e => new FaiLicence(
            Int(e, "idlicence") ?? 0, Int(e, "idlicencee"), Str(e, "IOC") ?? "", Str(e, "licencenumber_lic"),
            Str(e, "givenname_lip"), Str(e, "surname_lip"), Date(e, "birthdate_lip"), Str(e, "Sport") ?? Str(e, "sport"),
            Date(e, "dateissued_lic"), Date(e, "validuntil_lic"), Bool(e, "is_expired")))];
    }

    /// <summary>Fetches the details of one licence card, or null when the API returns nothing for the id.</summary>
    public async Task<FaiLicenceDetails?> GetDetailsAsync(int idLicence, CancellationToken ct = default)
    {
        using var doc = await GetAsync($"licence/{idLicence}", new Dictionary<string, string?>(), ct);
        var e = doc.RootElement.ValueKind == JsonValueKind.Array ? doc.RootElement.EnumerateArray().FirstOrDefault() : doc.RootElement;
        if (e.ValueKind != JsonValueKind.Object) return null;
        return new FaiLicenceDetails(Int(e, "idlicence") ?? idLicence, Str(e, "licencenumber_lic"), Date(e, "validuntil_lic"),
            Bool(e, "is_expired"), Bool(e, "suspended_lip"), Str(e, "idissue_lic"));
    }

    /// <summary>
    /// Builds the request URL for an allowed path. <b>The result contains the credentials — never log it.</b>
    /// Throws <see cref="InvalidOperationException"/> for any path outside <see cref="AllowedPaths"/>.
    /// </summary>
    public string BuildUrl(string path, IDictionary<string, string?> parameters)
    {
        if (!IsAllowed(path))
            throw new InvalidOperationException("The FAI licence client is read-only: only 'licences' and 'licence/{id}' may be called.");
        var o = options.Value;
        var query = new List<KeyValuePair<string, string?>>(parameters)
        {
            new("auth_username", o.Username),
            new("auth_password", Convert.ToBase64String(Encoding.UTF8.GetBytes(o.Password ?? ""))),
        };
        var sb = new StringBuilder(o.BaseUrl!.TrimEnd('/')).Append('/').Append(path);
        var first = true;
        foreach (var (key, value) in query)
        {
            if (value is null) continue;
            sb.Append(first ? '?' : '&').Append(Uri.EscapeDataString(key)).Append('=').Append(Uri.EscapeDataString(value));
            first = false;
        }
        return sb.ToString();
    }

    private static bool IsAllowed(string path) =>
        AllowedPaths.Any(a => a.EndsWith('/')
            ? path.StartsWith(a, StringComparison.Ordinal) && path.Length > a.Length && path[a.Length..].All(char.IsAsciiDigit)
            : path == a);

    private async Task<JsonDocument> GetAsync(string path, IDictionary<string, string?> parameters, CancellationToken ct)
    {
        if (!IsConfigured) throw new InvalidOperationException("The FAI licence API is not configured (BaseUrl, Username and Password are required).");
        HttpResponseMessage response;
        try { response = await http.GetAsync(BuildUrl(path, parameters), ct); }
        catch (HttpRequestException ex) { throw new FaiLicenceException($"The FAI licence database could not be reached ({ex.StatusCode?.ToString() ?? "network"})."); }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested) { throw new FaiLicenceException("The FAI licence database did not answer in time."); }
        using (response)
        {
            if (!response.IsSuccessStatusCode)
                throw new FaiLicenceException($"The FAI licence database answered {(int)response.StatusCode}.");
            var text = await response.Content.ReadAsStringAsync(ct);
            try { return JsonDocument.Parse(text); }
            catch (JsonException) { throw new FaiLicenceException("The FAI licence database returned an invalid response (wrong credentials?)."); }
        }
    }

    private static string? Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) ? v.ValueKind switch
        {
            JsonValueKind.String => v.GetString(),
            JsonValueKind.Number => v.GetRawText(),
            _ => null,
        } : null;

    private static int? Int(JsonElement e, string name) =>
        int.TryParse(Str(e, name), NumberStyles.Integer, CultureInfo.InvariantCulture, out var i) ? i : null;

    private static DateTime? Date(JsonElement e, string name)
    {
        var s = Str(e, name);
        if (s is null || s.Length < 10) return null;
        return DateTime.TryParseExact(s[..10], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;
    }

    /// <summary>The manual documents 1/0; the test environment answers true/false — both are accepted.</summary>
    private static bool Bool(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && (v.ValueKind == JsonValueKind.True
            || (v.ValueKind == JsonValueKind.Number && v.GetRawText() != "0")
            || (v.ValueKind == JsonValueKind.String && v.GetString() is "1" or "true"));
}
