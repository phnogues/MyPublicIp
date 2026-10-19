namespace MyPublicIp.Core.LocalNetwork;

/// <summary>
/// Rules to pick the physical adapter that reaches the internet and to tell whether a VPN is
/// connected. Windows has no API for third-party VPNs, so this relies on the adapters they install.
/// </summary>
public static class AdapterSelection
{
    /// <summary>Words found in the name or description of the virtual adapters of common VPN clients.</summary>
    private static readonly string[] VpnKeywords =
    [
        "vpn", "wireguard", "wintun", "tap-windows", "tap-win", "openvpn", "anyconnect", "fortinet", "forticlient",
        "globalprotect", "pangp", "nordlynx", "protonvpn", "cyberghost", "mullvad", "surfshark", "expressvpn",
        "zscaler", "tailscale", "zerotier", "softether", "sonicwall", "checkpoint", "pulse secure", "ivanti",
    ];

    /// <summary>Tunnels that Windows creates on its own and that do not mean a VPN is on.</summary>
    private static readonly string[] SystemTunnels = ["teredo", "isatap", "6to4", "ip-https", "loopback"];

    /// <summary>The adapter carrying internet traffic outside of any VPN: up, with an IPv4 address and a gateway.</summary>
    public static AdapterInfo? SelectPrimary(IEnumerable<AdapterInfo> adapters) =>
        adapters
            .Where(a => a.IsUp && a.HasIPv4 && a.HasGateway && a.Type is not (AdapterType.Loopback or AdapterType.Tunnel or AdapterType.Ppp))
            .Where(a => !IsVpnAdapter(a))
            .OrderBy(a => a.Type switch
            {
                AdapterType.Ethernet => 0,
                AdapterType.WiFi => 1,
                AdapterType.Cellular => 2,
                _ => 3,
            })
            .FirstOrDefault();

    public static VpnStatus DetectVpn(IEnumerable<AdapterInfo> adapters)
    {
        AdapterInfo? vpn = adapters.FirstOrDefault(a => a.IsUp && a.HasIPv4 && IsVpnAdapter(a));
        return vpn is null ? VpnStatus.Off : new VpnStatus(true, vpn.Name);
    }

    internal static bool IsVpnAdapter(AdapterInfo adapter)
    {
        string text = $"{adapter.Name} {adapter.Description}".ToLowerInvariant();
        if (SystemTunnels.Any(text.Contains))
        {
            return false;
        }

        return adapter.Type is AdapterType.Ppp or AdapterType.Tunnel || VpnKeywords.Any(text.Contains);
    }
}
