using MyPublicIp.Core.Abstractions;
using MyPublicIp.Core.History;
using MyPublicIp.Core.LocalNetwork;
using MyPublicIp.Core.PublicIp;
using MyPublicIp.Core.ViewModels;

namespace MyPublicIp.Core.Tests;

public sealed class MainViewModelTests : IDisposable
{
    private static readonly DateTimeOffset Now = ViewModelFixture.Now;

    private readonly ViewModelFixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public void Shows_placeholders_while_looking_up()
    {
        MainViewModel viewModel = _fixture.CreateMain();

        Assert.Equal("…", viewModel.IPv4Text);
        Assert.Equal("…", viewModel.IPv6Text);
        Assert.False(viewModel.CopyIPv4Command.CanExecute(null));
        Assert.True(viewModel.IsRefreshing);
        Assert.Equal("AppName\nStatusLookingUp", viewModel.TrayToolTip);
    }

    [Fact]
    public void Shows_the_addresses_their_location_and_the_vpn_in_the_tooltip()
    {
        _fixture.LocalNetwork.Vpn = new VpnStatus(true, "NordLynx");
        MainViewModel viewModel = _fixture.CreateMain();

        Publish("203.0.113.42", "2001:db8::42");

        Assert.Equal("203.0.113.42", viewModel.IPv4Text);
        Assert.Equal("2001:db8::42", viewModel.IPv6Text);
        Assert.True(viewModel.CopyIPv4Command.CanExecute(null));
        Assert.False(viewModel.IsStatusOpen);
        string expectedCountry = new GeoInfoProbe("FR").CountryName;
        Assert.Equal(
            $"AppName\nToolTipLine(IPv4|203.0.113.42)\n{expectedCountry} · Aurora Telecom\nToolTipVpn(NordLynx)\nToolTipLine(IPv6|2001:db8::42)",
            viewModel.TrayToolTip);
    }

    [Fact]
    public void Says_when_there_is_no_IPv6()
    {
        MainViewModel viewModel = _fixture.CreateMain();

        Publish("203.0.113.42", null);

        Assert.Equal("NotAvailable", viewModel.IPv6Text);
        Assert.False(viewModel.CopyIPv6Command.CanExecute(null));
    }

    [Theory]
    [InlineData(IpLookupStatus.Offline, StatusKind.Offline, "StatusOffline")]
    [InlineData(IpLookupStatus.Failed, StatusKind.Failed, "StatusFailed")]
    public void Explains_why_there_is_no_address(IpLookupStatus status, StatusKind expectedKind, string expectedTitle)
    {
        MainViewModel viewModel = _fixture.CreateMain();

        _fixture.Monitor.Publish(new IpSnapshot(status, null, null, null, IsRefreshing: false));

        Assert.True(viewModel.IsStatusOpen);
        Assert.Equal(expectedKind, viewModel.Status);
        Assert.Equal(expectedTitle, viewModel.StatusTitle);
        Assert.Equal("—", viewModel.IPv4Text);
        Assert.Equal($"AppName\n{expectedTitle}", viewModel.TrayToolTip);
    }

    [Fact]
    public void Cuts_the_tooltip_to_the_Windows_limit()
    {
        _fixture.Localizer = new LongLocalizer();
        MainViewModel viewModel = _fixture.CreateMain();

        Publish("203.0.113.42", "2001:db8:85a3:8d3:1319:8a2e:370:7348");

        Assert.Equal(MainViewModel.MaxToolTipLength, viewModel.TrayToolTip.Length);
    }

    [Fact]
    public async Task Copies_the_address_and_shows_a_short_confirmation()
    {
        MainViewModel viewModel = _fixture.CreateMain();
        Publish("203.0.113.42", null);

        Task copy = viewModel.CopyIPv4Command.ExecuteAsync(null);

        Assert.Equal("203.0.113.42", _fixture.Clipboard.Text);
        Assert.Equal("ipv4", viewModel.CopiedItem);

        _fixture.Time.Advance(TimeSpan.FromSeconds(1.5));
        await copy;
        Assert.Null(viewModel.CopiedItem);
    }

