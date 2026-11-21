using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
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
/// <para>
/// <b>Logging.</b> The client logs its activity through the <see cref="ILogger{TCategoryName}"/> it is given (category
/// <c>AlfaVision.FaiLicence.FaiLicenceClient</c>): each call at Debug when it starts and at Information when it completes
/// (endpoint, result count, HTTP status, duration), failures at Warning. It never logs the URL, the credentials, search
/// values (which may be a person's name) or anything from the response body — only parameter <i>names</i>, counts,
/// status codes and licence ids. Pass <c>NullLogger&lt;FaiLicenceClient&gt;.Instance</c> when you have no logging.
/// </para>
/// </summary>
public sealed class FaiLicenceClient(HttpClient http, IOptions<FaiLicenceOptions> options, ILogger<FaiLicenceClient> logger) : IFaiLicenceClient
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
        var names = string.Join(",", parameters.Where(p => p.Value is not null).Select(p => p.Key).OrderBy(k => k, StringComparer.Ordinal));
        logger.LogDebug("FAI licence search starting with parameters [{ParameterNames}]", names);
        var watch = Stopwatch.StartNew();
        using var doc = await GetAsync("licences", parameters, ct);
        if (doc.RootElement.ValueKind != JsonValueKind.Array)
        {
            logger.LogWarning("FAI licence search answered a {Kind} instead of an array after {ElapsedMs} ms; treating as no results", doc.RootElement.ValueKind, watch.ElapsedMilliseconds);
            return [];
        }
        IReadOnlyList<FaiLicence> result = [.. doc.RootElement.EnumerateArray().Select(e => new FaiLicence(
            Int(e, "idlicence") ?? 0, Int(e, "idlicencee"), Str(e, "IOC") ?? "", Str(e, "licencenumber_lic"),
            Str(e, "givenname_lip"), Str(e, "surname_lip"), Date(e, "birthdate_lip"), Str(e, "Sport") ?? Str(e, "sport"),
            Date(e, "dateissued_lic"), Date(e, "validuntil_lic"), Bool(e, "is_expired")))];
        logger.LogInformation("FAI licence search [{ParameterNames}] returned {Count} licences in {ElapsedMs} ms", names, result.Count, watch.ElapsedMilliseconds);
        return result;
    }

    /// <summary>Fetches the details of one licence card, or null when the API returns nothing for the id.</summary>
    public async Task<FaiLicenceDetails?> GetDetailsAsync(int idLicence, CancellationToken ct = default)
    {
        logger.LogDebug("FAI licence details starting for licence {IdLicence}", idLicence);
        var watch = Stopwatch.StartNew();
        using var doc = await GetAsync($"licence/{idLicence}", new Dictionary<string, string?>(), ct);
        var e = doc.RootElement.ValueKind == JsonValueKind.Array ? doc.RootElement.EnumerateArray().FirstOrDefault() : doc.RootElement;
        if (e.ValueKind != JsonValueKind.Object)
        {
            logger.LogInformation("FAI licence details for licence {IdLicence}: no card in {ElapsedMs} ms", idLicence, watch.ElapsedMilliseconds);
            return null;
        }
        var details = new FaiLicenceDetails(Int(e, "idlicence") ?? idLicence, Str(e, "licencenumber_lic"), Date(e, "validuntil_lic"),
            Bool(e, "is_expired"), Bool(e, "suspended_lip"), Str(e, "idissue_lic"));
        logger.LogInformation("FAI licence details for licence {IdLicence}: expired={Expired} suspended={Suspended} in {ElapsedMs} ms",
            idLicence, details.IsExpired, details.Suspended, watch.ElapsedMilliseconds);
        return details;
    }

    /// <summary>
    /// Builds the request URL for an allowed path. <b>The result contains the credentials — never log it.</b>
    /// Throws <see cref="InvalidOperationException"/> for any path outside <see cref="AllowedPaths"/>.
    /// </summary>
    public string BuildUrl(string path, IDictionary<string, string?> parameters)
    {
        if (!IsAllowed(path))
        {
            logger.LogWarning("FAI licence client refused a request to a non-allowed path (read-only client)");
            throw new InvalidOperationException("The FAI licence client is read-only: only 'licences' and 'licence/{id}' may be called.");
        }
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
        var endpoint = path.StartsWith("licence/", StringComparison.Ordinal) ? "licence/{id}" : path;   // the template, so log lines group per endpoint
        if (!IsConfigured)
        {
            logger.LogWarning("FAI licence client called ({Endpoint}) but it is not configured (BaseUrl, Username and Password are required)", endpoint);
            throw new InvalidOperationException("The FAI licence API is not configured (BaseUrl, Username and Password are required).");
        }
        HttpResponseMessage response;
        try { response = await http.GetAsync(BuildUrl(path, parameters), ct); }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "FAI licence database could not be reached ({Endpoint}, {Status})", endpoint, ex.StatusCode?.ToString() ?? "network");
            throw new FaiLicenceException($"The FAI licence database could not be reached ({ex.StatusCode?.ToString() ?? "network"}).");
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning("FAI licence database did not answer in time ({Endpoint}, timeout {TimeoutSeconds} s)", endpoint, http.Timeout.TotalSeconds);
            throw new FaiLicenceException("The FAI licence database did not answer in time.");
        }
        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("FAI licence database answered {StatusCode} ({Endpoint})", (int)response.StatusCode, endpoint);
                throw new FaiLicenceException($"The FAI licence database answered {(int)response.StatusCode}.");
            }
            var text = await response.Content.ReadAsStringAsync(ct);
            try { return JsonDocument.Parse(text); }
            catch (JsonException ex)
            {
                logger.LogWarning(ex, "FAI licence database returned an invalid response ({Endpoint}, {Length} bytes) — wrong credentials?", endpoint, text.Length);
                throw new FaiLicenceException("The FAI licence database returned an invalid response (wrong credentials?).");
            }
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
