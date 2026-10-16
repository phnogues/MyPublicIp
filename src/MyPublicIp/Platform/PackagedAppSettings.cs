using MyPublicIp.Core.Abstractions;
using Windows.Storage;

namespace MyPublicIp.Platform;

/// <summary>Settings stored in the package local settings (removed when the app is uninstalled).</summary>
internal sealed class PackagedAppSettings : IAppSettings
{
    public bool FirstRunDone
    {
        get => Get(nameof(FirstRunDone), defaultValue: false);
        set => Set(nameof(FirstRunDone), value);
    }

    public bool PinTipDismissed
    {
        get => Get(nameof(PinTipDismissed), defaultValue: false);
        set => Set(nameof(PinTipDismissed), value);
    }

    public bool NotifyOnIpChange
    {
        get => Get(nameof(NotifyOnIpChange), defaultValue: true);
        set => Set(nameof(NotifyOnIpChange), value);
    }

    private static bool Get(string key, bool defaultValue)
    {
        try
        {
            return ApplicationData.Current.LocalSettings.Values[key] is bool value ? value : defaultValue;
        }
        catch (InvalidOperationException)
        {
            // No package identity (unpackaged run)
            return defaultValue;
        }
    }

    private static void Set(string key, bool value)
    {
        try
        {
            ApplicationData.Current.LocalSettings.Values[key] = value;
        }
        catch (InvalidOperationException)
        {
        }
    }
}
