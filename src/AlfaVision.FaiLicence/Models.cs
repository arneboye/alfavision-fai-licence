namespace AlfaVision.FaiLicence;

/// <summary>One licence as returned by the search endpoint (<c>licences</c>).</summary>
/// <param name="IdLicence">FAI's id of this licence card. A new card (new id) is issued every year.</param>
/// <param name="IdLicencee">FAI's id of the licence holder. Not stable: the same person can have several.</param>
/// <param name="Ioc">IOC country code of the issuing NAC, e.g. <c>DEN</c>, <c>AUS</c>.</param>
/// <param name="LicenceNumber">The NAC's licence number as printed on the card.</param>
/// <param name="GivenName">Given name(s). Non-ASCII letters may come back as <c>?</c>.</param>
/// <param name="Surname">Surname. Non-ASCII letters may come back as <c>?</c>.</param>
/// <param name="BirthDate">Date of birth, when present.</param>
/// <param name="Sport">Sport / discipline, e.g. <c>Gliding</c>.</param>
/// <param name="DateIssued">Issue date.</param>
/// <param name="ValidUntil">Last day of validity.</param>
/// <param name="IsExpired">FAI's own expiry flag.</param>
public sealed record FaiLicence(
    int IdLicence, int? IdLicencee, string Ioc, string? LicenceNumber, string? GivenName, string? Surname,
    DateTime? BirthDate, string? Sport, DateTime? DateIssued, DateTime? ValidUntil, bool IsExpired);

/// <summary>Details of one licence (<c>licence/{idlicence}</c>) — the subset of fields most consumers need.</summary>
/// <param name="IdLicence">FAI's id of the licence card.</param>
/// <param name="LicenceNumber">The NAC's licence number.</param>
/// <param name="ValidUntil">Last day of validity.</param>
/// <param name="IsExpired">FAI's own expiry flag.</param>
/// <param name="Suspended">True when the holder is suspended.</param>
/// <param name="Issuer">Issuing body as FAI records it.</param>
public sealed record FaiLicenceDetails(int IdLicence, string? LicenceNumber, DateTime? ValidUntil, bool IsExpired, bool Suspended, string? Issuer);

/// <summary>Error from the FAI API. The message never contains the request URL, user name or password.</summary>
public sealed class FaiLicenceException(string message) : Exception(message);
