using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace AlfaVision.FaiLicence.Tests;

public class FaiLicenceClientTests
{
    private const string Password = "secret-password";
    private const string BaseUrl = "https://fai.test/fai/en/api/v1/";

    /// <summary>Stand-in for FAI: answers search and details from an in-memory list, and records every request.</summary>
    private sealed class FaiStub : HttpMessageHandler
    {
        public List<Uri> Requests { get; } = [];
        public List<object> Licences { get; } = [];
        public HttpStatusCode? Fail { get; set; }
        public string? RawBody { get; set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests.Add(request.RequestUri!);
            if (Fail is { } f) return Task.FromResult(new HttpResponseMessage(f));
            if (RawBody is { } raw) return Task.FromResult(Json(raw));
            object body;
            if (request.RequestUri!.AbsolutePath.Contains("/licence/"))
            {
                var id = int.Parse(request.RequestUri.Segments[^1]);
                var l = Licences.Cast<dynamic>().Single(x => (int)x.idlicence == id);
                body = new { idlicence = id, licencenumber_lic = (string)l.licencenumber_lic, validuntil_lic = (string)l.validuntil_lic,
                             is_expired = (bool)l.is_expired, suspended_lip = 0, idissue_lic = "Some Aero Club" };
            }
            else body = Licences;
            return Task.FromResult(Json(JsonSerializer.Serialize(body)));
        }

        private static HttpResponseMessage Json(string text) =>
            new(HttpStatusCode.OK) { Content = new StringContent(text, Encoding.UTF8, "application/json") };
    }

    private readonly FaiStub _stub = new();
    private FaiLicenceOptions Options { get; } = new() { BaseUrl = BaseUrl, Username = "nac-api-user", Password = Password };
    private FaiLicenceClient Client => new(new HttpClient(_stub), Microsoft.Extensions.Options.Options.Create(Options));

    private static object Licence(int id, string ioc, string number, string given, string surname, string validUntil, bool expired) =>
        new { idlicence = id, idlicencee = 1000 + id, IOC = ioc, licencenumber_lic = number, givenname_lip = given, surname_lip = surname,
              birthdate_lip = "1980-05-17 00:00:00", Sport = "Gliding", dateissued_lic = "2024-01-01", validuntil_lic = validUntil, is_expired = expired };

    [Fact]
    public void BuildUrl_IsReadOnly_AndBase64EncodesThePassword()
    {
        var url = Client.BuildUrl("licences", new Dictionary<string, string?> { ["search_number"] = "3864" });
        Assert.StartsWith(BaseUrl + "licences?search_number=3864&auth_username=nac-api-user&auth_password=", url);
        Assert.Contains("auth_password=" + Uri.EscapeDataString(Convert.ToBase64String(Encoding.UTF8.GetBytes(Password))), url);
        Assert.DoesNotContain(Password, url);
        Assert.StartsWith(BaseUrl + "licence/123?", Client.BuildUrl("licence/123", new Dictionary<string, string?>()));
        foreach (var forbidden in new[] { "create", "licence/1/../create", "licence/abc", "update", "licence/", "licences/create" })
            Assert.True(Record.Exception(() => Client.BuildUrl(forbidden, new Dictionary<string, string?>())) is InvalidOperationException, "should be blocked: " + forbidden);
    }

    [Fact]
    public void BuildUrl_SkipsNullParameters_AndEscapesValues()
    {
        var url = Client.BuildUrl("licences", new Dictionary<string, string?> { ["search_name"] = "Boye-Møller", ["country"] = null });
        Assert.Contains("search_name=Boye-M%C3%B8ller", url);
        Assert.DoesNotContain("country=", url);
    }

