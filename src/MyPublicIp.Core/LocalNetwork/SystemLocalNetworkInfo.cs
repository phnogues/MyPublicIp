using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace MyPublicIp.Core.LocalNetwork;

/// <summary>Picks the adapter that has a default gateway: the one traffic to the internet goes through.</summary>
public sealed class SystemLocalNetworkInfo : ILocalNetworkInfo
{
    public LocalAddress? GetPrimaryIPv4()
    {
        try
        {
            return NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up
                    && n.NetworkInterfaceType is not (NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel))
                .Select(n => (Interface: n, Properties: n.GetIPProperties()))
                .Where(n => n.Properties.GatewayAddresses.Any(g => IsUsable(g.Address)))
                .SelectMany(n => n.Properties.UnicastAddresses
                    .Where(a => IsUsable(a.Address))
                    .Select(a => new LocalAddress(a.Address.ToString(), n.Interface.Name)))
                .FirstOrDefault();
        }
        catch (NetworkInformationException)
        {
            return null;
        }
    }

    /// <summary>IPv4, and not a self-assigned 169.254.x.x address (no DHCP answer).</summary>
    private static bool IsUsable(IPAddress address) =>
        address.AddressFamily == AddressFamily.InterNetwork
        && !address.Equals(IPAddress.Any)
        && address.GetAddressBytes() is not [169, 254, ..];
}
