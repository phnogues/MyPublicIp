using MyPublicIp.Core.Abstractions;
using MyPublicIp.Core.ViewModels;

namespace MyPublicIp.Core.Tests;

public sealed class SettingsViewModelTests : IDisposable
{
    private readonly ViewModelFixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public async Task First_run_turns_start_with_Windows_on()
    {
        SettingsViewModel settings = _fixture.CreateSettings();

        await settings.InitializeAsync();

        Assert.True(_fixture.Settings.FirstRunDone);
        Assert.True(settings.IsStartupOn);
    }

    [Fact]
    public async Task First_run_respects_a_choice_made_in_Windows_Settings()
    {
        _fixture.Startup.State = StartupState.DisabledByUser;
        SettingsViewModel settings = _fixture.CreateSettings();

        await settings.InitializeAsync();

        Assert.Empty(_fixture.Startup.Requests);
        Assert.False(settings.IsStartupOn);
        Assert.Equal("StartupDisabledByUser", settings.StartupToolTip);
    }

    [Fact]
    public async Task Later_runs_do_not_touch_start_with_Windows()
    {
        _fixture.Settings.FirstRunDone = true;
        SettingsViewModel settings = _fixture.CreateSettings();

        await settings.InitializeAsync();

        Assert.Empty(_fixture.Startup.Requests);
        Assert.False(settings.IsStartupOn);
    }

    [Fact]
    public async Task Toggling_start_with_Windows_always_reports_the_real_state()
    {
        _fixture.Settings.FirstRunDone = true;
        _fixture.Startup.State = StartupState.DisabledByUser;
        SettingsViewModel settings = _fixture.CreateSettings();
        await settings.InitializeAsync();
        var changed = new List<string?>();
        settings.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        await settings.ToggleStartupCommand.ExecuteAsync(null);

        Assert.Equal([true], _fixture.Startup.Requests);
        Assert.False(settings.IsStartupOn);
        Assert.Contains(nameof(SettingsViewModel.IsStartupOn), changed);
    }

    [Fact]
    public void Notification_setting_can_be_turned_off()
    {
        SettingsViewModel settings = _fixture.CreateSettings();
        Assert.True(settings.IsNotifyOnChangeOn);

        settings.ToggleNotifyOnChangeCommand.Execute(null);

        Assert.False(settings.IsNotifyOnChangeOn);
        Assert.False(_fixture.Settings.NotifyOnIpChange);
    }

    [Fact]
    public async Task Registers_the_keyboard_shortcut_at_startup()
    {
        SettingsViewModel settings = _fixture.CreateSettings();

        await settings.InitializeAsync();

        Assert.True(_fixture.Hotkey.IsRegistered);
        Assert.True(settings.IsHotkeyOn);
        Assert.Null(settings.HotkeyProblem);
        Assert.Equal("Win + Shift + I", settings.HotkeyText);
    }

    [Fact]
    public async Task Says_when_another_app_uses_the_shortcut()
    {
        _fixture.Hotkey.Available = false;
        SettingsViewModel settings = _fixture.CreateSettings();

        await settings.InitializeAsync();

        Assert.True(settings.IsHotkeyOn);
        Assert.Equal("HotkeyUnavailable", settings.HotkeyProblem);
    }

    [Fact]
    public async Task The_shortcut_can_be_turned_off()
    {
        SettingsViewModel settings = _fixture.CreateSettings();
        await settings.InitializeAsync();

        settings.ToggleHotkeyCommand.Execute(null);

        Assert.False(_fixture.Hotkey.IsRegistered);
        Assert.False(settings.IsHotkeyOn);
        Assert.False(_fixture.Settings.HotkeyEnabled);
    }

    [Fact]
    public void Dismissing_the_tip_is_remembered()
    {
        SettingsViewModel settings = _fixture.CreateSettings();
        Assert.True(settings.IsPinTipOpen);

        settings.DismissPinTipCommand.Execute(null);

        Assert.False(settings.IsPinTipOpen);
        Assert.True(_fixture.Settings.PinTipDismissed);
        Assert.False(_fixture.CreateSettings().IsPinTipOpen);
    }
}
