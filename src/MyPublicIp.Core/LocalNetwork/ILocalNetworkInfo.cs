namespace MyPublicIp.Core.LocalNetwork;

/// <summary>Address of this machine on the local network, and the adapter that carries it.</summary>
public sealed record LocalAddress(string Address, string InterfaceName);

public interface ILocalNetworkInfo
{
    /// <returns>The IPv4 address of the adapter used to reach the internet, or null when offline.</returns>
    LocalAddress? GetPrimaryIPv4();
}
