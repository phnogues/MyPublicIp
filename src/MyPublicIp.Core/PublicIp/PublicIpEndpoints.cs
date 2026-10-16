namespace MyPublicIp.Core.PublicIp;

/// <summary>
/// Plain-text "what is my IP" services, tried in order. Each list only contains hosts reachable
/// over a single address family, so the answer is the public address for that family.
/// </summary>
public sealed record PublicIpEndpoints(IReadOnlyList<Uri> IPv4, IReadOnlyList<Uri> IPv6)
{
    public static PublicIpEndpoints Default { get; } = new(
        IPv4:
        [
            new Uri("https://api.ipify.org"),
            new Uri("https://ipv4.icanhazip.com"),
            new Uri("https://checkip.amazonaws.com"),
        ],
        IPv6:
        [
            new Uri("https://api6.ipify.org"),
            new Uri("https://ipv6.icanhazip.com"),
        ]);
}
