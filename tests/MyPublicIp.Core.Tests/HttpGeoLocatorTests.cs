using System.Net;
using MyPublicIp.Core.Geo;

namespace MyPublicIp.Core.Tests;

public sealed class HttpGeoLocatorTests
{
    private const string Address = "203.0.113.42";
    private const string IpInfo = $"https://ipinfo.io/{Address}/json";
    private const string IpApi = $"https://ipapi.co/{Address}/json/";

    [Fact]
    public async Task Reads_the_location_and_the_provider()
    {
        var handler = new StubHttpHandler().Respond(IpInfo, HttpStatusCode.OK,
            """{"ip":"203.0.113.42","city":"Paris","region":"Île-de-France","country":"FR","org":"AS64500 Aurora Telecom SA"}""");

        GeoInfo? info = await Locate(handler);

        Assert.Equal(new GeoInfo("FR", "Paris", "Île-de-France", "Aurora Telecom SA"), info);
    }

    [Fact]
    public async Task Falls_back_to_the_second_service()
    {
        var handler = new StubHttpHandler()
            .Respond(IpInfo, HttpStatusCode.TooManyRequests)
            .Respond(IpApi, HttpStatusCode.OK,
                """{"ip":"203.0.113.42","city":"Paris","region":"Île-de-France","country_code":"FR","org":"Aurora Telecom SA"}""");

        GeoInfo? info = await Locate(handler);

        Assert.Equal(new GeoInfo("FR", "Paris", "Île-de-France", "Aurora Telecom SA"), info);
    }

    [Fact]
    public async Task Returns_null_for_private_or_unknown_addresses()
    {
        var handler = new StubHttpHandler()
            .Respond(IpInfo, HttpStatusCode.OK, """{"ip":"203.0.113.42","bogon":true}""")
            .Respond(IpApi, HttpStatusCode.OK, """{"ip":"203.0.113.42","error":true,"reason":"Reserved IP Address"}""");

        Assert.Null(await Locate(handler));
    }

    [Fact]
    public async Task Ignores_an_unexpected_answer()
    {
        var handler = new StubHttpHandler()
            .Respond(IpInfo, HttpStatusCode.OK, "<html>Rate limited</html>")
            .Fail(IpApi);

        Assert.Null(await Locate(handler));
    }

    [Fact]
    public async Task Asks_only_once_per_address()
    {
        var handler = new StubHttpHandler().Respond(IpInfo, HttpStatusCode.OK, """{"country":"FR"}""");
        var locator = new HttpGeoLocator(new HttpClient(handler));

        await locator.LocateAsync(Address, TestContext.Current.CancellationToken);
        await locator.LocateAsync(Address, TestContext.Current.CancellationToken);

        Assert.Single(handler.Requests);
    }

    [Theory]
    [InlineData("AS3215 Orange S.A.", "Orange S.A.")]
    [InlineData("Free SAS", "Free SAS")]
    [InlineData("", null)]
    public void Cleans_the_provider_name(string organization, string? expected)
    {
        Assert.Equal(expected, HttpGeoLocator.CleanProvider(organization));
    }

    private static Task<GeoInfo?> Locate(StubHttpHandler handler) =>
        new HttpGeoLocator(new HttpClient(handler)).LocateAsync(Address, TestContext.Current.CancellationToken);
}
