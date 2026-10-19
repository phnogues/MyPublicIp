using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using H.NotifyIcon;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MyPublicIp.Core.Abstractions;
using MyPublicIp.Core.ViewModels;
using MyPublicIp.Platform;

namespace MyPublicIp.Tray;

/// <summary>
/// The icon in the notification area: tooltip with the IP, a green dot while a VPN is connected,
/// left click toggles the flyout, right click opens a native menu. Everything it shows comes from
/// <see cref="MainViewModel"/>.
/// </summary>
internal sealed class TrayIconController : IDisposable
{
    private readonly MainViewModel _viewModel;
    private readonly TaskbarIcon _icon;
    private readonly Icon _plainIcon;
    private readonly Icon _vpnIcon;
    private readonly MenuFlyoutItem _copyIPv4Item;
    private readonly MenuFlyoutItem _copyIPv6Item;
    private readonly ToggleMenuFlyoutItem _startupItem;
    private readonly ToggleMenuFlyoutItem _notifyItem;
    private readonly ToggleMenuFlyoutItem _hotkeyItem;
    private bool _showingVpn;

    public TrayIconController(MainViewModel viewModel, ILocalizer localizer, Action toggleFlyout, Action showFlyout, Action quit)
    {
        _viewModel = viewModel;
        SettingsViewModel settings = viewModel.Settings;

        _copyIPv4Item = CreateItem(localizer.Get("MenuCopyIPv4"), viewModel.CopyIPv4Command);
        _copyIPv6Item = CreateItem(localizer.Get("MenuCopyIPv6"), viewModel.CopyIPv6Command);
        _startupItem = new ToggleMenuFlyoutItem { Text = localizer.Get("MenuStartWithWindows"), Command = settings.ToggleStartupCommand };
        _notifyItem = new ToggleMenuFlyoutItem { Text = localizer.Get("MenuNotifyOnChange"), Command = settings.ToggleNotifyOnChangeCommand };
        _hotkeyItem = new ToggleMenuFlyoutItem
        {
            Text = localizer.Format("MenuHotkey", settings.HotkeyText),
            Command = settings.ToggleHotkeyCommand,
        };

        var menu = new MenuFlyout();
        menu.Items.Add(CreateItem(localizer.Get("MenuShow"), new RelayCommand(showFlyout)));
        menu.Items.Add(new MenuFlyoutSeparator());
        menu.Items.Add(_copyIPv4Item);
        menu.Items.Add(_copyIPv6Item);
        menu.Items.Add(CreateItem(localizer.Get("MenuRefresh"), viewModel.RefreshCommand));
        menu.Items.Add(new MenuFlyoutSeparator());
        menu.Items.Add(_startupItem);
        menu.Items.Add(_notifyItem);
        menu.Items.Add(_hotkeyItem);
        menu.Items.Add(new MenuFlyoutSeparator());
        menu.Items.Add(CreateItem(localizer.Get("MenuQuit"), new RelayCommand(quit)));

        int iconSize = NativeMethods.GetSmallIconSize();
        _plainIcon = new Icon(Path.Combine(AppContext.BaseDirectory, "Assets", "TrayIcon.ico"), iconSize, iconSize);
        _vpnIcon = WithVpnBadge(_plainIcon);

        _icon = new TaskbarIcon
        {
            Id = CreateIconId(),
            Icon = _plainIcon,
            NoLeftClickDelay = true,
            LeftClickCommand = new RelayCommand(toggleFlyout),
            ContextMenuMode = ContextMenuMode.PopupMenu,
            ContextFlyout = menu,

            // Windows keeps the tooltip shown at creation as the icon name in Settings > Personalization > Taskbar
            ToolTipText = localizer.Get("AppName"),
        };
        _icon.ForceCreate(enablesEfficiencyMode: false);

        Render();
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        _viewModel.Connection.PropertyChanged += OnViewModelPropertyChanged;
        settings.PropertyChanged += OnViewModelPropertyChanged;
    }

    public void Dispose()
    {
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _viewModel.Connection.PropertyChanged -= OnViewModelPropertyChanged;
        _viewModel.Settings.PropertyChanged -= OnViewModelPropertyChanged;
        _icon.Dispose();
        _plainIcon.Dispose();
        NativeMethods.DestroyIcon(_vpnIcon.Handle);
        _vpnIcon.Dispose();
    }

    private static MenuFlyoutItem CreateItem(string text, ICommand command) => new() { Text = text, Command = command };

    /// <summary>
    /// Windows ties a tray icon Guid to the path of the executable, and every MSIX update installs
    /// the app in a new versioned folder: derive the Guid from that path so that it is always accepted.
    /// </summary>
    private static Guid CreateIconId()
    {
        string path = (Environment.ProcessPath ?? "MyPublicIp").ToUpperInvariant();
        return new Guid(MD5.HashData(Encoding.UTF8.GetBytes(path)));
    }

    /// <summary>The same icon with a green dot in the bottom-right corner, ringed so it shows on any taskbar color.</summary>
    private static Icon WithVpnBadge(Icon icon)
    {
        using Bitmap bitmap = icon.ToBitmap();
        using (Graphics graphics = Graphics.FromImage(bitmap))
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            float size = bitmap.Width * 0.5f;
            float x = bitmap.Width - size - 0.5f;
            float y = bitmap.Height - size - 0.5f;
            using var ring = new SolidBrush(Color.White);
            using var dot = new SolidBrush(Color.FromArgb(22, 163, 74));
            graphics.FillEllipse(ring, x, y, size, size);
            float inset = Math.Max(1f, size * 0.18f);
            graphics.FillEllipse(dot, x + inset, y + inset, size - (2 * inset), size - (2 * inset));
        }

        // The handle is destroyed in Dispose: Icon.FromHandle does not own it
        return Icon.FromHandle(bitmap.GetHicon());
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e) => Render();

    private void Render()
    {
        SettingsViewModel settings = _viewModel.Settings;
        _icon.ToolTipText = _viewModel.TrayToolTip;
        _copyIPv4Item.IsEnabled = _viewModel.HasIPv4;
        _copyIPv6Item.IsEnabled = _viewModel.HasIPv6;
        _startupItem.IsChecked = settings.IsStartupOn;
        _startupItem.IsEnabled = settings.CanChangeStartup;
        _startupItem.Visibility = settings.IsStartupAvailable ? Visibility.Visible : Visibility.Collapsed;
        _notifyItem.IsChecked = settings.IsNotifyOnChangeOn;
        _hotkeyItem.IsChecked = settings.IsHotkeyOn;

        bool vpn = _viewModel.Connection.IsVpnActive;
        if (vpn != _showingVpn)
        {
            _showingVpn = vpn;
            _icon.UpdateIcon(vpn ? _vpnIcon : _plainIcon);
        }
    }
}
