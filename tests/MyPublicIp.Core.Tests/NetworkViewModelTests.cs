using MyPublicIp.Core.ViewModels;

namespace MyPublicIp.Core.Tests;

public sealed class NetworkViewModelTests : IDisposable
{
    private readonly ViewModelFixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public void Shows_the_adapter_used_to_reach_the_internet()
    {
        NetworkViewModel network = _fixture.CreateNetwork();

        Assert.True(network.HasNetwork);
        Assert.Equal("Wi-Fi · Home-5G", network.ConnectionText);
        Assert.Equal(4, network.SignalBars);
        Assert.Equal("192.168.1.42", network.LocalIPv4);
        Assert.Equal("192.168.1.1", network.Gateway);
        Assert.Equal("192.168.1.1\n1.1.1.1", network.DnsServers);
        Assert.Equal("00:15:5D:12:34:56", network.MacAddress);
        Assert.StartsWith("Intel(R) Wi-Fi 6 AX201 · ", network.AdapterText);
    }

    [Fact]
    public void Says_when_there_is_no_network()
    {
        _fixture.LocalNetwork.Details = null;

        NetworkViewModel network = _fixture.CreateNetwork();

        Assert.False(network.HasNetwork);
        Assert.Equal("NoNetwork", network.ConnectionText);
        Assert.Equal("—", network.Gateway);
    }

    [Fact]
    public void Ethernet_shows_no_network_name_when_it_is_the_adapter_name()
    {
        _fixture.LocalNetwork.Details = _fixture.LocalNetwork.Details! with
        {
            AdapterName = "Ethernet",
            Kind = LocalNetwork.ConnectionKind.Ethernet,
            NetworkName = "Ethernet",
            SignalBars = null,
        };

        NetworkViewModel network = _fixture.CreateNetwork();

        Assert.Equal("Ethernet", network.ConnectionText);
        Assert.False(network.HasSignal);
    }

    [Theory]
    [InlineData("gateway", "192.168.1.1")]
    [InlineData("dns", "192.168.1.1, 1.1.1.1")]
    [InlineData("mac", "00:15:5D:12:34:56")]
    [InlineData("local", "192.168.1.42")]
    public async Task Copies_each_value(string item, string expected)
    {
        NetworkViewModel network = _fixture.CreateNetwork();

        Task copy = network.CopyCommand.ExecuteAsync(item);

        Assert.Equal(expected, _fixture.Clipboard.Text);
        Assert.Equal(item, network.CopiedItem);
        _fixture.Time.Advance(TimeSpan.FromSeconds(1.5));
        await copy;
        Assert.Null(network.CopiedItem);
    }

    [Theory]
    [InlineData(866_000_000, "866 Mbit/s")]
    [InlineData(2_500_000_000, "2.5 Gbit/s")]
    public void Formats_the_link_speed(long bitsPerSecond, string expected)
    {
        using var culture = new CultureScope("en-US");

        Assert.Equal(expected, NetworkViewModel.FormatSpeed(bitsPerSecond));
    }
}
