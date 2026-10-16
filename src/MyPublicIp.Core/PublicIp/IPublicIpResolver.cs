using System.Net;
using System.Net.Sockets;

namespace MyPublicIp.Core.PublicIp;

/// <summary>Finds the public address of this machine for one address family.</summary>
public interface IPublicIpResolver
{
    /// <returns>The address, or null if it could not be determined (no IPv6, services down...).</returns>
    Task<IPAddress?> ResolveAsync(AddressFamily family, CancellationToken cancellationToken);
}
