namespace MyPublicIp.Core.LocalNetwork;

public enum ConnectionKind
{
    Unknown,
    Ethernet,
    WiFi,
    Cellular,
}

/// <summary>The network adapter used to reach the internet, and its settings.</summary>
public sealed record NetworkDetails(
    string AdapterName,
    string AdapterDescription,
    ConnectionKind Kind,
    string? IPv4,
    string? Gateway,
    IReadOnlyList<string> DnsServers,
    string? MacAddress,
    long? LinkSpeedBitsPerSecond)
{
    /// <summary>Wi-Fi network name or Windows network profile name, when the platform provides it.</summary>
    public string? NetworkName { get; init; }

    /// <summary>Wi-Fi or cellular signal strength from 0 to 5, when the platform provides it.</summary>
    public int? SignalBars { get; init; }
}

/// <summary>Whether traffic currently goes through a VPN, and which one.</summary>
public sealed record VpnStatus(bool IsActive, string? Name)
{
    public static VpnStatus Off { get; } = new(false, null);
}

/// <summary>What the selection rules need to know about a network adapter (see <see cref="AdapterSelection"/>).</summary>
public sealed record AdapterInfo(
    string Name,
    string Description,
    AdapterType Type,
    bool IsUp,
    bool HasIPv4,
    bool HasGateway);

public enum AdapterType
{
    Other,
    Ethernet,
    WiFi,
    Cellular,
    Loopback,
    Tunnel,
    Ppp,
}