    [Fact]
    public async Task Search_MapsFields_AndAcceptsBothExpiryEncodings()
    {
        _stub.Licences.Add(Licence(1, "DEN", "3864", "Arne", "Boye-M?ller", "2023-12-31", expired: true));
        _stub.Licences.Add(new { idlicence = "2", idlicencee = "77", IOC = "AUS", licencenumber_lic = "A-12", givenname_lip = "Jane",
                                 surname_lip = "Pilot", sport = "Gliding", validuntil_lic = "2027-03-31 00:00:00", is_expired = 0 });

        var result = await Client.SearchAsync(new Dictionary<string, string?> { ["discipline"] = "Gliding" });

        Assert.Equal(2, result.Count);
        var first = result[0];
        Assert.Equal(1, first.IdLicence);
        Assert.Equal(1001, first.IdLicencee);
        Assert.Equal("DEN", first.Ioc);
        Assert.Equal(new DateTime(1980, 5, 17), first.BirthDate);
        Assert.Equal(new DateTime(2023, 12, 31), first.ValidUntil);
        Assert.True(first.IsExpired);
        var second = result[1];
        Assert.Equal(2, second.IdLicence);
        Assert.Equal(77, second.IdLicencee);
        Assert.Equal("Gliding", second.Sport);
        Assert.False(second.IsExpired);
        Assert.Null(second.BirthDate);
    }

    [Fact]
    public async Task Details_MapsFields()
    {
        _stub.Licences.Add(Licence(5, "AUS", "A-5", "Jane", "Pilot", "2027-03-31", expired: false));
        var d = await Client.GetDetailsAsync(5);
        Assert.NotNull(d);
        Assert.Equal(5, d!.IdLicence);
        Assert.Equal("A-5", d.LicenceNumber);
        Assert.Equal(new DateTime(2027, 3, 31), d.ValidUntil);
        Assert.False(d.IsExpired);
        Assert.False(d.Suspended);
        Assert.Equal("Some Aero Club", d.Issuer);
        Assert.Single(_stub.Requests);
        Assert.Equal("/fai/en/api/v1/licence/5", _stub.Requests[0].AbsolutePath);
    }

    [Fact]
    public async Task Details_ReturnsNull_WhenTheApiReturnsNothing()
    {
        _stub.RawBody = "[]";
        Assert.Null(await Client.GetDetailsAsync(42));
    }

    [Fact]
    public async Task Search_ReturnsEmpty_WhenTheApiDoesNotReturnAnArray()
    {
        _stub.RawBody = "{\"error\":\"nothing\"}";
        Assert.Empty(await Client.SearchAsync(new Dictionary<string, string?>()));
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden, "403")]
    [InlineData(HttpStatusCode.InternalServerError, "500")]
    public async Task Errors_NeverContainCredentialsOrUrl(HttpStatusCode status, string expected)
    {
        _stub.Fail = status;
        var ex = await Assert.ThrowsAsync<FaiLicenceException>(() => Client.SearchAsync(new Dictionary<string, string?>()));
        Assert.Contains(expected, ex.Message);
        AssertNoSecrets(ex.Message);
    }

    [Fact]
    public async Task InvalidJson_IsReportedWithoutSecrets()
    {
        _stub.RawBody = "<html>login</html>";
        var ex = await Assert.ThrowsAsync<FaiLicenceException>(() => Client.SearchAsync(new Dictionary<string, string?>()));
        Assert.Contains("invalid response", ex.Message);
        AssertNoSecrets(ex.Message);
    }

    [Fact]
    public async Task NotConfigured_IsAnError_BeforeAnyRequest()
    {
        var client = new FaiLicenceClient(new HttpClient(_stub), Microsoft.Extensions.Options.Options.Create(new FaiLicenceOptions()));
        Assert.False(client.IsConfigured);
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.SearchAsync(new Dictionary<string, string?>()));
        Assert.Empty(_stub.Requests);
    }

    [Fact]
    public void AddFaiLicenceClient_RegistersTypedClient_WithOptions()
    {
        var services = new ServiceCollection();
        services.AddFaiLicenceClient(o => { o.BaseUrl = BaseUrl; o.Username = "u"; o.Password = "p"; });
        using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<FaiLicenceClient>();
        Assert.True(client.IsConfigured);
        Assert.StartsWith(BaseUrl + "licences?", client.BuildUrl("licences", new Dictionary<string, string?>()));
    }

    private void AssertNoSecrets(string message)
    {
        Assert.DoesNotContain("auth_", message);
        Assert.DoesNotContain(Password, message);
        Assert.DoesNotContain(Convert.ToBase64String(Encoding.UTF8.GetBytes(Password)), message);
        Assert.DoesNotContain("fai.test", message);
        Assert.DoesNotContain(Options.Username!, message);
    }
}
