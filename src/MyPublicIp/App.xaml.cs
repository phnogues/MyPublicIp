using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using Microsoft.Windows.AppNotifications;
using MyPublicIp.Core.History;
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

        bool demo = LaunchOptions.Current.Demo;
        var localizer = new ResourceLocalizer();
        var settings = new PackagedAppSettings();
        IPublicIpResolver resolver = demo
            ? new DemoPublicIpResolver()
            : new HttpPublicIpResolver(HttpPublicIpResolver.CreateHttpClient(), PublicIpEndpoints.Default);
        IIpHistoryStore historyStore = demo ? new DemoIpHistoryStore(TimeProvider.System) : new JsonIpHistoryStore();
        ILocalNetworkInfo localNetwork = demo ? new DemoLocalNetworkInfo() : new SystemLocalNetworkInfo();

        _network = new SystemNetworkMonitor();
        _monitor = new PublicIpMonitor(resolver, _network, TimeProvider.System);
        _viewModel = new MainViewModel(
            _monitor,
            new IpHistory(historyStore, new AppNotificationIpChangeNotifier(localizer), settings),
            new PackagedStartupManager(),
            _clipboard,
            settings,
            localizer,
            localNetwork,
            TimeProvider.System);

        _flyout = new FlyoutWindow(_viewModel, localizer.Get("AppName"));
        _menuTheme = new MenuTheme();
        _tray = new TrayIconController(_viewModel, localizer, ToggleFlyout, ShowFlyout, Quit);

        _monitor.Start();
        AppInstance.GetCurrent().Activated += OnRedirectedActivation;

        // Launched at login: stay discreet. Launched by the user or from a notification: show the flyout.
        bool launchedAtLogin = AppInstance.GetCurrent().GetActivatedEventArgs().Kind == ExtendedActivationKind.StartupTask;
        _ = InitializeAsync(showFlyout: !launchedAtLogin);
    }

    private async Task InitializeAsync(bool showFlyout)
    {
        await _viewModel!.InitializeAsync();
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

    private void ShowFlyout()
    {
        _flyout!.ShowNearTray();

        // The user may have changed it in Windows Settings meanwhile
        _ = _viewModel!.RefreshStartupStateAsync();
    }

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
        // Raised on a background thread when the user clicks an "IP changed" notification or its button
        bool copy = args.Arguments.TryGetValue(AppNotificationIpChangeNotifier.ActionArgument, out string? action)
            && action == AppNotificationIpChangeNotifier.CopyAction;
        args.Arguments.TryGetValue(AppNotificationIpChangeNotifier.AddressArgument, out string? address);

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
        _viewModel?.Dispose();
        _monitor?.Dispose();
        _network?.Dispose();
        _flyout?.CloseForExit();
        Exit();
    }
}
