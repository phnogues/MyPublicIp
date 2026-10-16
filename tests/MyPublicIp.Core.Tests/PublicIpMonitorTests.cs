using System.Net.Sockets;
using Microsoft.Extensions.Time.Testing;
using MyPublicIp.Core.PublicIp;

namespace MyPublicIp.Core.Tests;

public sealed class PublicIpMonitorTests : IDisposable
{
    private static readonly RefreshPolicy Policy = new(
        Interval: TimeSpan.FromMinutes(15),
        NetworkSettleDelay: TimeSpan.FromSeconds(2),
        RetryDelays: [TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(30)]);

    private readonly FakeResolver _resolver = new();
    private readonly FakeNetwork _network = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 16, 10, 42, 0, TimeSpan.Zero));
    private readonly PublicIpMonitor _monitor;

    public PublicIpMonitorTests()
    {
        _monitor = new PublicIpMonitor(_resolver, _network, _time, Policy);
    }

    public void Dispose() => _monitor.Dispose();

    [Fact]
    public void Starts_pending()
    {
        Assert.Equal(IpSnapshot.Initial, _monitor.Current);
    }

    [Fact]
    public async Task Publishes_both_addresses()
    {
        _resolver.Answer(AddressFamily.InterNetwork, "203.0.113.42");
        _resolver.Answer(AddressFamily.InterNetworkV6, "2001:db8::42");

        _monitor.Start();
        await Wait.UntilAsync(() => !_monitor.Current.IsRefreshing, "the first lookup completes");

        IpSnapshot snapshot = _monitor.Current;
        Assert.Equal(IpLookupStatus.Ok, snapshot.Status);
        Assert.Equal("203.0.113.42", snapshot.IPv4);
        Assert.Equal("2001:db8::42", snapshot.IPv6);
        Assert.Equal(_time.GetLocalNow(), snapshot.UpdatedAt);
    }

    [Fact]
    public async Task Reports_offline_when_there_is_no_network()
    {
        _network.IsNetworkAvailable = false;

        _monitor.Start();
        await Wait.UntilAsync(() => !_monitor.Current.IsRefreshing, "the first lookup completes");

        Assert.Equal(IpLookupStatus.Offline, _monitor.Current.Status);
    }

    [Fact]
    public async Task Reports_a_failure_when_the_services_do_not_answer()
    {
        _monitor.Start();
        await Wait.UntilAsync(() => !_monitor.Current.IsRefreshing, "the first lookup completes");

        Assert.Equal(IpLookupStatus.Failed, _monitor.Current.Status);
    }

    [Fact]
    public async Task Survives_a_resolver_exception()
    {
        _resolver.Throw(AddressFamily.InterNetworkV6);
        _resolver.Answer(AddressFamily.InterNetwork, "203.0.113.42");

        _monitor.Start();
        await Wait.UntilAsync(() => !_monitor.Current.IsRefreshing, "the first lookup completes");

        Assert.Equal("203.0.113.42", _monitor.Current.IPv4);
        Assert.Null(_monitor.Current.IPv6);
    }

    [Fact]
    public async Task Never_keeps_an_address_after_a_failed_lookup()
    {
        _resolver.Answer(AddressFamily.InterNetwork, "203.0.113.42");
        _monitor.Start();
        await Wait.UntilAsync(() => _monitor.Current.IPv4 is not null, "the address is found");

        _resolver.Answer(AddressFamily.InterNetwork, null);
        _monitor.RequestRefresh();
        await Wait.UntilAsync(() => _monitor.Current.Status == IpLookupStatus.Failed, "the second lookup fails");

        Assert.Null(_monitor.Current.IPv4);
    }

    [Fact]
    public async Task Retries_with_backoff_after_a_failure()
    {
        _monitor.Start();
        await Wait.UntilAsync(() => _monitor.WaitCount == 1, "the first lookup fails and the retry is scheduled");

        _resolver.Answer(AddressFamily.InterNetwork, "203.0.113.42");
        _time.Advance(TimeSpan.FromSeconds(3));

        await Wait.UntilAsync(() => _monitor.Current.IPv4 == "203.0.113.42", "the retry succeeds");
        Assert.Equal(2, _resolver.IPv4Calls);
    }

    [Fact]
    public async Task Refreshes_periodically()
    {
        _resolver.Answer(AddressFamily.InterNetwork, "203.0.113.42");
        _monitor.Start();
        await Wait.UntilAsync(() => _monitor.WaitCount == 1, "the first lookup completes and the next one is scheduled");

        _time.Advance(TimeSpan.FromMinutes(14));
        await Task.Delay(50, TestContext.Current.CancellationToken);
        Assert.Equal(1, _resolver.IPv4Calls);

        _time.Advance(TimeSpan.FromMinutes(1));
        await Wait.UntilAsync(() => _resolver.IPv4Calls == 2, "the periodic refresh runs");
    }

    [Fact]
    public async Task Refreshes_once_after_a_burst_of_network_changes()
    {
        _resolver.Answer(AddressFamily.InterNetwork, "203.0.113.42");
        _monitor.Start();
        await Wait.UntilAsync(() => _monitor.WaitCount == 1, "the first lookup completes and the next one is scheduled");

        _resolver.Answer(AddressFamily.InterNetwork, "198.51.100.7");
        for (int i = 0; i < 5; i++)
        {
            _network.RaiseChanged();
            _time.Advance(TimeSpan.FromMilliseconds(500));
        }

        _time.Advance(TimeSpan.FromSeconds(2));

        await Wait.UntilAsync(() => _monitor.Current.IPv4 == "198.51.100.7", "the new address is published");
        await Task.Delay(50, TestContext.Current.CancellationToken);
        Assert.Equal(2, _resolver.IPv4Calls);
    }
}
