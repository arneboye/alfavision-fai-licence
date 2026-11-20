namespace AlfaVision.FaiLicence.Testing;

/// <summary>
/// A small, deterministic set of licence cards for tests: two Danish gliding pilots (one valid card, one expired card
/// plus her newer valid card) and one Australian pilot. Names use ASCII only, as FAI's API tends to return them.
/// </summary>
public static class FaiLicenceTestData
{
    /// <summary>Anne Pilot (DEN), valid card 2027-03-31, licence number <c>DSvU1001</c>, licence id 101, licencee id 9001.</summary>
    public static FaiLicence AnneValid { get; } = new(101, 9001, "DEN", "DSvU1001", "Anne", "Pilot",
        new DateTime(1985, 4, 12), "Gliding", new DateTime(2026, 4, 1), new DateTime(2027, 3, 31), IsExpired: false);

    /// <summary>Bo Flyver (DEN), expired card 2024-12-31, licence number <c>DSvU1002</c>, licence id 102, licencee id 9002.</summary>
    public static FaiLicence BoExpired { get; } = new(102, 9002, "DEN", "DSvU1002", "Bo", "Flyver",
        new DateTime(1970, 9, 3), "Gliding", new DateTime(2024, 1, 15), new DateTime(2024, 12, 31), IsExpired: true);

    /// <summary>Bo Flyver's newer card, valid 2026-12-31, same licence number, licence id 103.</summary>
    public static FaiLicence BoValid { get; } = new(103, 9002, "DEN", "DSvU1002", "Bo", "Flyver",
        new DateTime(1970, 9, 3), "Gliding", new DateTime(2026, 1, 10), new DateTime(2026, 12, 31), IsExpired: false);

    /// <summary>Jane Soarer (AUS), valid card 2027-06-30, licence number <c>GFA-55</c>, licence id 201, licencee id 9101.</summary>
    public static FaiLicence JaneAus { get; } = new(201, 9101, "AUS", "GFA-55", "Jane", "Soarer",
        new DateTime(1992, 11, 23), "Gliding", new DateTime(2026, 7, 1), new DateTime(2027, 6, 30), IsExpired: false);

    /// <summary>All four cards.</summary>
    public static IReadOnlyList<FaiLicence> Cards { get; } = [AnneValid, BoExpired, BoValid, JaneAus];

    /// <summary>Details for every card in <see cref="Cards"/> (nobody suspended; issuer = the NAC).</summary>
    public static IReadOnlyDictionary<int, FaiLicenceDetails> Details { get; } =
        Cards.ToDictionary(c => c.IdLicence, c => new FaiLicenceDetails(c.IdLicence, c.LicenceNumber, c.ValidUntil, c.IsExpired,
            Suspended: false, Issuer: c.Ioc == "DEN" ? "Dansk Svaeveflyver Union" : "Gliding Federation of Australia"));
}
