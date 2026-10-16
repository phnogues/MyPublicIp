using Microsoft.Extensions.Time.Testing;
using MyPublicIp.Core.Abstractions;
using MyPublicIp.Core.History;
using MyPublicIp.Core.PublicIp;
using MyPublicIp.Core.ViewModels;

namespace MyPublicIp.Core.Tests;

public sealed class MainViewModelTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 16, 10, 42, 0, TimeSpan.Zero);

    private readonly FakeMonitor _monitor = new();
    private readonly FakeStartupManager _startup = new(StartupState.Disabled);
    private readonly FakeClipboard _clipboard = new();
    private readonly FakeSettings _settings = new();
    private readonly FakeTimeProvider _time = new(Now);
    private readonly FakeHistoryStore _historyStore = new();
    private readonly FakeNotifier _notifier = new();
    private readonly FakeLocalNetwork _localNetwork = new();

    [Fact]
    public void Shows_placeholders_while_looking_up()
    {
        MainViewModel viewModel = CreateViewModel();

        Assert.Equal("…", viewModel.IPv4Text);
        Assert.Equal("…", viewModel.IPv6Text);
        Assert.False(viewModel.CopyIPv4Command.CanExecute(null));
        Assert.True(viewModel.IsRefreshing);
        Assert.Equal("AppName\nStatusLookingUp", viewModel.TrayToolTip);
    }

    [Fact]
    public void Shows_the_addresses_in_the_flyout_and_the_tooltip()
    {
        MainViewModel viewModel = CreateViewModel();

        _monitor.Publish(new IpSnapshot(IpLookupStatus.Ok, "203.0.113.42", "2001:db8::42", Now, IsRefreshing: false));

        Assert.Equal("203.0.113.42", viewModel.IPv4Text);
        Assert.Equal("2001:db8::42", viewModel.IPv6Text);
        Assert.True(viewModel.CopyIPv4Command.CanExecute(null));
        Assert.False(viewModel.IsStatusOpen);
        Assert.Equal("AppName\nToolTipLine(IPv4|203.0.113.42)\nToolTipLine(IPv6|2001:db8::42)", viewModel.TrayToolTip);
    }

    [Fact]
    public void Says_when_there_is_no_IPv6()
    {
        MainViewModel viewModel = CreateViewModel();

        _monitor.Publish(new IpSnapshot(IpLookupStatus.Ok, "203.0.113.42", null, Now, IsRefreshing: false));

        Assert.Equal("NotAvailable", viewModel.IPv6Text);
        Assert.False(viewModel.CopyIPv6Command.CanExecute(null));
    }

    [Theory]
    [InlineData(IpLookupStatus.Offline, StatusKind.Offline, "StatusOffline")]
    [InlineData(IpLookupStatus.Failed, StatusKind.Failed, "StatusFailed")]
    public void Explains_why_there_is_no_address(IpLookupStatus status, StatusKind expectedKind, string expectedTitle)
    {
        MainViewModel viewModel = CreateViewModel();

        _monitor.Publish(new IpSnapshot(status, null, null, null, IsRefreshing: false));

        Assert.True(viewModel.IsStatusOpen);
        Assert.Equal(expectedKind, viewModel.Status);
        Assert.Equal(expectedTitle, viewModel.StatusTitle);
        Assert.Equal("—", viewModel.IPv4Text);
        Assert.Equal($"AppName\n{expectedTitle}", viewModel.TrayToolTip);
    }

    [Fact]
    public void Cuts_the_tooltip_to_the_Windows_limit()
    {
        MainViewModel viewModel = CreateViewModel(new LongLocalizer());

        _monitor.Publish(new IpSnapshot(IpLookupStatus.Ok, "203.0.113.42", "2001:db8:85a3:8d3:1319:8a2e:370:7348", Now, IsRefreshing: false));

        Assert.Equal(MainViewModel.MaxToolTipLength, viewModel.TrayToolTip.Length);
    }

    [Fact]
    public async Task Copies_the_address_and_shows_a_short_confirmation()
    {
        MainViewModel viewModel = CreateViewModel();
        _monitor.Publish(new IpSnapshot(IpLookupStatus.Ok, "203.0.113.42", null, Now, IsRefreshing: false));

        Task copy = viewModel.CopyIPv4Command.ExecuteAsync(null);

        Assert.Equal("203.0.113.42", _clipboard.Text);
        Assert.True(viewModel.IsIPv4Copied);

        _time.Advance(TimeSpan.FromSeconds(1.5));
        await copy;
        Assert.False(viewModel.IsIPv4Copied);
    }

    [Fact]
    public void Refresh_asks_the_monitor()
    {
        MainViewModel viewModel = CreateViewModel();

        viewModel.RefreshCommand.Execute(null);

        Assert.Equal(1, _monitor.RefreshRequests);
    }

    [Fact]
    public async Task First_run_turns_start_with_Windows_on()
    {
        MainViewModel viewModel = CreateViewModel();

        await viewModel.InitializeAsync();

        Assert.True(_settings.FirstRunDone);
        Assert.True(viewModel.IsStartupOn);
    }

    [Fact]
    public async Task First_run_respects_a_choice_made_in_Windows_Settings()
    {
        _startup.State = StartupState.DisabledByUser;
        MainViewModel viewModel = CreateViewModel();

        await viewModel.InitializeAsync();

        Assert.Empty(_startup.Requests);
        Assert.False(viewModel.IsStartupOn);
        Assert.Equal("StartupDisabledByUser", viewModel.StartupToolTip);
    }

    [Fact]
    public async Task Later_runs_do_not_touch_start_with_Windows()
    {
        _settings.FirstRunDone = true;
        MainViewModel viewModel = CreateViewModel();

        await viewModel.InitializeAsync();

        Assert.Empty(_startup.Requests);
        Assert.False(viewModel.IsStartupOn);
    }

    [Fact]
    public async Task Toggling_start_with_Windows_always_reports_the_real_state()
    {
        _settings.FirstRunDone = true;
        _startup.State = StartupState.DisabledByUser;
        MainViewModel viewModel = CreateViewModel();
        await viewModel.InitializeAsync();
        var changed = new List<string?>();
        viewModel.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        await viewModel.ToggleStartupCommand.ExecuteAsync(null);

        Assert.Equal([true], _startup.Requests);
        Assert.False(viewModel.IsStartupOn);
        Assert.Contains(nameof(MainViewModel.IsStartupOn), changed);
    }

    [Fact]
    public void Dismissing_the_tip_is_remembered()
    {
        MainViewModel viewModel = CreateViewModel();
        Assert.True(viewModel.IsPinTipOpen);

        viewModel.DismissPinTipCommand.Execute(null);

        Assert.False(viewModel.IsPinTipOpen);
        Assert.True(_settings.PinTipDismissed);
        Assert.False(CreateViewModel().IsPinTipOpen);
    }

    [Fact]
    public async Task Shows_and_copies_the_local_address()
    {
        MainViewModel viewModel = CreateViewModel();

        Assert.Equal("192.168.1.42", viewModel.LocalIPv4Text);
        Assert.Equal("LocalNetworkOn(Wi-Fi)", viewModel.LocalNetworkLabel);

        Task copy = viewModel.CopyLocalIPv4Command.ExecuteAsync(null);
        Assert.Equal("192.168.1.42", _clipboard.Text);
        _time.Advance(TimeSpan.FromSeconds(1.5));
        await copy;
    }

    [Fact]
    public void Has_no_local_address_when_offline()
    {
        _localNetwork.Address = null;

        MainViewModel viewModel = CreateViewModel();

        Assert.False(viewModel.CopyLocalIPv4Command.CanExecute(null));
        Assert.Equal("—", viewModel.LocalIPv4Text);
    }

    [Fact]
    public void Lists_recent_addresses_with_readable_times()
    {
        _time.SetLocalTimeZone(TimeZoneInfo.Utc);
        var older = new IpHistoryEntry(Now.AddDays(-5), "192.0.2.81", null);
        var yesterday = new IpHistoryEntry(Now.AddDays(-1), "198.51.100.7", null);
        _historyStore.Save([yesterday, older]);
        MainViewModel viewModel = CreateViewModel();

        _monitor.Publish(new IpSnapshot(IpLookupStatus.Ok, "203.0.113.42", null, Now, IsRefreshing: false));

        Assert.Equal(
            [
                new HistoryRow(Now.ToString("t"), "203.0.113.42", IsCurrent: true),
                new HistoryRow($"Yesterday({yesterday.SeenAt:t})", "198.51.100.7", IsCurrent: false),
                new HistoryRow(older.SeenAt.ToString("g"), "192.0.2.81", IsCurrent: false),
            ],
            viewModel.History);
        Assert.Equal("HistoryHeader(3)", viewModel.HistoryHeader);
    }

    [Fact]
    public void Notification_setting_can_be_turned_off()
    {
        MainViewModel viewModel = CreateViewModel();
        Assert.True(viewModel.IsNotifyOnChangeOn);

        viewModel.ToggleNotifyOnChangeCommand.Execute(null);

        Assert.False(viewModel.IsNotifyOnChangeOn);
        Assert.False(_settings.NotifyOnIpChange);
    }

    private MainViewModel CreateViewModel(ILocalizer? localizer = null) =>
        new(
            _monitor,
            new IpHistory(_historyStore, _notifier, _settings),
            _startup,
            _clipboard,
            _settings,
            localizer ?? new KeyLocalizer(),
            _localNetwork,
            _time);

    private sealed class LongLocalizer : ILocalizer
    {
        public string Get(string key) => new('x', 80);

        public string Format(string key, params object?[] args) => new('y', 60);
    }
}
