using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using Microsoft.Windows.AppNotifications;
using MyPublicIp.Core.Abstractions;
using MyPublicIp.Core.Geo;
using MyPublicIp.Core.History;
using MyPublicIp.Core.Latency;
using MyPublicIp.Core.LocalNetwork;
using MyPublicIp.Core.PublicIp;
using MyPublicIp.Core.ViewModels;
using MyPublicIp.Diagnostics;
using MyPublicIp.Platform;
using MyPublicIp.Tray;
using MyPublicIp.Views;

namespace MyPublicIp;

/// <summary>
/// Composition root. The app has no main window: it lives in the notification area,
/// with a flyout on left click and a menu on right click.
/// </summary>
public partial class App : Application
{
    private readonly WindowsClipboard _clipboard = new();
    private SystemNetworkMonitor? _network;
    private PublicIpMonitor? _monitor;
    private LatencyMonitor? _latency;
    private GlobalHotkey? _hotkey;
    private MainViewModel? _viewModel;
    private FlyoutWindow? _flyout;
    private TrayIconController? _tray;
    private MenuTheme? _menuTheme;
    private bool _exiting;

    public App()
    {
        LaunchOptions.Current.ApplyTo(this);
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        // Hiding the flyout must not end the app: it only quits from the tray menu
        DispatcherShutdownMode = DispatcherShutdownMode.OnExplicitShutdown;

        // Subscribe before registering, so that a click on a notification is never missed
        AppNotificationManager.Default.NotificationInvoked += OnNotificationInvoked;
        AppNotificationManager.Default.Register();

        ILocalizer localizer = new ResourceLocalizer();
        var settings = new PackagedAppSettings();
        var notifier = new AppNotificationUserNotifier(localizer);
        HttpClient http = HttpPublicIpResolver.CreateHttpClient();

        IPublicIpResolver resolver;
        IIpHistoryStore historyStore;
        ILocalNetworkInfo localNetwork;
        IGeoLocator geo;
        ILatencyProbe probe;
        if (LaunchOptions.Current.Demo)
        {
            var demoResolver = new DemoPublicIpResolver();
            (resolver, historyStore, localNetwork, geo, probe) = (demoResolver, new DemoIpHistoryStore(TimeProvider.System),
                new DemoLocalNetworkInfo(demoResolver), new DemoGeoLocator(), new DemoLatencyProbe());
        }
        else
        {
            (resolver, historyStore, localNetwork, geo, probe) = (new HttpPublicIpResolver(http, PublicIpEndpoints.Default),
                new JsonIpHistoryStore(), new WindowsLocalNetworkInfo(), new HttpGeoLocator(http), new PingLatencyProbe());
        }

        _network = new SystemNetworkMonitor();
        _monitor = new PublicIpMonitor(resolver, _network, TimeProvider.System);
        _latency = new LatencyMonitor(probe, TimeProvider.System);
        _hotkey = new GlobalHotkey(localizer.Get("HotkeyKeys"));
        _viewModel = new MainViewModel(
            _monitor,
            new IpHistory(historyStore, notifier, settings),
            localNetwork,
            _clipboard,
            notifier,
            localizer,
            TimeProvider.System,
            new ConnectionViewModel(geo, _latency, localizer),
            new NetworkViewModel(localNetwork, _clipboard, localizer, TimeProvider.System),
            new SettingsViewModel(new PackagedStartupManager(), settings, _hotkey, localizer));

        Windows.ApplicationModel.PackageVersion version = Windows.ApplicationModel.Package.Current.Id.Version;
        _flyout = new FlyoutWindow(_viewModel, localizer.Get("AppName"), localizer.Format("VersionText", $"{version.Major}.{version.Minor}.{version.Build}"));
        _menuTheme = new MenuTheme();
        _tray = new TrayIconController(_viewModel, localizer, ToggleFlyout, ShowFlyout, Quit);

        _monitor.Start();
        _latency.Start();
        AppInstance.GetCurrent().Activated += OnRedirectedActivation;

        // Launched at login: stay discreet. Launched by the user or from a notification: show the flyout.
        bool launchedAtLogin = AppInstance.GetCurrent().GetActivatedEventArgs().Kind == ExtendedActivationKind.StartupTask;
        _ = InitializeAsync(showFlyout: !launchedAtLogin);
    }

    private async Task InitializeAsync(bool showFlyout)
    {
        await _viewModel!.Settings.InitializeAsync();
        if (showFlyout)
        {
            ShowFlyout();
        }
    }

    private void ToggleFlyout()
    {
        if (_flyout!.IsOpen)
        {
            _flyout.HideFlyout();
        }
        else if (!_flyout.WasJustHidden)
        {
            ShowFlyout();
        }
    }

    private void ShowFlyout() => _flyout!.ShowNearTray();

    private void OnRedirectedActivation(object? sender, AppActivationArguments args)
    {
        // Raised on a background thread when the app is launched again while already running
        if (args.Kind != ExtendedActivationKind.StartupTask)
        {
            _flyout?.DispatcherQueue.TryEnqueue(ShowFlyout);
        }
    }

    private void OnNotificationInvoked(AppNotificationManager sender, AppNotificationActivatedEventArgs args)
    {
        // Raised on a background thread when the user clicks a notification or its button
        bool copy = args.Arguments.TryGetValue(AppNotificationUserNotifier.ActionArgument, out string? action)
            && action == AppNotificationUserNotifier.CopyAction;
        args.Arguments.TryGetValue(AppNotificationUserNotifier.AddressArgument, out string? address);

        _flyout?.DispatcherQueue.TryEnqueue(() =>
        {
            if (copy && address is not null)
            {
                _clipboard.TrySetText(address);
            }
            else
            {
                ShowFlyout();
            }
        });
    }

    private void Quit()
    {
        if (_exiting)
        {
            return;
        }

        _exiting = true;
        AppInstance.GetCurrent().Activated -= OnRedirectedActivation;
        AppNotificationManager.Default.NotificationInvoked -= OnNotificationInvoked;
        AppNotificationManager.Default.Unregister();
        _tray?.Dispose();
        _menuTheme?.Dispose();
        _viewModel?.Settings.Dispose();
        _viewModel?.Connection.Dispose();
        _viewModel?.Dispose();
        _hotkey?.Dispose();
        _latency?.Dispose();
        _monitor?.Dispose();
        _network?.Dispose();
        _flyout?.CloseForExit();
        Exit();
    }
}
