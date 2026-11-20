namespace AlfaVision.FaiLicence;

/// <summary>
/// The read-only FAI Sporting Licences client as an interface, so code that depends on it can be unit-tested with a
/// mock (NSubstitute, Moq) or with <see cref="Testing.FakeFaiLicenceClient"/>. <see cref="FaiLicenceClient"/> is the
/// implementation that talks to FAI; <see cref="ServiceCollectionExtensions.AddFaiLicenceClient"/> registers both.
/// </summary>
public interface IFaiLicenceClient
{
    /// <summary>True when base URL, user name and password are configured.</summary>
    bool IsConfigured { get; }

    /// <summary>
    /// Searches licences. Common parameters: <c>search_number</c> (matches the NAC licence number, the FAI licence id
    /// and the FAI licencee id), <c>search_name</c>, <c>country</c> (IOC code), <c>discipline</c>, <c>include_invalid</c>,
    /// <c>valid_from</c>/<c>valid_to</c>, <c>limit_start</c>/<c>limit_length</c> (at most 100 per page; oldest first).
    /// </summary>
    Task<IReadOnlyList<FaiLicence>> SearchAsync(IDictionary<string, string?> parameters, CancellationToken ct = default);

    /// <summary>Fetches the details of one licence card, or null when the API returns nothing for the id.</summary>
    Task<FaiLicenceDetails?> GetDetailsAsync(int idLicence, CancellationToken ct = default);
}