    [Fact]
    public void The_keyboard_shortcut_copies_the_address_and_confirms_it()
    {
        MainViewModel viewModel = _fixture.CreateMain();
        Publish("203.0.113.42", null);

        _fixture.Hotkey.Press();

        Assert.Equal("203.0.113.42", _fixture.Clipboard.Text);
        Assert.Equal(["203.0.113.42"], _fixture.Notifier.Copied);
    }

    [Fact]
    public void Refresh_asks_the_monitor()
    {
        MainViewModel viewModel = _fixture.CreateMain();

        viewModel.RefreshCommand.Execute(null);

        Assert.Equal(1, _fixture.Monitor.RefreshRequests);
    }

    [Fact]
    public void Locates_each_new_address_once_and_records_its_country()
    {
        MainViewModel viewModel = _fixture.CreateMain();

        Publish("203.0.113.42", null);
        Publish("203.0.113.42", null, minutes: 15);

        Assert.Equal(["203.0.113.42"], _fixture.Geo.Requests);
        Assert.Equal("FR", viewModel.Connection.CountryCode);
        Assert.Equal("FR", viewModel.History[0].CountryCode);
    }

    [Fact]
    public void Lists_recent_addresses_with_readable_times()
    {
        var older = new IpHistoryEntry(Now.AddDays(-5), "192.0.2.81", null, CountryCode: "ES");
        var yesterday = new IpHistoryEntry(Now.AddDays(-1), "198.51.100.7", null, VpnName: "NordLynx", CountryCode: "DE");
        _fixture.HistoryStore.Save([yesterday, older]);
        MainViewModel viewModel = _fixture.CreateMain();

        Publish("203.0.113.42", null);

        Assert.Equal(
            [
                new HistoryRow(Now.ToString("t"), "203.0.113.42", "FR", ThroughVpn: false, IsCurrent: true),
                new HistoryRow($"Yesterday({yesterday.SeenAt:t})", "198.51.100.7", "DE", ThroughVpn: true, IsCurrent: false),
                new HistoryRow(older.SeenAt.ToString("g"), "192.0.2.81", "ES", ThroughVpn: false, IsCurrent: false),
            ],
            viewModel.History);
    }

    [Fact]
    public void Records_the_vpn_with_the_address_and_notifies_the_change()
    {
        MainViewModel viewModel = _fixture.CreateMain();
        Publish("203.0.113.42", null);

        _fixture.LocalNetwork.Vpn = new VpnStatus(true, "NordLynx");
        Publish("198.51.100.7", null, minutes: 5);

        (IpHistoryEntry previous, IpHistoryEntry current) = Assert.Single(_fixture.Notifier.Notifications);
        Assert.Null(previous.VpnName);
        Assert.Equal("NordLynx", current.VpnName);
        Assert.True(viewModel.Connection.IsVpnActive);
    }

    [Fact]
    public void Showing_the_flyout_reads_the_network_again()
    {
        MainViewModel viewModel = _fixture.CreateMain();
        _fixture.LocalNetwork.Vpn = new VpnStatus(true, "WireGuard");

        viewModel.OnFlyoutVisibilityChanged(visible: true);

        Assert.Equal("VpnOn(WireGuard)", viewModel.Connection.VpnText);
    }

    private void Publish(string? ipv4, string? ipv6, int minutes = 0) =>
        _fixture.Monitor.Publish(new IpSnapshot(IpLookupStatus.Ok, ipv4, ipv6, Now.AddMinutes(minutes), IsRefreshing: false));

    /// <summary>Gets the country name the same way the app does, whatever the culture of the test machine.</summary>
    private sealed record GeoInfoProbe(string Code)
    {
        public string CountryName => new Geo.GeoInfo(Code, null, null, null).CountryName;
    }

    private sealed class LongLocalizer : ILocalizer
    {
        public string Get(string key) => new('x', 80);

        public string Format(string key, params object?[] args) => new('y', 60);
    }
}
