using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using MyPublicIp.Core.Layout;
using MyPublicIp.Core.ViewModels;
using MyPublicIp.Platform;
using Windows.Graphics;
using WinRT.Interop;

namespace MyPublicIp.Views;

/// <summary>
/// Small borderless window docked above the clock, like the Windows 11 quick settings.
/// It hides itself as soon as it loses focus. The code-behind only handles the window itself:
/// content and actions are bound to <see cref="MainViewModel"/>.
/// </summary>
public sealed partial class FlyoutWindow : Window
{
    private const double ContentWidth = 380;
    private const double ScreenMargin = 12;
    private const double FallbackHeight = 420;

    /// <summary>A click on the tray icon first takes the focus away from the flyout, which hides it.</summary>
    private static readonly TimeSpan ReopenGuard = TimeSpan.FromMilliseconds(400);

    private readonly nint _hwnd;
    private NativeMethods.POINT _anchor;
    private long _hiddenAt;
    private bool _allowClose;

    public FlyoutWindow(MainViewModel viewModel, string title, string versionText)
    {
        ViewModel = viewModel;
        InitializeComponent();
        Title = title;
        VersionText.Text = versionText;
        _hwnd = WindowNative.GetWindowHandle(this);

        var presenter = OverlappedPresenter.Create();
        presenter.IsResizable = false;
        presenter.IsMaximizable = false;
        presenter.IsMinimizable = false;
        presenter.IsAlwaysOnTop = true;
        presenter.SetBorderAndTitleBar(hasBorder: true, hasTitleBar: false);
        AppWindow.SetPresenter(presenter);
        AppWindow.IsShownInSwitchers = false;
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "TrayIcon.ico"));
        AppWindow.Closing += OnClosing;

        Activated += OnActivated;
        Root.SizeChanged += OnContentSizeChanged;
    }

    public MainViewModel ViewModel { get; }

    public bool IsOpen => AppWindow.IsVisible;

    /// <summary>True right after the flyout hid itself because the user clicked elsewhere.</summary>
    public bool WasJustHidden => Environment.TickCount64 - _hiddenAt < ReopenGuard.TotalMilliseconds;

    public void ShowNearTray()
    {
        NativeMethods.GetCursorPos(out _anchor);
        Place();
        AppWindow.Show();
        Activate();
        NativeMethods.SetForegroundWindow(_hwnd);
        ViewModel.OnFlyoutVisibilityChanged(visible: true);
    }

    public void HideFlyout()
    {
        if (AppWindow.IsVisible)
        {
            AppWindow.Hide();
            _hiddenAt = Environment.TickCount64;
            ViewModel.OnFlyoutVisibilityChanged(visible: false);
        }
    }

    public void CloseForExit()
    {
        _allowClose = true;
        Close();
    }

    /// <summary>Sizes the window to its content and docks it next to the clock of the screen used last.</summary>
    private void Place()
    {
        DisplayArea display = DisplayArea.GetFromPoint(new PointInt32(_anchor.X, _anchor.Y), DisplayAreaFallback.Nearest);
        RectInt32 work = display.WorkArea;
        RectInt32 outer = display.OuterBounds;
        double scale = NativeMethods.GetScaleAt(new NativeMethods.POINT { X = work.X + 1, Y = work.Y + 1 });

        double contentHeight = Root.ActualHeight > 0 ? Root.ActualHeight : FallbackHeight;
        PixelRect bounds = FlyoutPlacement.Compute(
            workArea: new PixelRect(work.X, work.Y, work.Width, work.Height),
            screen: new PixelRect(outer.X, outer.Y, outer.Width, outer.Height),
            width: (int)Math.Ceiling(ContentWidth * scale),
            height: (int)Math.Ceiling(contentHeight * scale),
            margin: (int)Math.Round(ScreenMargin * scale));

        AppWindow.MoveAndResize(new RectInt32(bounds.X, bounds.Y, bounds.Width, bounds.Height));
    }

    private void OnContentSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (AppWindow.IsVisible)
        {
            Place();
        }
    }

    private void OnActivated(object sender, WindowActivatedEventArgs args)
    {
        if (args.WindowActivationState == WindowActivationState.Deactivated)
        {
            HideFlyout();
        }
    }

    private void OnClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        // Alt+F4 only hides the flyout: the app keeps running in the notification area
        if (!_allowClose)
        {
            args.Cancel = true;
            HideFlyout();
        }
    }

    /// <summary>Shows the panel of the selected tab; the window then follows the new content height.</summary>
    private void OnTabChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
    {
        string? tab = sender.SelectedItem?.Tag as string;
        OverviewPanel.Visibility = tab is null or "Overview" ? Visibility.Visible : Visibility.Collapsed;
        NetworkPanel.Visibility = tab == "Network" ? Visibility.Visible : Visibility.Collapsed;
        HistoryPanel.Visibility = tab == "History" ? Visibility.Visible : Visibility.Collapsed;
        SettingsPanel.Visibility = tab == "Settings" ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnEscapeInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        HideFlyout();
    }
}
