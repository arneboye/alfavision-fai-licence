namespace AlfaVision.FaiLicence.Testing;

/// <summary>
/// In-memory <see cref="IFaiLicenceClient"/> for unit tests: no HTTP, no network. Put cards in <see cref="Licences"/>,
/// details in <see cref="Details"/>, or set <see cref="Error"/> to make every call throw. Every call is recorded in
/// <see cref="Searches"/> / <see cref="DetailRequests"/> so a test can assert what the code under test asked for.
/// <para>
/// Search filtering is deliberately simple and mirrors FAI's behaviour for the parameters most code uses:
/// <c>search_number</c> (licence number, licence id or licencee id), <c>search_name</c> (contained in given name or
/// surname, case-insensitive), <c>country</c>, <c>discipline</c>, <c>include_invalid</c> (expired cards are only
/// returned when it is <c>1</c>/<c>true</c>) and <c>limit_start</c>/<c>limit_length</c>. Results come oldest first.
/// </para>
/// </summary>
public sealed class FakeFaiLicenceClient : IFaiLicenceClient
{
    /// <summary>The cards the fake "knows". Use <see cref="FaiLicenceTestData.Cards"/> for a ready-made set.</summary>
    public List<FaiLicence> Licences { get; } = [];

    /// <summary>Details per licence id. Missing ids answer null, as the real API does.</summary>
    public Dictionary<int, FaiLicenceDetails> Details { get; } = [];

    /// <summary>When set, every call throws this exception (e.g. a <see cref="FaiLicenceException"/> to simulate FAI being down).</summary>
    public Exception? Error { get; set; }

    /// <inheritdoc />
    public bool IsConfigured { get; set; } = true;

    /// <summary>Every parameter set passed to <see cref="SearchAsync"/>, in order.</summary>
    public List<IReadOnlyDictionary<string, string?>> Searches { get; } = [];

    /// <summary>Every id passed to <see cref="GetDetailsAsync"/>, in order.</summary>
    public List<int> DetailRequests { get; } = [];

    /// <inheritdoc />
    public Task<IReadOnlyList<FaiLicence>> SearchAsync(IDictionary<string, string?> parameters, CancellationToken ct = default)
    {
        Searches.Add(new Dictionary<string, string?>(parameters));
        if (Error is not null) throw Error;
        if (!IsConfigured) throw new InvalidOperationException("The FAI licence API is not configured (BaseUrl, Username and Password are required).");
        return Task.FromResult<IReadOnlyList<FaiLicence>>(Filter(Licences, parameters));
    }

    /// <inheritdoc />
    public Task<FaiLicenceDetails?> GetDetailsAsync(int idLicence, CancellationToken ct = default)
    {
        DetailRequests.Add(idLicence);
        if (Error is not null) throw Error;
        if (!IsConfigured) throw new InvalidOperationException("The FAI licence API is not configured (BaseUrl, Username and Password are required).");
        return Task.FromResult(Details.TryGetValue(idLicence, out var d) ? d : null);
    }

    /// <summary>Applies FAI's search semantics to an in-memory list (shared with <see cref="FaiLicenceTestHandler"/>).</summary>
    public static List<FaiLicence> Filter(IEnumerable<FaiLicence> cards, IDictionary<string, string?> p)
    {
        static string? Get(IDictionary<string, string?> d, string key) => d.TryGetValue(key, out var v) ? v : null;
        static bool Flag(string? v) => v is "1" or "true" or "True";

        IEnumerable<FaiLicence> q = cards;
        if (Get(p, "search_number") is { Length: > 0 } number)
            q = q.Where(c => c.LicenceNumber == number || c.IdLicence.ToString() == number || c.IdLicencee?.ToString() == number);
        if (Get(p, "search_name") is { Length: > 0 } name)
            q = q.Where(c => (c.GivenName ?? "").Contains(name, StringComparison.OrdinalIgnoreCase)
                          || (c.Surname ?? "").Contains(name, StringComparison.OrdinalIgnoreCase));
        if (Get(p, "country") is { Length: > 0 } country)
            q = q.Where(c => string.Equals(c.Ioc, country, StringComparison.OrdinalIgnoreCase));
        if (Get(p, "discipline") is { Length: > 0 } discipline)
            q = q.Where(c => string.Equals(c.Sport, discipline, StringComparison.OrdinalIgnoreCase));
        if (!Flag(Get(p, "include_invalid")))
            q = q.Where(c => !c.IsExpired);
        q = q.OrderBy(c => c.DateIssued ?? DateTime.MinValue).ThenBy(c => c.IdLicence);
        if (int.TryParse(Get(p, "limit_start"), out var start) && start > 0) q = q.Skip(start);
        if (int.TryParse(Get(p, "limit_length"), out var length) && length > 0) q = q.Take(length);
        return [.. q];
    }
}
