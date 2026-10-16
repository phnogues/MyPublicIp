namespace MyPublicIp.Core.History;

/// <summary>Tells the user that the public address changed (a Windows notification in the app).</summary>
public interface IIpChangeNotifier
{
    void NotifyChanged(IpHistoryEntry previous, IpHistoryEntry current);
}
