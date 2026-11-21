using System.Net;
using AlfaVision.FaiLicence.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace AlfaVision.FaiLicence.Tests;

/// <summary>The helpers consumers test with must themselves behave like FAI — otherwise a green test proves nothing.</summary>
public class TestingHelpersTests
{
    private static readonly IOptions<FaiLicenceOptions> Options = Microsoft.Extensions.Options.Options.Create(
        new FaiLicenceOptions { BaseUrl = "https://fai.test/fai/en/api/v1/", Username = "nac-api-user", Password = "secret" });

    [Fact]
    public async Task Fake_FiltersLikeFai_AndRecordsCalls()
    {
        var fake = new FakeFaiLicenceClient();
        fake.Licences.AddRange(FaiLicenceTestData.Cards);
        foreach (var (id, d) in FaiLicenceTestData.Details) fake.Details[id] = d;

        var valid = await fake.SearchAsync(new Dictionary<string, string?> { ["country"] = "DEN", ["discipline"] = "Gliding" });
        Assert.Equal([103, 101], valid.Select(c => c.IdLicence));                      // expired card hidden; oldest issue date first (Bo 2026-01, Anne 2026-04)

        var all = await fake.SearchAsync(new Dictionary<string, string?> { ["search_number"] = "DSvU1002", ["include_invalid"] = "1" });
        Assert.Equal([102, 103], all.Select(c => c.IdLicence));

        var byLicenceeId = await fake.SearchAsync(new Dictionary<string, string?> { ["search_number"] = "9101" });
        Assert.Equal("GFA-55", Assert.Single(byLicenceeId).LicenceNumber);

        var byName = await fake.SearchAsync(new Dictionary<string, string?> { ["search_name"] = "flyver", ["include_invalid"] = "true", ["limit_start"] = "1", ["limit_length"] = "1" });
        Assert.Equal(103, Assert.Single(byName).IdLicence);

        Assert.Equal("Dansk Svaeveflyver Union", (await fake.GetDetailsAsync(101))!.Issuer);
        Assert.Null(await fake.GetDetailsAsync(999));
        Assert.Equal(4, fake.Searches.Count);
        Assert.Equal([101, 999], fake.DetailRequests);
    }

    [Fact]
    public async Task Fake_ErrorAndNotConfigured_Throw()
    {
        var fake = new FakeFaiLicenceClient { Error = new FaiLicenceException("The FAI licence database answered 503.") };
        var ex = await Assert.ThrowsAsync<FaiLicenceException>(() => fake.SearchAsync(new Dictionary<string, string?>()));
        Assert.Contains("503", ex.Message);

        var unconfigured = new FakeFaiLicenceClient { IsConfigured = false };
        await Assert.ThrowsAsync<InvalidOperationException>(() => unconfigured.GetDetailsAsync(1));
    }

    [Fact]
    public async Task Handler_ServesSearchAndDetails_ThroughTheRealClient()
    {
        var handler = new FaiLicenceTestHandler { Username = "nac-api-user", Password = "secret" };
        var client = new FaiLicenceClient(new HttpClient(handler), Options, Microsoft.Extensions.Logging.Abstractions.NullLogger<FaiLicenceClient>.Instance);

        var cards = await client.SearchAsync(new Dictionary<string, string?> { ["search_number"] = "DSvU1002", ["include_invalid"] = "1" });
        Assert.Equal([102, 103], cards.Select(c => c.IdLicence));
        Assert.True(cards[0].IsExpired);                                            // 1/0 round-trips through the parser
        Assert.Equal(new DateTime(2026, 12, 31), cards[1].ValidUntil);
        Assert.Equal("Flyver", cards[1].Surname);

        var details = await client.GetDetailsAsync(201);
        Assert.NotNull(details);
        Assert.Equal("GFA-55", details!.LicenceNumber);
        Assert.False(details.Suspended);

        Assert.Equal(2, handler.Requests.Count);
        Assert.Contains("licence/201?", handler.Requests[1].ToString());
    }

    [Fact]
    public async Task Handler_WrongCredentials_UnknownId_AndOutage()
    {
        var wrong = new FaiLicenceTestHandler { Username = "someone-else" };
        var ex = await Assert.ThrowsAsync<FaiLicenceException>(() => new FaiLicenceClient(new HttpClient(wrong), Options, Microsoft.Extensions.Logging.Abstractions.NullLogger<FaiLicenceClient>.Instance).GetDetailsAsync(101));
        Assert.Contains("403", ex.Message);
        Assert.DoesNotContain("secret", ex.Message);

        var ok = new FaiLicenceTestHandler();
        var notFound = await Assert.ThrowsAsync<FaiLicenceException>(() => new FaiLicenceClient(new HttpClient(ok), Options, Microsoft.Extensions.Logging.Abstractions.NullLogger<FaiLicenceClient>.Instance).GetDetailsAsync(999));
        Assert.Contains("404", notFound.Message);

        var down = new FaiLicenceTestHandler { FailWith = HttpStatusCode.ServiceUnavailable };
        var outage = await Assert.ThrowsAsync<FaiLicenceException>(() => new FaiLicenceClient(new HttpClient(down), Options, Microsoft.Extensions.Logging.Abstractions.NullLogger<FaiLicenceClient>.Instance).SearchAsync(new Dictionary<string, string?>()));
        Assert.Contains("503", outage.Message);
    }

    [Fact]
    public void Registration_ExposesTheInterface_AsTheSameClient()
    {
        var services = new ServiceCollection();
        services.AddFaiLicenceClient(o => { o.BaseUrl = "https://fai.test/"; o.Username = "u"; o.Password = "p"; });
        using var sp = services.BuildServiceProvider();
        using var scope = sp.CreateScope();

        var viaInterface = scope.ServiceProvider.GetRequiredService<IFaiLicenceClient>();
        Assert.IsType<FaiLicenceClient>(viaInterface);
        Assert.True(viaInterface.IsConfigured);
    }

    [Fact]
    public void Fake_CanReplaceTheRegistration()
    {
        var services = new ServiceCollection();
        services.AddFaiLicenceClient(o => { o.BaseUrl = "https://fai.test/"; o.Username = "u"; o.Password = "p"; });
        var fake = new FakeFaiLicenceClient();
        services.AddSingleton<IFaiLicenceClient>(fake);                                 // last registration wins
        using var sp = services.BuildServiceProvider();

        Assert.Same(fake, sp.GetRequiredService<IFaiLicenceClient>());
    }
}
