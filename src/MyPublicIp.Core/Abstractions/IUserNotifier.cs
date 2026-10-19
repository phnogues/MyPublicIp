using MyPublicIp.Core.History;

namespace MyPublicIp.Core.Abstractions;

/// <summary>Windows notifications shown by the app.</summary>
public interface IUserNotifier
{
    /// <summary>The public address changed (VPN turned on or off, new network, new address from the provider).</summary>
    void NotifyIpChanged(IpHistoryEntry previous, IpHistoryEntry current);

    /// <summary>Confirms a copy made with the keyboard shortcut, when nothing else shows it.</summary>
    void NotifyCopied(string address);
}
