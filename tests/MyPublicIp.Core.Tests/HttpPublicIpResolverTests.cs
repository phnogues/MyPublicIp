using System.Net;
using System.Net.Sockets;
using MyPublicIp.Core.PublicIp;

namespace MyPublicIp.Core.Tests;

public sealed class HttpPublicIpResolverTests
{
    private const string First = "https://first.test/";
    private const string Second = "https://second.test/";
    private const string Third = "https://third.test/";

    private static readonly PublicIpEndpoints Endpoints = new(
        IPv4: [new Uri(First), new Uri(Second), new Uri(Third)],
        IPv6: [new Uri("https://v6.test/")]);

    [Fact]
    public async Task Returns_the_address_from_the_first_service()
    {
        var handler = new StubHttpHandler().Respond(First, HttpStatusCode.OK, "203.0.113.42\n");

        IPAddress? address = await ResolveIPv4(handler);

        Assert.Equal(IPAddress.Parse("203.0.113.42"), address);
        Assert.Equal([First], handler.Requests);
    }

    [Fact]
    public async Task Falls_back_to_the_next_service_on_errors_and_timeouts()
    {
        var handler = new StubHttpHandler()
            .Respond(First, HttpStatusCode.ServiceUnavailable)
            .TimeOut(Second)
            .Respond(Third, HttpStatusCode.OK, "203.0.113.42");

        IPAddress? address = await ResolveIPv4(handler);

        Assert.Equal(IPAddress.Parse("203.0.113.42"), address);
        Assert.Equal([First, Second, Third], handler.Requests);
    }

    [Theory]
    [InlineData("<html>Rate limited</html>")]
    [InlineData("1")]
    [InlineData("10.1")]
    [InlineData("2001:db8::1")]
    public async Task Ignores_answers_that_are_not_an_IPv4_address(string body)
    {
        var handler = new StubHttpHandler()
            .Respond(First, HttpStatusCode.OK, body)
            .Respond(Second, HttpStatusCode.OK, "203.0.113.42");

        IPAddress? address = await ResolveIPv4(handler);

        Assert.Equal(IPAddress.Parse("203.0.113.42"), address);
    }

    [Fact]
    public async Task Returns_null_when_every_service_fails()
    {
        var handler = new StubHttpHandler().Fail(First).Fail(Second).Fail(Third);

        Assert.Null(await ResolveIPv4(handler));
    }

    [Fact]
    public async Task Resolves_IPv6_with_the_IPv6_services()
    {
        var handler = new StubHttpHandler().Respond("https://v6.test/", HttpStatusCode.OK, "2001:db8:85a3::8a2e:370:7334");
        var resolver = new HttpPublicIpResolver(new HttpClient(handler), Endpoints);

        IPAddress? address = await resolver.ResolveAsync(AddressFamily.InterNetworkV6, TestContext.Current.CancellationToken);

        Assert.Equal(IPAddress.Parse("2001:db8:85a3::8a2e:370:7334"), address);
    }

    [Fact]
    public async Task Stops_when_cancelled()
    {
        var handler = new StubHttpHandler().Respond(First, HttpStatusCode.OK, "203.0.113.42");
        var resolver = new HttpPublicIpResolver(new HttpClient(handler), Endpoints);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => resolver.ResolveAsync(AddressFamily.InterNetwork, new CancellationToken(canceled: true)));
    }

    private static Task<IPAddress?> ResolveIPv4(StubHttpHandler handler) =>
        new HttpPublicIpResolver(new HttpClient(handler), Endpoints)
            .ResolveAsync(AddressFamily.InterNetwork, TestContext.Current.CancellationToken);
}
