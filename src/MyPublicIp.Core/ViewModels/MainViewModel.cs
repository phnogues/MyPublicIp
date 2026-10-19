using System.ComponentModel;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MyPublicIp.Core.Abstractions;
using MyPublicIp.Core.Geo;
using MyPublicIp.Core.History;
using MyPublicIp.Core.LocalNetwork;
using MyPublicIp.Core.PublicIp;

namespace MyPublicIp.Core.ViewModels;

public enum StatusKind
{
    None,
    Offline,
    Failed,
}

/// <summary>
/// State and actions shared by the flyout and the tray icon: the public addresses, their history,
/// and the child view models of the flyout tabs. Platform services are injected, so the whole
/// behavior is unit tested without Windows.
/// </summary>
public sealed partial class MainViewModel : ObservableObject, IDisposable
{
    /// <summary>NOTIFYICONDATA.szTip holds 128 characters including the terminating null.</summary>
    internal const int MaxToolTipLength = 127;

    private static readonly TimeSpan CopiedFeedbackDuration = TimeSpan.FromSeconds(1.5);

    private readonly IPublicIpMonitor _monitor;
    private readonly IpHistory _history;
    private readonly ILocalNetworkInfo _localNetwork;
    private readonly IClipboard _clipboard;
    private readonly IUserNotifier _notifier;
    private readonly ILocalizer _localizer;
    private readonly TimeProvider _time;
    private readonly UiDispatcher _dispatcher = new();
    private IpSnapshot _snapshot = IpSnapshot.Initial;

    public MainViewModel(
        IPublicIpMonitor monitor,
        IpHistory history,
        ILocalNetworkInfo localNetwork,
        IClipboard clipboard,
        IUserNotifier notifier,
        ILocalizer localizer,
        TimeProvider time,
        ConnectionViewModel connection,
        NetworkViewModel network,
        SettingsViewModel settings)
    {
        _monitor = monitor;
        _history = history;
        _localNetwork = localNetwork;
        _clipboard = clipboard;
        _notifier = notifier;
        _localizer = localizer;
        _time = time;
        Connection = connection;
        Network = network;
        Settings = settings;

        _monitor.Changed += OnMonitorChanged;
        _history.Changed += OnHistoryChanged;
        Connection.PropertyChanged += OnConnectionChanged;
        Settings.HotkeyPressed += OnHotkeyPressed;

        UpdateHistory();
        Apply(monitor.Current);
    }

    public ConnectionViewModel Connection { get; }

    public NetworkViewModel Network { get; }

    public SettingsViewModel Settings { get; }

