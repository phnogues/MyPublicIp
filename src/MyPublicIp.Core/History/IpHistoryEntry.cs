using System.Text.Json.Serialization;

namespace MyPublicIp.Core.History;

/// <summary>
/// Public addresses observed from <paramref name="SeenAt"/> until the next entry, with the VPN
/// that was connected at that time and the country of the address once it is known.
/// </summary>
public sealed record IpHistoryEntry(
    DateTimeOffset SeenAt,
    string? IPv4,
    string? IPv6,
    string? VpnName = null,
    string? CountryCode = null)
{
    /// <summary>The address to show in a single line: IPv4 when there is one.</summary>
    [JsonIgnore]
    public string? MainAddress => IPv4 ?? IPv6;
}
