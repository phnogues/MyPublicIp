using System.ComponentModel;
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
/// The icon in the notification area: tooltip with the IP, left click toggles the flyout,
/// right click opens a native menu. Everything it shows comes from <see cref="MainViewModel"/>.
/// </summary>
internal sealed class TrayIconController : IDisposable
{
    private readonly MainViewModel _viewModel;
    private readonly TaskbarIcon _icon;
    private readonly MenuFlyoutItem _copyIPv4Item;
    private readonly MenuFlyoutItem _copyIPv6Item;
    private readonly ToggleMenuFlyoutItem _startupItem;
    private readonly ToggleMenuFlyoutItem _notifyItem;

    public TrayIconController(MainViewModel viewModel, ILocalizer localizer, Action toggleFlyout, Action showFlyout, Action quit)
    {
        _viewModel = viewModel;

        _copyIPv4Item = CreateItem(localizer.Get("MenuCopyIPv4"), viewModel.CopyIPv4Command);
        _copyIPv6Item = CreateItem(localizer.Get("MenuCopyIPv6"), viewModel.CopyIPv6Command);
        _startupItem = new ToggleMenuFlyoutItem { Text = localizer.Get("MenuStartWithWindows"), Command = viewModel.ToggleStartupCommand };
        _notifyItem = new ToggleMenuFlyoutItem { Text = localizer.Get("MenuNotifyOnChange"), Command = viewModel.ToggleNotifyOnChangeCommand };

        var menu = new MenuFlyout();
        menu.Items.Add(CreateItem(localizer.Get("MenuShow"), new RelayCommand(showFlyout)));
        menu.Items.Add(new MenuFlyoutSeparator());
        menu.Items.Add(_copyIPv4Item);
        menu.Items.Add(_copyIPv6Item);
        menu.Items.Add(CreateItem(localizer.Get("MenuRefresh"), viewModel.RefreshCommand));
        menu.Items.Add(new MenuFlyoutSeparator());
        menu.Items.Add(_startupItem);
        menu.Items.Add(_notifyItem);
        menu.Items.Add(new MenuFlyoutSeparator());
        menu.Items.Add(CreateItem(localizer.Get("MenuQuit"), new RelayCommand(quit)));

        int iconSize = NativeMethods.GetSmallIconSize();
        _icon = new TaskbarIcon
        {
            Id = CreateIconId(),
            Icon = new System.Drawing.Icon(Path.Combine(AppContext.BaseDirectory, "Assets", "TrayIcon.ico"), iconSize, iconSize),
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
    }

    public void Dispose()
    {
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _icon.Dispose();
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

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e) => Render();

    private void Render()
    {
        _icon.ToolTipText = _viewModel.TrayToolTip;
        _copyIPv4Item.IsEnabled = _viewModel.HasIPv4;
        _copyIPv6Item.IsEnabled = _viewModel.HasIPv6;
        _startupItem.IsChecked = _viewModel.IsStartupOn;
        _startupItem.IsEnabled = _viewModel.CanChangeStartup;
        _startupItem.Visibility = _viewModel.IsStartupAvailable ? Visibility.Visible : Visibility.Collapsed;
        _notifyItem.IsChecked = _viewModel.IsNotifyOnChangeOn;
    }
}
