using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MyPublicIp.Core.Abstractions;

namespace MyPublicIp.Core.ViewModels;

/// <summary>"Settings" tab and the matching items of the tray menu.</summary>
public sealed partial class SettingsViewModel : ObservableObject, IDisposable
{
    private readonly IStartupManager _startup;
    private readonly IAppSettings _settings;
    private readonly IGlobalHotkey _hotkey;
    private readonly ILocalizer _localizer;

    public SettingsViewModel(IStartupManager startup, IAppSettings settings, IGlobalHotkey hotkey, ILocalizer localizer)
    {
        _startup = startup;
        _settings = settings;
        _hotkey = hotkey;
        _localizer = localizer;

        IsPinTipOpen = !settings.PinTipDismissed;
        IsNotifyOnChangeOn = settings.NotifyOnIpChange;
        HotkeyText = hotkey.DisplayText;
        _hotkey.Pressed += OnHotkeyPressed;
    }

    /// <summary>The copy shortcut was pressed in any app.</summary>
    public event EventHandler? HotkeyPressed;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsStartupAvailable), nameof(IsStartupOn), nameof(CanChangeStartup), nameof(StartupToolTip))]
    public partial StartupState StartupState { get; private set; }

    [ObservableProperty]
    public partial bool IsNotifyOnChangeOn { get; private set; }

    [ObservableProperty]
    public partial bool IsHotkeyOn { get; private set; }

    [ObservableProperty]
    public partial string HotkeyText { get; private set; }

    /// <summary>Set when the shortcut is on in the settings but another app already uses it.</summary>
    [ObservableProperty]
    public partial string? HotkeyProblem { get; private set; }

    [ObservableProperty]
    public partial bool IsPinTipOpen { get; private set; }

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

        ApplyHotkey(_settings.HotkeyEnabled);
        await RefreshStartupStateAsync();
    }

    public async Task RefreshStartupStateAsync() => StartupState = await _startup.GetStateAsync();

    public void Dispose()
    {
        _hotkey.Pressed -= OnHotkeyPressed;
        _hotkey.Unregister();
    }

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
    private void ToggleHotkey()
    {
        _settings.HotkeyEnabled = !_settings.HotkeyEnabled;
        ApplyHotkey(_settings.HotkeyEnabled);
        OnPropertyChanged(nameof(IsHotkeyOn));
    }

    [RelayCommand]
    private void DismissPinTip()
    {
        _settings.PinTipDismissed = true;
        IsPinTipOpen = false;
    }

    private void ApplyHotkey(bool enabled)
    {
        if (!enabled)
        {
            _hotkey.Unregister();
            IsHotkeyOn = false;
            HotkeyProblem = null;
            return;
        }

        bool registered = _hotkey.TryRegister();
        IsHotkeyOn = true;
        HotkeyProblem = registered ? null : _localizer.Get("HotkeyUnavailable");
    }

    private void OnHotkeyPressed(object? sender, EventArgs e) => HotkeyPressed?.Invoke(this, EventArgs.Empty);
}
