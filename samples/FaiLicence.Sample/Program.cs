// Sample: look up sporting licences in FAI's database from the command line.
//
//   dotnet run --project samples/FaiLicence.Sample -- --number 1234 --country DEN
//   dotnet run --project samples/FaiLicence.Sample -- --name Jensen --country DEN
//
// Configuration (section FaiLicenceApi): BaseUrl, Username, Password — from `dotnet user-secrets` or environment
// variables (FaiLicenceApi__BaseUrl, FaiLicenceApi__Username, FaiLicenceApi__Password). Never put them in a file
// that is committed.

using AlfaVision.FaiLicence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

var configuration = new ConfigurationBuilder()
    .AddUserSecrets<SampleMarker>(optional: true)
    .AddEnvironmentVariables()
    .Build();

var (number, name, country, discipline) = ParseArguments(args);
if (number is null && name is null)
{
    Console.Error.WriteLine("Usage: --number <licence number> | --name <surname> [--country <IOC code>] [--discipline Gliding]");
    return 2;
}

var services = new ServiceCollection();
services.AddLogging(b => b.AddSimpleConsole().SetMinimumLevel(LogLevel.Information));   // the client logs its calls (never the URL or credentials)
services.AddFaiLicenceClient(options => configuration.GetSection(FaiLicenceOptions.SectionName).Bind(options));
await using var provider = services.BuildServiceProvider();
var client = provider.GetRequiredService<FaiLicenceClient>();

if (!client.IsConfigured)
{
    Console.Error.WriteLine("Not configured. Set FaiLicenceApi:BaseUrl, FaiLicenceApi:Username and FaiLicenceApi:Password, e.g.:");
    Console.Error.WriteLine("  dotnet user-secrets set \"FaiLicenceApi:BaseUrl\" \"https://extranet.fai.org/fai/en/api/v1/\" --project samples/FaiLicence.Sample");
    Console.Error.WriteLine("  dotnet user-secrets set \"FaiLicenceApi:Username\" \"<user>\" --project samples/FaiLicence.Sample");
    Console.Error.WriteLine("  dotnet user-secrets set \"FaiLicenceApi:Password\" \"<password>\" --project samples/FaiLicence.Sample");
    return 1;
}

var parameters = new Dictionary<string, string?>
{
    ["search_number"] = number,
    ["search_name"] = name,
    ["country"] = country,
    ["discipline"] = discipline,
    ["include_invalid"] = "1",
    ["limit_length"] = "100",
};

try
{
    var cards = await client.SearchAsync(parameters);
    if (cards.Count == 0)
    {
        Console.WriteLine("No licences found.");
        return 0;
    }

    // The API returns the oldest card first; show the newest first so the current card is at the top.
    foreach (var card in cards.OrderByDescending(c => c.ValidUntil))
    {
        var status = card.ValidUntil is { } until && until.Date >= DateTime.Today && !card.IsExpired ? "valid" : "expired";
        Console.WriteLine($"{card.Ioc} {card.LicenceNumber,-10} {card.GivenName} {card.Surname,-25} {card.Sport,-10} valid until {card.ValidUntil:yyyy-MM-dd}  {status}  (id {card.IdLicence})");
    }

    // Details of the newest card: suspension and issuer live only on the details endpoint.
    var newest = cards.OrderByDescending(c => c.ValidUntil).First();
    var details = await client.GetDetailsAsync(newest.IdLicence);
    if (details is not null)
        Console.WriteLine($"Newest card {details.IdLicence}: issuer {details.Issuer ?? "?"}, suspended: {(details.Suspended ? "yes" : "no")}");
    return 0;
}
catch (FaiLicenceException ex)
{
    // The message never contains the URL or the credentials.
    Console.Error.WriteLine($"FAI API error: {ex.Message}");
    return 1;
}

static (string? Number, string? Name, string? Country, string Discipline) ParseArguments(string[] args)
{
    string? number = null, name = null, country = null;
    var discipline = "Gliding";
    for (var i = 0; i + 1 < args.Length; i += 2)
    {
        switch (args[i])
        {
            case "--number": number = args[i + 1]; break;
            case "--name": name = args[i + 1]; break;
            case "--country": country = args[i + 1].ToUpperInvariant(); break;
            case "--discipline": discipline = args[i + 1]; break;
        }
    }
    return (number, name, country, discipline);
}

/// <summary>Anchor type for <c>AddUserSecrets</c> (the user-secrets id is in the project file).</summary>
internal sealed class SampleMarker;
