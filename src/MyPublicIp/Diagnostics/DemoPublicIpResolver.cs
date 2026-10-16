using System.Net;
using System.Net.Sockets;
using MyPublicIp.Core.PublicIp;

namespace MyPublicIp.Diagnostics;

/// <summary>
/// Answers with addresses reserved for documentation (RFC 5737 and RFC 3849), so that screenshots
/// never show a real public IP. Every manual refresh switches to the next IPv4 address, which
/// shows the "IP changed" notification.
/// </summary>
internal sealed class DemoPublicIpResolver : IPublicIpResolver
{
    private static readonly string[] IPv4Addresses = ["203.0.113.42", "198.51.100.7"];

    private int _lookups;

    public async Task<IPAddress?> ResolveAsync(AddressFamily family, CancellationToken cancellationToken)
    {
        await Task.Delay(TimeSpan.FromMilliseconds(400), cancellationToken);
        if (family == AddressFamily.InterNetworkV6)
        {
            return IPAddress.Parse("2001:db8:85a3::8a2e:370:7334");
        }

        int lookup = Interlocked.Increment(ref _lookups) - 1;
        return IPAddress.Parse(IPv4Addresses[lookup % IPv4Addresses.Length]);
    }
}
