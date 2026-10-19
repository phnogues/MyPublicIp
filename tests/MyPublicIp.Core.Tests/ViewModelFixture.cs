using Microsoft.Extensions.Time.Testing;
using MyPublicIp.Core.Abstractions;
using MyPublicIp.Core.History;
using MyPublicIp.Core.Latency;
using MyPublicIp.Core.ViewModels;

namespace MyPublicIp.Core.Tests;

/// <summary>Builds the view models the way the app does, with fakes for every service.</summary>
internal sealed class ViewModelFixture : IDisposable
{
    public static readonly DateTimeOffset Now = new(2026, 10, 16, 10, 42, 0, TimeSpan.Zero);

    public ViewModelFixture()
    {
        Time.SetLocalTimeZone(TimeZoneInfo.Utc);
        Latency = new LatencyMonitor(Probe, Time);
    }

    public FakeMonitor Monitor { get; } = new();

    public FakeStartupManager Startup { get; } = new(StartupState.Disabled);

    public FakeClipboard Clipboard { get; } = new();

    public FakeSettings Settings { get; } = new();

    public FakeTimeProvider Time { get; } = new(Now);

    public FakeHistoryStore HistoryStore { get; } = new();

    public FakeNotifier Notifier { get; } = new();

    public FakeLocalNetwork LocalNetwork { get; } = new();

    public FakeGeoLocator Geo { get; } = new();

    public FakeLatencyProbe Probe { get; } = new();

    public FakeHotkey Hotkey { get; } = new();

    public LatencyMonitor Latency { get; }

    public ILocalizer Localizer { get; set; } = new KeyLocalizer();

    public SettingsViewModel CreateSettings() => new(Startup, Settings, Hotkey, Localizer);

    public NetworkViewModel CreateNetwork() => new(LocalNetwork, Clipboard, Localizer, Time);

    public ConnectionViewModel CreateConnection() => new(Geo, Latency, Localizer);

    public MainViewModel CreateMain() => new(
        Monitor,
        new IpHistory(HistoryStore, Notifier, Settings),
        LocalNetwork,
        Clipboard,
        Notifier,
        Localizer,
        Time,
        CreateConnection(),
        CreateNetwork(),
        CreateSettings());

    public void Dispose() => Latency.Dispose();
}
