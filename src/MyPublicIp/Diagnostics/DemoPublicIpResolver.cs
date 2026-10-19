using System.Net;
using System.Net.Sockets;
using MyPublicIp.Core.PublicIp;

namespace MyPublicIp.Diagnostics;

/// <summary>
/// Answers with addresses reserved for documentation (RFC 5737 and RFC 3849), so that screenshots
/// never show a real public IP. Every manual refresh switches between the home address and the
/// VPN address, which shows the "IP changed" notification.
/// </summary>
internal sealed class DemoPublicIpResolver : IPublicIpResolver
{
    public const string HomeAddress = "203.0.113.42";
    public const string VpnAddress = "198.51.100.7";

    private int _lookups;

    /// <summary>The last IPv4 answer was the VPN address (every second lookup).</summary>
    public bool UsingVpn
    {
        get
        {
            int lookups = Volatile.Read(ref _lookups);
            return lookups > 0 && lookups % 2 == 0;
        }
    }

    public async Task<IPAddress?> ResolveAsync(AddressFamily family, CancellationToken cancellationToken)
    {
        await Task.Delay(TimeSpan.FromMilliseconds(400), cancellationToken);
        if (family == AddressFamily.InterNetworkV6)
        {
            return IPAddress.Parse("2001:db8:85a3::8a2e:370:7334");
        }

        int lookup = Interlocked.Increment(ref _lookups);
        return IPAddress.Parse(lookup % 2 == 1 ? HomeAddress : VpnAddress);
    }
}