    [ObservableProperty]
    public partial string IPv4Text { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string IPv6Text { get; private set; } = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CopyIPv4Command))]
    public partial bool HasIPv4 { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CopyIPv6Command))]
    public partial bool HasIPv6 { get; private set; }

    [ObservableProperty]
    public partial string UpdatedText { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsRefreshing { get; private set; }

    [ObservableProperty]
    public partial StatusKind Status { get; private set; }

    [ObservableProperty]
    public partial string StatusTitle { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string StatusMessage { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string TrayToolTip { get; private set; } = string.Empty;

    /// <summary>Key of the address just copied ("ipv4", "ipv6"), for the check mark on its button.</summary>
    [ObservableProperty]
    public partial string? CopiedItem { get; private set; }

    [ObservableProperty]
    public partial IReadOnlyList<HistoryRow> History { get; private set; } = [];

    [ObservableProperty]
    public partial bool HasHistory { get; private set; }

    public bool IsStatusOpen => Status != StatusKind.None;

    public void OnFlyoutVisibilityChanged(bool visible)
    {
        if (visible)
        {
            // The adapters or the VPN may have changed without a new public address
            RefreshLocalNetwork();
            _ = Settings.RefreshStartupStateAsync();
        }

        Connection.SetLive(visible);
    }

    public void Dispose()
    {
        _monitor.Changed -= OnMonitorChanged;
        _history.Changed -= OnHistoryChanged;
        Connection.PropertyChanged -= OnConnectionChanged;
        Settings.HotkeyPressed -= OnHotkeyPressed;
    }

    [RelayCommand]
    private void Refresh() => _monitor.RequestRefresh();

    [RelayCommand(CanExecute = nameof(HasIPv4))]
    private Task CopyIPv4Async() => CopyAsync(_snapshot.IPv4, "ipv4");

    [RelayCommand(CanExecute = nameof(HasIPv6))]
    private Task CopyIPv6Async() => CopyAsync(_snapshot.IPv6, "ipv6");

    partial void OnStatusChanged(StatusKind value) => OnPropertyChanged(new PropertyChangedEventArgs(nameof(IsStatusOpen)));

    /// <summary>Copies and shows a check mark on the button for a moment.</summary>
    private async Task CopyAsync(string? text, string item)
    {
        if (text is null || !_clipboard.TrySetText(text))
        {
            return;
        }

        CopiedItem = item;
        await Task.Delay(CopiedFeedbackDuration, _time);
        if (CopiedItem == item)
        {
            CopiedItem = null;
        }
    }

    private void OnHotkeyPressed(object? sender, EventArgs e)
    {
        string? address = _snapshot.IPv4 ?? _snapshot.IPv6;
        if (address is not null && _clipboard.TrySetText(address))
        {
            _notifier.NotifyCopied(address);
        }
    }

    private void OnMonitorChanged(object? sender, EventArgs e) => _dispatcher.Post(() => Apply(_monitor.Current));

    private void OnHistoryChanged(object? sender, EventArgs e) => UpdateHistory();

    private void OnConnectionChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ConnectionViewModel.LocationText) or nameof(ConnectionViewModel.VpnText))
        {
            TrayToolTip = BuildToolTip(_snapshot);
        }
    }

    private void Apply(IpSnapshot snapshot)
    {
        bool addressChanged = snapshot.IPv4 != _snapshot.IPv4 || snapshot.IPv6 != _snapshot.IPv6;
        _snapshot = snapshot;
        bool lookingUp = snapshot.Status == IpLookupStatus.Pending;

        IPv4Text = snapshot.IPv4 ?? (lookingUp ? "…" : "—");
        IPv6Text = snapshot.IPv6 ?? (lookingUp ? "…" : _localizer.Get("NotAvailable"));
        HasIPv4 = snapshot.IPv4 is not null;
        HasIPv6 = snapshot.IPv6 is not null;
        IsRefreshing = snapshot.IsRefreshing;
        UpdatedText = snapshot.UpdatedAt is { } updatedAt
            ? _localizer.Format("UpdatedAt", ToLocal(updatedAt).ToString("t"))
            : _localizer.Get("StatusLookingUp");

        (Status, StatusTitle, StatusMessage) = snapshot.Status switch
        {
            IpLookupStatus.Offline => (StatusKind.Offline, _localizer.Get("StatusOffline"), _localizer.Get("StatusOfflineDetail")),
            IpLookupStatus.Failed => (StatusKind.Failed, _localizer.Get("StatusFailed"), _localizer.Get("StatusFailedDetail")),
            _ => (StatusKind.None, string.Empty, string.Empty),
        };

        // The adapters and the VPN change with the same network events as the public address
        RefreshLocalNetwork();
        _history.Observe(snapshot, Connection.Vpn);
        TrayToolTip = BuildToolTip(snapshot);

        if (addressChanged && !snapshot.IsRefreshing)
        {
            _ = LocateAsync(snapshot.IPv4 ?? snapshot.IPv6);
        }
    }

    private void RefreshLocalNetwork()
    {
        Connection.UpdateVpn(_localNetwork.GetVpnStatus());
        Network.Refresh();
    }

    private async Task LocateAsync(string? address)
    {
        GeoInfo? info = await Connection.LocateAsync(address);
        if (address is not null && info is not null)
        {
            _history.SetCountry(address, info.CountryCode);
        }
    }

    private void UpdateHistory()
    {
        History = [.. _history.Entries
            .Where(e => e.MainAddress is not null)
            .Select((e, index) => new HistoryRow(
                FormatWhen(e.SeenAt),
                e.MainAddress!,
                e.CountryCode ?? string.Empty,
                e.VpnName is not null,
                IsCurrent: index == 0))];
        HasHistory = History.Count > 0;
    }

    /// <summary>"14:32", "Yesterday 14:32" or the short date and time.</summary>
    private string FormatWhen(DateTimeOffset seenAt)
    {
        DateTimeOffset local = ToLocal(seenAt);
        DateTime today = ToLocal(_time.GetUtcNow()).Date;

        if (local.Date == today)
        {
            return local.ToString("t");
        }

        return local.Date == today.AddDays(-1)
            ? _localizer.Format("Yesterday", local.ToString("t"))
            : local.ToString("g");
    }

    private DateTimeOffset ToLocal(DateTimeOffset value) => TimeZoneInfo.ConvertTime(value, _time.LocalTimeZone);

    private string BuildToolTip(IpSnapshot snapshot)
    {
        var text = new StringBuilder(_localizer.Get("AppName"));
        if (!snapshot.HasAddress)
        {
            text.Append('\n').Append(snapshot.Status switch
            {
                IpLookupStatus.Offline => _localizer.Get("StatusOffline"),
                IpLookupStatus.Failed => _localizer.Get("StatusFailed"),
                _ => _localizer.Get("StatusLookingUp"),
            });
        }

        if (snapshot.IPv4 is not null)
        {
            text.Append('\n').Append(_localizer.Format("ToolTipLine", "IPv4", snapshot.IPv4));
        }

        if (Connection.Location is { } location)
        {
            text.Append('\n').Append(location.Provider is null ? location.CountryName : $"{location.CountryName} · {location.Provider}");
        }

        if (Connection.Vpn is { IsActive: true } vpn)
        {
            text.Append('\n').Append(_localizer.Format("ToolTipVpn", vpn.Name ?? "VPN"));
        }

        // Last: the longest line, the one cut when the tooltip is too long
        if (snapshot.IPv6 is not null)
        {
            text.Append('\n').Append(_localizer.Format("ToolTipLine", "IPv6", snapshot.IPv6));
        }

        return text.Length > MaxToolTipLength ? text.ToString(0, MaxToolTipLength) : text.ToString();
    }
}
