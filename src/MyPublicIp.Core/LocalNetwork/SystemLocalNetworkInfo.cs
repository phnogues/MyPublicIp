using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace MyPublicIp.Core.LocalNetwork;

/// <summary><see cref="ILocalNetworkInfo"/> based on <see cref="NetworkInterface"/> (no Windows-only API).</summary>
public class SystemLocalNetworkInfo : ILocalNetworkInfo
{
    public virtual NetworkDetails? GetPrimaryNetwork()
    {
        IReadOnlyList<(NetworkInterface Interface, AdapterInfo Info)> adapters = ReadAdapters();
        AdapterInfo? primary = AdapterSelection.SelectPrimary(adapters.Select(a => a.Info));
        if (primary is null)
        {
            return null;
        }

        NetworkInterface adapter = adapters.First(a => a.Info == primary).Interface;
        IPInterfaceProperties properties = adapter.GetIPProperties();
        string? mac = adapter.GetPhysicalAddress().GetAddressBytes() is { Length: > 0 } bytes
            ? string.Join(':', bytes.Select(b => b.ToString("X2")))
            : null;

        return new NetworkDetails(
            AdapterName: adapter.Name,
            AdapterDescription: adapter.Description,
            Kind: primary.Type switch
            {
                AdapterType.Ethernet => ConnectionKind.Ethernet,
                AdapterType.WiFi => ConnectionKind.WiFi,
                AdapterType.Cellular => ConnectionKind.Cellular,
                _ => ConnectionKind.Unknown,
            },
            IPv4: properties.UnicastAddresses.Select(a => a.Address).FirstOrDefault(IsUsableIPv4)?.ToString(),
            Gateway: properties.GatewayAddresses.Select(g => g.Address).FirstOrDefault(IsUsableIPv4)?.ToString(),
            // fec0:: site-local servers are placeholders Windows lists when no IPv6 DNS server is configured
            DnsServers: [.. properties.DnsAddresses.Where(a => !a.IsIPv6SiteLocal).Select(a => a.ToString()).Distinct()],
            MacAddress: mac,
            LinkSpeedBitsPerSecond: adapter.Speed > 0 ? adapter.Speed : null);
    }

    public VpnStatus GetVpnStatus() => AdapterSelection.DetectVpn(ReadAdapters().Select(a => a.Info));

    private static IReadOnlyList<(NetworkInterface Interface, AdapterInfo Info)> ReadAdapters()
    {
        try
        {
            return [.. NetworkInterface.GetAllNetworkInterfaces().Select(n => (n, ToAdapterInfo(n)))];
        }
        catch (NetworkInformationException)
        {
            return [];
        }
    }

    private static AdapterInfo ToAdapterInfo(NetworkInterface adapter)
    {
        IPInterfaceProperties properties = adapter.GetIPProperties();
        return new AdapterInfo(
            adapter.Name,
            adapter.Description,
            adapter.NetworkInterfaceType switch
            {
                NetworkInterfaceType.Ethernet or NetworkInterfaceType.GigabitEthernet or NetworkInterfaceType.FastEthernetT
                    or NetworkInterfaceType.FastEthernetFx or NetworkInterfaceType.Ethernet3Megabit => AdapterType.Ethernet,
                NetworkInterfaceType.Wireless80211 => AdapterType.WiFi,
                NetworkInterfaceType.Wwanpp or NetworkInterfaceType.Wwanpp2 => AdapterType.Cellular,
                NetworkInterfaceType.Loopback => AdapterType.Loopback,
                NetworkInterfaceType.Tunnel => AdapterType.Tunnel,
                NetworkInterfaceType.Ppp => AdapterType.Ppp,
                _ => AdapterType.Other,
            },
            IsUp: adapter.OperationalStatus == OperationalStatus.Up,
            HasIPv4: properties.UnicastAddresses.Any(a => IsUsableIPv4(a.Address)),
            HasGateway: properties.GatewayAddresses.Any(g => IsUsableIPv4(g.Address)));
    }

    /// <summary>IPv4, and not a self-assigned 169.254.x.x address (no DHCP answer).</summary>
    private static bool IsUsableIPv4(IPAddress address) =>
        address.AddressFamily == AddressFamily.InterNetwork
        && !address.Equals(IPAddress.Any)
        && address.GetAddressBytes() is not [169, 254, ..];
}
