using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
using MyPublicIp.Core.Abstractions;
using MyPublicIp.Core.History;

namespace MyPublicIp.Platform;

/// <summary>
/// Windows notifications of the app. Clicks come back through
/// <see cref="AppNotificationManager.NotificationInvoked"/> (see App).
/// </summary>
internal sealed class AppNotificationUserNotifier : IUserNotifier
{
    public const string ActionArgument = "action";
    public const string ShowAction = "show";
    public const string CopyAction = "copy";
    public const string AddressArgument = "address";

    private const string CopiedTag = "copied";

    private readonly ILocalizer _localizer;

    public AppNotificationUserNotifier(ILocalizer localizer)
    {
        _localizer = localizer;
    }

    /// <summary>"Your public IP address changed", the old and new address, the VPN change, and a copy button.</summary>
    public void NotifyIpChanged(IpHistoryEntry previous, IpHistoryEntry current)
    {
        if (!AppNotificationManager.IsSupported() || current.MainAddress is not { } address)
        {
            return;
        }

        var builder = new AppNotificationBuilder()
            .AddArgument(ActionArgument, ShowAction)
            .AddText(_localizer.Get("NotificationTitle"))
            .AddText(_localizer.Format("NotificationBody", previous.MainAddress ?? "—", address));

        string? vpnChange = (previous.VpnName, current.VpnName) switch
        {
            (not null, null) => _localizer.Format("NotificationVpnOff", previous.VpnName),
            (null, not null) => _localizer.Format("NotificationVpnOn", current.VpnName),
            _ => null,
        };
        if (vpnChange is not null)
        {
            builder.AddText(vpnChange);
        }

        builder.AddButton(new AppNotificationButton(_localizer.Get("NotificationCopy"))
            .AddArgument(ActionArgument, CopyAction)
            .AddArgument(AddressArgument, address));

        AppNotificationManager.Default.Show(builder.BuildNotification());
    }

    /// <summary>Silent confirmation of a copy made with the keyboard shortcut; replaces the previous one.</summary>
    public void NotifyCopied(string address)
    {
        if (!AppNotificationManager.IsSupported())
        {
            return;
        }

        AppNotification notification = new AppNotificationBuilder()
            .AddArgument(ActionArgument, ShowAction)
            .AddText(_localizer.Format("NotificationCopied", address))
            .MuteAudio()
            .SetTag(CopiedTag)
            .BuildNotification();
        notification.Expiration = DateTimeOffset.Now.AddSeconds(30);

        AppNotificationManager.Default.Show(notification);
    }
}
