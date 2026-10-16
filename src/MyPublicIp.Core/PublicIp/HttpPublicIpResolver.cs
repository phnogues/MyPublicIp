using System.Net;
using System.Net.Sockets;

namespace MyPublicIp.Core.PublicIp;

/// <summary>Asks public HTTP services, falling back to the next one when a service fails.</summary>
public sealed class HttpPublicIpResolver : IPublicIpResolver
{
    private readonly HttpClient _http;
    private readonly PublicIpEndpoints _endpoints;

    public HttpPublicIpResolver(HttpClient http, PublicIpEndpoints endpoints)
    {
        _http = http;
        _endpoints = endpoints;
    }

    /// <summary>HttpClient tuned for short lookups that must survive network switches.</summary>
    public static HttpClient CreateHttpClient()
    {
        var handler = new SocketsHttpHandler
        {
            ConnectTimeout = TimeSpan.FromSeconds(4),
            // Short lifetimes so that a network switch (VPN, Wi-Fi...) never reuses a stale connection
            PooledConnectionLifetime = TimeSpan.FromMinutes(1),
            PooledConnectionIdleTimeout = TimeSpan.FromSeconds(20),
        };

        var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(6) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("MyPublicIp/2.0 (+https://github.com/phnogues/MyPublicIp)");
        return http;
    }

    public async Task<IPAddress?> ResolveAsync(AddressFamily family, CancellationToken cancellationToken)
    {
        IReadOnlyList<Uri> endpoints = family switch
        {
            AddressFamily.InterNetwork => _endpoints.IPv4,
            AddressFamily.InterNetworkV6 => _endpoints.IPv6,
            _ => throw new ArgumentOutOfRangeException(nameof(family), family, "Only IPv4 and IPv6 are supported."),
        };

        foreach (Uri endpoint in endpoints)
        {
            try
            {
                string body = await _http.GetStringAsync(endpoint, cancellationToken).ConfigureAwait(false);
                if (TryParse(body, family, out IPAddress? address))
                {
                    return address;
                }
            }
            catch (HttpRequestException)
            {
                // Service down or unreachable: try the next one
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // Timeout: try the next one
            }
        }

        return null;
    }

    /// <summary>
    /// Strict parsing: <see cref="IPAddress.TryParse(string, out IPAddress)"/> alone accepts forms
    /// such as "1" or "10.1" that no service would return, which would hide an unexpected answer.
    /// </summary>
    internal static bool TryParse(string text, AddressFamily family, out IPAddress? address)
    {
        address = null;
        string candidate = text.Trim();

        if (family == AddressFamily.InterNetwork && candidate.Count(c => c == '.') != 3)
        {
            return false;
        }

        if (!IPAddress.TryParse(candidate, out IPAddress? parsed) || parsed.AddressFamily != family)
        {
            return false;
        }

        address = parsed;
        return true;
    }
}
