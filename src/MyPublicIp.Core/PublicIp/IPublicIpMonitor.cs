namespace MyPublicIp.Core.PublicIp;

public interface IPublicIpMonitor
{
    IpSnapshot Current { get; }

    /// <summary>Raised on a background thread whenever <see cref="Current"/> changes.</summary>
    event EventHandler? Changed;

    /// <summary>Asks for a lookup as soon as possible. Concurrent requests are coalesced.</summary>
    void RequestRefresh();
}
