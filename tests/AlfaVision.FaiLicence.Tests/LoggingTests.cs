using System.Net;
using AlfaVision.FaiLicence.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Options;
using Xunit;

namespace AlfaVision.FaiLicence.Tests;

/// <summary>
/// The client logs its activity through <see cref="ILogger{TCategoryName}"/> at a useful technical level — and never the
/// URL, the credentials, search values or response content (the API carries the password in the query string).
/// </summary>
public class LoggingTests
{
    private const string Password = "very-secret-password";
    private const string Username = "nac-api-user";

    private static readonly IOptions<FaiLicenceOptions> Options = Microsoft.Extensions.Options.Options.Create(
        new FaiLicenceOptions { BaseUrl = "https://fai.test/fai/en/api/v1/", Username = Username, Password = Password });

    private static (FaiLicenceClient Client, FakeLogger<FaiLicenceClient> Log) Client(HttpMessageHandler handler)
    {
        var log = new FakeLogger<FaiLicenceClient>();
        return (new FaiLicenceClient(new HttpClient(handler), Options, log), log);
    }

    private static FaiLicenceTestHandler Fai()
    {
        var h = new FaiLicenceTestHandler { Username = Username, Password = Password };
        h.Licences.AddRange(FaiLicenceTestData.Cards);
        foreach (var (id, d) in FaiLicenceTestData.Details) h.Details[id] = d;
        return h;
    }

    [Fact]
    public async Task Search_LogsStartAtDebug_AndResultAtInformation_WithParameterNamesOnly()
    {
        var (client, log) = Client(Fai());
        await client.SearchAsync(new Dictionary<string, string?> { ["search_name"] = "Jensen", ["country"] = "DEN", ["include_invalid"] = "1" });

        var records = log.Collector.GetSnapshot();
        var start = Assert.Single(records, r => r.Level == LogLevel.Debug);
        Assert.Contains("[country,include_invalid,search_name]", start.Message);
        var done = Assert.Single(records, r => r.Level == LogLevel.Information);
        Assert.Matches(@"returned \d+ licences in \d+ ms", done.Message);
        Assert.All(records, r => Assert.DoesNotContain("Jensen", r.Message));   // search values may be a person's name
    }

    [Fact]
    public async Task Details_LogsIdAndFlags()
    {
        var (client, log) = Client(Fai());
        var id = FaiLicenceTestData.AnneValid.IdLicence;
        var details = await client.GetDetailsAsync(id);

        Assert.NotNull(details);
        var done = Assert.Single(log.Collector.GetSnapshot(), r => r.Level == LogLevel.Information);
        Assert.Contains($"licence {id}:", done.Message);
        Assert.Contains("expired=False", done.Message);
    }

    [Fact]
    public async Task Failures_LogAtWarning_WithStatusAndEndpoint()
    {
        var wrong = new FaiLicenceTestHandler { Username = Username, Password = "other" };
        var (client, log) = Client(wrong);
        await Assert.ThrowsAsync<FaiLicenceException>(() => client.GetDetailsAsync(101));

        var warning = Assert.Single(log.Collector.GetSnapshot(), r => r.Level == LogLevel.Warning);
        Assert.Contains("answered 403", warning.Message);
        Assert.Contains("licence/{id}", warning.Message);
    }

    [Fact]
    public async Task NotConfigured_LogsWarning_BeforeThrowing()
    {
        var log = new FakeLogger<FaiLicenceClient>();
        var client = new FaiLicenceClient(new HttpClient(Fai()), Microsoft.Extensions.Options.Options.Create(new FaiLicenceOptions()), log);
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.SearchAsync(new Dictionary<string, string?>()));
        Assert.Contains(log.Collector.GetSnapshot(), r => r.Level == LogLevel.Warning && r.Message.Contains("not configured"));
    }

    [Fact]
    public async Task NothingLogged_ContainsUrlCredentialsOrResponseContent()
    {
        var fai = Fai();
        var (client, log) = Client(fai);
        await client.SearchAsync(new Dictionary<string, string?> { ["search_number"] = FaiLicenceTestData.AnneValid.LicenceNumber });
        await client.GetDetailsAsync(FaiLicenceTestData.AnneValid.IdLicence);
        await Assert.ThrowsAsync<FaiLicenceException>(() => client.GetDetailsAsync(999_999));                    // 404
        await Assert.ThrowsAsync<FaiLicenceException>(() => Client(new Down()).Client.SearchAsync(new Dictionary<string, string?>()));

        var all = string.Join("\n", log.Collector.GetSnapshot().Select(r => r.Message + " " + r.Exception));
        Assert.DoesNotContain(Password, all);
        Assert.DoesNotContain(Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(Password)), all);
        Assert.DoesNotContain(Username, all);
        Assert.DoesNotContain("fai.test", all);
        Assert.DoesNotContain("auth_", all);
        Assert.DoesNotContain(FaiLicenceTestData.AnneValid.Surname!, all);
        Assert.NotEmpty(all);
    }

    [Fact]
    public void Di_ResolvesTheLogger_WithoutExtraRegistration()
    {
        var services = new ServiceCollection();
        services.AddFaiLicenceClient(o => { o.BaseUrl = "https://fai.test/"; o.Username = "u"; o.Password = "p"; });
        using var provider = services.BuildServiceProvider();
        Assert.NotNull(provider.GetRequiredService<IFaiLicenceClient>());   // AddHttpClient registers logging itself
    }

    private sealed class Down : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => throw new HttpRequestException("boom", null, HttpStatusCode.ServiceUnavailable);
    }
}
