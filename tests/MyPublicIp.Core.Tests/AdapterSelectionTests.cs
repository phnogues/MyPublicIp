using MyPublicIp.Core.LocalNetwork;

namespace MyPublicIp.Core.Tests;

public sealed class AdapterSelectionTests
{
    private static readonly AdapterInfo WiFi = new("Wi-Fi", "Intel(R) Wi-Fi 6 AX201", AdapterType.WiFi, IsUp: true, HasIPv4: true, HasGateway: true);
    private static readonly AdapterInfo Ethernet = new("Ethernet", "Realtek PCIe GbE", AdapterType.Ethernet, IsUp: true, HasIPv4: true, HasGateway: true);
    private static readonly AdapterInfo HyperV = new("vEthernet (WSL)", "Hyper-V Virtual Ethernet Adapter", AdapterType.Ethernet, IsUp: true, HasIPv4: true, HasGateway: false);
    private static readonly AdapterInfo Loopback = new("Loopback", "Software Loopback Interface 1", AdapterType.Loopback, IsUp: true, HasIPv4: true, HasGateway: false);
    private static readonly AdapterInfo Teredo = new("Teredo", "Teredo Tunneling Pseudo-Interface", AdapterType.Tunnel, IsUp: true, HasIPv4: true, HasGateway: false);
    private static readonly AdapterInfo WireGuard = new("NordLynx", "NordLynx Tunnel", AdapterType.Other, IsUp: true, HasIPv4: true, HasGateway: true);
    private static readonly AdapterInfo DisconnectedTap = new("CyberGhost VPN", "TAP-Windows Adapter V9", AdapterType.Ethernet, IsUp: false, HasIPv4: false, HasGateway: false);

    [Fact]
    public void Picks_the_physical_adapter_with_a_gateway()
    {
        Assert.Equal(WiFi, AdapterSelection.SelectPrimary([Loopback, HyperV, Teredo, WiFi]));
    }

    [Fact]
    public void Prefers_ethernet_to_wifi()
    {
        Assert.Equal(Ethernet, AdapterSelection.SelectPrimary([WiFi, Ethernet]));
    }

    [Fact]
    public void Ignores_the_vpn_adapter_even_with_a_gateway()
    {
        Assert.Equal(WiFi, AdapterSelection.SelectPrimary([WireGuard, WiFi]));
    }

    [Fact]
    public void Returns_null_when_offline()
    {
        Assert.Null(AdapterSelection.SelectPrimary([Loopback, HyperV with { HasGateway = false }]));
    }

    [Fact]
    public void Detects_a_connected_vpn()
    {
        Assert.Equal(new VpnStatus(true, "NordLynx"), AdapterSelection.DetectVpn([WiFi, WireGuard]));
    }

    [Theory]
    [InlineData("Ethernet 3", "TAP-Windows Adapter V9", AdapterType.Ethernet)]
    [InlineData("ProtonVPN", "WireGuard Tunnel", AdapterType.Other)]
    [InlineData("Company VPN", "WAN Miniport (IKEv2)", AdapterType.Ppp)]
    [InlineData("Ethernet 2", "Cisco AnyConnect Secure Mobility Client Virtual Miniport Adapter", AdapterType.Ethernet)]
    public void Recognizes_common_vpn_clients(string name, string description, AdapterType type)
    {
        var adapter = new AdapterInfo(name, description, type, IsUp: true, HasIPv4: true, HasGateway: false);

        Assert.True(AdapterSelection.DetectVpn([WiFi, adapter]).IsActive);
    }

    [Fact]
    public void A_disconnected_vpn_adapter_or_a_system_tunnel_is_not_a_vpn()
    {
        Assert.Equal(VpnStatus.Off, AdapterSelection.DetectVpn([WiFi, DisconnectedTap, Teredo, HyperV]));
    }
}
