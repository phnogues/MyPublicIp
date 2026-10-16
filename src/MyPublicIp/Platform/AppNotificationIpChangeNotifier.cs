using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
using MyPublicIp.Core.Abstractions;
using MyPublicIp.Core.History;

namespace MyPublicIp.Platform;

/// <summary>
/// Windows notification "Your public IP address changed", with a button to copy the new address.
/// Clicks come back through <see cref="AppNotificationManager.NotificationInvoked"/> (see App).
/// </summary>
internal sealed class AppNotificationIpChangeNotifier : IIpChangeNotifier
{
    public const string ActionArgument = "action";
    public const string ShowAction = "show";
    public const string CopyAction = "copy";
    public const string AddressArgument = "address";

    private readonly ILocalizer _localizer;

    public AppNotificationIpChangeNotifier(ILocalizer localizer)
    {
        _localizer = localizer;
    }

    public void NotifyChanged(IpHistoryEntry previous, IpHistoryEntry current)
    {
        if (!AppNotificationManager.IsSupported() || current.MainAddress is not { } address)
        {
            return;
        }

        AppNotification notification = new AppNotificationBuilder()
            .AddArgument(ActionArgument, ShowAction)
            .AddText(_localizer.Get("NotificationTitle"))
            .AddText(_localizer.Format("NotificationBody", previous.MainAddress ?? "—", address))
            .AddButton(new AppNotificationButton(_localizer.Get("NotificationCopy"))
                .AddArgument(ActionArgument, CopyAction)
                .AddArgument(AddressArgument, address))
            .BuildNotification();

        AppNotificationManager.Default.Show(notification);
    }
}
