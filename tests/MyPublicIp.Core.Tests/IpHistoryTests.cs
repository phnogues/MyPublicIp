using MyPublicIp.Core.History;
using MyPublicIp.Core.PublicIp;

namespace MyPublicIp.Core.Tests;

public sealed class IpHistoryTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 16, 10, 42, 0, TimeSpan.Zero);

    private readonly FakeHistoryStore _store = new();
    private readonly FakeNotifier _notifier = new();
    private readonly FakeSettings _settings = new();

    [Fact]
    public void Records_the_first_address_without_notifying()
    {
        IpHistory history = CreateHistory();

        history.Observe(Ok("203.0.113.42", minutes: 0));

        Assert.Equal("203.0.113.42", Assert.Single(history.Entries).IPv4);
        Assert.Empty(_notifier.Notifications);
        Assert.Equal(1, _store.SaveCount);
    }

    [Fact]
    public void Notifies_when_the_address_changes_during_the_session()
    {
        IpHistory history = CreateHistory();
        history.Observe(Ok("203.0.113.42", minutes: 0));

        history.Observe(Ok("198.51.100.7", minutes: 5));

        Assert.Equal(["198.51.100.7", "203.0.113.42"], history.Entries.Select(e => e.IPv4));
        (IpHistoryEntry previous, IpHistoryEntry current) = Assert.Single(_notifier.Notifications);
        Assert.Equal("203.0.113.42", previous.IPv4);
        Assert.Equal("198.51.100.7", current.IPv4);
    }

    [Fact]
    public void Ignores_the_same_address()
    {
        IpHistory history = CreateHistory();
        history.Observe(Ok("203.0.113.42", minutes: 0));

        history.Observe(Ok("203.0.113.42", minutes: 15));

        Assert.Single(history.Entries);
        Assert.Equal(1, _store.SaveCount);
    }

    [Fact]
    public void Ignores_a_new_IPv6_address_in_the_same_network()
    {
        IpHistory history = CreateHistory();
        history.Observe(Ok("203.0.113.42", minutes: 0, ipv6: "2001:db8:85a3:1:a1b2:c3d4:e5f6:1"));

        // Privacy extensions: same /64 prefix, new interface identifier
        history.Observe(Ok("203.0.113.42", minutes: 60, ipv6: "2001:db8:85a3:1:9f8e:7d6c:5b4a:2"));

        Assert.Single(history.Entries);
        Assert.Empty(_notifier.Notifications);
    }

    [Fact]
    public void Records_a_new_IPv6_network()
    {
        IpHistory history = CreateHistory();
        history.Observe(Ok("203.0.113.42", minutes: 0, ipv6: "2001:db8:85a3:1::1"));

        history.Observe(Ok("203.0.113.42", minutes: 60, ipv6: "2001:db8:ffff:2::1"));

        Assert.Equal(2, history.Entries.Count);
        Assert.Single(_notifier.Notifications);
    }

    [Fact]
    public void Does_not_notify_at_the_start_of_a_session_even_if_the_address_changed_since_last_time()
    {
        _store.Save([new IpHistoryEntry(Now.AddDays(-1), "198.51.100.7", null)]);
        IpHistory history = CreateHistory();

        history.Observe(Ok("203.0.113.42", minutes: 0));

        Assert.Equal(["203.0.113.42", "198.51.100.7"], history.Entries.Select(e => e.IPv4));
        Assert.Empty(_notifier.Notifications);
    }

    [Fact]
    public void Respects_the_notification_setting()
    {
        _settings.NotifyOnIpChange = false;
        IpHistory history = CreateHistory();
        history.Observe(Ok("203.0.113.42", minutes: 0));

        history.Observe(Ok("198.51.100.7", minutes: 5));

        Assert.Equal(2, history.Entries.Count);
        Assert.Empty(_notifier.Notifications);
    }

    [Theory]
    [InlineData(IpLookupStatus.Pending)]
    [InlineData(IpLookupStatus.Offline)]
    [InlineData(IpLookupStatus.Failed)]
    public void Ignores_lookups_without_an_address(IpLookupStatus status)
    {
        IpHistory history = CreateHistory();

        history.Observe(new IpSnapshot(status, null, null, Now, IsRefreshing: false));

        Assert.Empty(history.Entries);
    }

    [Fact]
    public void Waits_for_the_lookup_to_be_complete()
    {
        IpHistory history = CreateHistory();

        // IPv4 is published while IPv6 is still being looked up
        history.Observe(new IpSnapshot(IpLookupStatus.Ok, "203.0.113.42", null, Now, IsRefreshing: true));

        Assert.Empty(history.Entries);
    }

    [Fact]
    public void Keeps_the_most_recent_entries_only()
    {
        IpHistory history = CreateHistory();

        for (int i = 0; i < IpHistory.MaxEntries + 5; i++)
        {
            history.Observe(Ok($"203.0.113.{i}", minutes: i));
        }

        Assert.Equal(IpHistory.MaxEntries, history.Entries.Count);
        Assert.Equal($"203.0.113.{IpHistory.MaxEntries + 4}", history.Entries[0].IPv4);
        Assert.Equal(IpHistory.MaxEntries, _store.Saved.Count);
    }

    private static IpSnapshot Ok(string ipv4, int minutes, string? ipv6 = null) =>
        new(IpLookupStatus.Ok, ipv4, ipv6, Now.AddMinutes(minutes), IsRefreshing: false);

    private IpHistory CreateHistory() => new(_store, _notifier, _settings);
}
