namespace AlfaVision.FaiLicence;

/// <summary>
/// Settings for the FAI Sporting Licences API. The password is a secret: keep it in user secrets, an environment
/// variable or a secret store — never in source control.
/// </summary>
public sealed class FaiLicenceOptions
{
    /// <summary>Default configuration section name (e.g. <c>"FaiLicenceApi"</c> in appsettings).</summary>
    public const string SectionName = "FaiLicenceApi";

    /// <summary>Production: <c>https://extranet.fai.org/fai/en/api/v1/</c>. FAI also runs a test environment; ask FAI for its address.</summary>
    public string? BaseUrl { get; set; }

    /// <summary>API user name issued by FAI (IGC).</summary>
    public string? Username { get; set; }

    /// <summary>The plain password. The client base64-encodes it as the API requires (API manual, section 2).</summary>
    public string? Password { get; set; }

    /// <summary>Delay between consecutive lookups during bulk checks, to be gentle with FAI's database.</summary>
    public int PauseMs { get; set; } = 400;

    /// <summary>True when base URL, user name and password are all set.</summary>
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(BaseUrl) && !string.IsNullOrWhiteSpace(Username) && !string.IsNullOrWhiteSpace(Password);
}
