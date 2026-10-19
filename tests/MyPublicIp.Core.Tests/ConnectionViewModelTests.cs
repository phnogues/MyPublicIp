using MyPublicIp.Core.Geo;
using MyPublicIp.Core.Latency;
using MyPublicIp.Core.LocalNetwork;
using MyPublicIp.Core.ViewModels;

namespace MyPublicIp.Core.Tests;

public sealed class ConnectionViewModelTests : IDisposable
{
    private readonly ViewModelFixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public async Task Shows_where_the_address_is_and_who_provides_it()
    {
        ConnectionViewModel connection = _fixture.CreateConnection();

        GeoInfo? info = await connection.LocateAsync("203.0.113.42");

        Assert.NotNull(info);
        Assert.True(connection.HasLocation);
        Assert.Equal("FR", connection.CountryCode);
        Assert.Equal($"Paris, Île-de-France, {info.CountryName}", connection.LocationText);
        Assert.Equal("Aurora Telecom", connection.ProviderText);
    }

    [Fact]
    public async Task Hides_the_location_when_it_is_unknown()
    {
        ConnectionViewModel connection = _fixture.CreateConnection();
        await connection.LocateAsync("203.0.113.42");

        await connection.LocateAsync("192.0.2.200");

        Assert.False(connection.HasLocation);
        Assert.Equal(string.Empty, connection.LocationText);
    }

    [Fact]
    public void Shows_the_vpn_state()
    {
        ConnectionViewModel connection = _fixture.CreateConnection();
        Assert.Equal("VpnOff", connection.VpnText);

        connection.UpdateVpn(new VpnStatus(true, "NordLynx"));

        Assert.True(connection.IsVpnActive);
        Assert.Equal("VpnOn(NordLynx)", connection.VpnText);
    }

    [Fact]
    public async Task Shows_the_latency_and_its_quality()
    {
        ConnectionViewModel connection = _fixture.CreateConnection();
        Assert.Equal("LatencyMeasuring", connection.LatencyText);

        _fixture.Probe.Enqueue(TimeSpan.FromMilliseconds(18));
        _fixture.Latency.Start();
        await Wait.UntilAsync(() => connection.Quality != ConnectionQuality.Unknown, "the first measurement is shown");

        Assert.Equal(ConnectionQuality.Excellent, connection.Quality);
        Assert.Equal("LatencyValue(18|QualityExcellent)", connection.LatencyText);
        Assert.Equal([18d], connection.LatencyHistory);
    }

    [Fact]
    public async Task Says_when_the_server_does_not_answer()
    {
        ConnectionViewModel connection = _fixture.CreateConnection();

        _fixture.Probe.Default = null;
        _fixture.Latency.Start();
        await Wait.UntilAsync(() => connection.Quality != ConnectionQuality.Unknown, "the first measurement is shown");

        Assert.Equal(ConnectionQuality.Unreachable, connection.Quality);
        Assert.Equal("QualityUnreachable", connection.LatencyText);
    }
}
