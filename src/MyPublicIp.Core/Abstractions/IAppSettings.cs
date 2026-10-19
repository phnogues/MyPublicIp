namespace MyPublicIp.Core.Abstractions;

/// <summary>Small per-user settings persisted between sessions.</summary>
public interface IAppSettings
{
    bool FirstRunDone { get; set; }

    bool PinTipDismissed { get; set; }

    /// <summary>Show a Windows notification when the public address changes. On by default.</summary>
    bool NotifyOnIpChange { get; set; }

    /// <summary>Copy the public IP with Win+Shift+I from any app. On by default.</summary>
    bool HotkeyEnabled { get; set; }
}
