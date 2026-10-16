using System.ComponentModel;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MyPublicIp.Core.Abstractions;
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
/// State and actions shared by the flyout and the tray icon. Platform services are injected,
/// so the whole behavior is unit tested without Windows.
/// </summary>
public sealed partial class MainViewModel : ObservableObject, IDisposable
{
    /// <summary>NOTIFYICONDATA.szTip holds 128 characters including the terminating null.</summary>
    internal const int MaxToolTipLength = 127;

    private static readonly TimeSpan CopiedFeedbackDuration = TimeSpan.FromSeconds(1.5);

    private readonly IPublicIpMonitor _monitor;
    private readonly IpHistory _history;
    private readonly IStartupManager _startup;
    private readonly IClipboard _clipboard;
    private readonly IAppSettings _settings;
    private readonly ILocalizer _localizer;
    private readonly ILocalNetworkInfo _localNetwork;
    private readonly TimeProvider _time;
    private readonly SynchronizationContext? _uiContext;
    private IpSnapshot _snapshot = IpSnapshot.Initial;
    private LocalAddress? _localAddress;

    public MainViewModel(
        IPublicIpMonitor monitor,
        IpHistory history,
        IStartupManager startup,
        IClipboard clipboard,
        IAppSettings settings,
        ILocalizer localizer,
        ILocalNetworkInfo localNetwork,
        TimeProvider time)
    {
        _monitor = monitor;
        _history = history;
        _startup = startup;
        _clipboard = clipboard;
        _settings = settings;
        _localizer = localizer;
        _localNetwork = localNetwork;
        _time = time;

        // The monitor raises its events on background threads: bring them back to the UI thread
        _uiContext = SynchronizationContext.Current;
        _monitor.Changed += OnMonitorChanged;
        _history.Changed += OnHistoryChanged;

        IsPinTipOpen = !settings.PinTipDismissed;
        IsNotifyOnChangeOn = settings.NotifyOnIpChange;
        UpdateHistory();
        Apply(monitor.Current);
    }

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
    public partial string LocalIPv4Text { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string LocalNetworkLabel { get; private set; } = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CopyLocalIPv4Command))]
    public partial bool HasLocalIPv4 { get; private set; }

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

    [ObservableProperty]
    public partial bool IsIPv4Copied { get; private set; }

    [ObservableProperty]
    public partial bool IsIPv6Copied { get; private set; }

    [ObservableProperty]
    public partial bool IsLocalIPv4Copied { get; private set; }

    [ObservableProperty]
    public partial IReadOnlyList<HistoryRow> History { get; private set; } = [];

    [ObservableProperty]
    public partial string HistoryHeader { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasHistory { get; private set; }

    [ObservableProperty]
    public partial bool IsNotifyOnChangeOn { get; private set; }

    [ObservableProperty]
    public partial bool IsPinTipOpen { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsStartupAvailable), nameof(IsStartupOn), nameof(CanChangeStartup), nameof(StartupToolTip))]
    public partial StartupState StartupState { get; private set; }

    public bool IsStatusOpen => Status != StatusKind.None;

    public bool IsStartupAvailable => StartupState != StartupState.Unsupported;

    public bool IsStartupOn => StartupState.IsOn();

    public bool CanChangeStartup => StartupState.CanChange();

    public string? StartupToolTip => StartupState switch
    {
        StartupState.DisabledByUser => _localizer.Get("StartupDisabledByUser"),
        StartupState.DisabledByPolicy or StartupState.EnabledByPolicy => _localizer.Get("StartupManagedByPolicy"),
        _ => null,
    };

    /// <summary>
    /// First launch turns "Start with Windows" on: the user has just installed and opened the app,
    /// and being there at every login is its purpose. A choice made in Windows Settings is respected.
    /// </summary>
    public async Task InitializeAsync()
    {
        if (!_settings.FirstRunDone)
        {
            _settings.FirstRunDone = true;
            if (await _startup.GetStateAsync() == StartupState.Disabled)
            {
                await _startup.SetEnabledAsync(true);
            }
        }

        await RefreshStartupStateAsync();
    }

    public async Task RefreshStartupStateAsync() => StartupState = await _startup.GetStateAsync();

    public void Dispose()
    {
        _monitor.Changed -= OnMonitorChanged;
        _history.Changed -= OnHistoryChanged;
    }

    [RelayCommand]
    private void Refresh() => _monitor.RequestRefresh();

    [RelayCommand(CanExecute = nameof(HasIPv4))]
    private Task CopyIPv4Async() => CopyAsync(_snapshot.IPv4, copied => IsIPv4Copied = copied);

    [RelayCommand(CanExecute = nameof(HasIPv6))]
    private Task CopyIPv6Async() => CopyAsync(_snapshot.IPv6, copied => IsIPv6Copied = copied);

    [RelayCommand(CanExecute = nameof(HasLocalIPv4))]
    private Task CopyLocalIPv4Async() => CopyAsync(_localAddress?.Address, copied => IsLocalIPv4Copied = copied);

    [RelayCommand]
    private async Task ToggleStartupAsync()
    {
        StartupState = await _startup.SetEnabledAsync(!IsStartupOn);

        // The request may be refused (turned off in Windows Settings, policy...): a check box
        // that was just clicked must come back to the real state even if nothing changed
        OnPropertyChanged(nameof(IsStartupOn));
    }

    [RelayCommand]
    private void ToggleNotifyOnChange()
    {
        _settings.NotifyOnIpChange = !_settings.NotifyOnIpChange;
        IsNotifyOnChangeOn = _settings.NotifyOnIpChange;
    }

    [RelayCommand]
    private void DismissPinTip()
    {
        _settings.PinTipDismissed = true;
        IsPinTipOpen = false;
    }

    partial void OnStatusChanged(StatusKind value) => OnPropertyChanged(new PropertyChangedEventArgs(nameof(IsStatusOpen)));

    /// <summary>Copies and shows a check mark on the button for a moment.</summary>
    private async Task CopyAsync(string? text, Action<bool> setCopied)
    {
        if (text is null || !_clipboard.TrySetText(text))
        {
            return;
        }

        setCopied(true);
        await Task.Delay(CopiedFeedbackDuration, _time);
        setCopied(false);
    }

    private void OnMonitorChanged(object? sender, EventArgs e)
    {
        if (_uiContext is null)
        {
            Apply(_monitor.Current);
        }
        else
        {
            _uiContext.Post(_ => Apply(_monitor.Current), null);
        }
    }

    private void OnHistoryChanged(object? sender, EventArgs e) => UpdateHistory();

    private void Apply(IpSnapshot snapshot)
    {
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

        // The local address changes with the same network events as the public one
        _localAddress = _localNetwork.GetPrimaryIPv4();
        HasLocalIPv4 = _localAddress is not null;
        LocalIPv4Text = _localAddress?.Address ?? "—";
        LocalNetworkLabel = _localAddress is null
            ? _localizer.Get("LocalNetwork")
            : _localizer.Format("LocalNetworkOn", _localAddress.InterfaceName);

        TrayToolTip = BuildToolTip(snapshot);
        _history.Observe(snapshot);
    }

    private void UpdateHistory()
    {
        IReadOnlyList<IpHistoryEntry> entries = _history.Entries;
        History = [.. entries
            .Where(e => e.MainAddress is not null)
            .Select((e, index) => new HistoryRow(FormatWhen(e.SeenAt), e.MainAddress!, IsCurrent: index == 0))];
        HasHistory = History.Count > 0;
        HistoryHeader = _localizer.Format("HistoryHeader", History.Count);
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

        if (snapshot.IPv6 is not null)
        {
            text.Append('\n').Append(_localizer.Format("ToolTipLine", "IPv6", snapshot.IPv6));
        }

        return text.Length > MaxToolTipLength ? text.ToString(0, MaxToolTipLength) : text.ToString();
    }
}
