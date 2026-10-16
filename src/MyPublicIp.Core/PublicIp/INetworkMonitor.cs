namespace MyPublicIp.Core.PublicIp;

public interface INetworkMonitor
{
    /// <summary>Raised (possibly in bursts, on any thread) when an address or connection changes.</summary>
    event EventHandler? NetworkChanged;

    bool IsNetworkAvailable { get; }
}
