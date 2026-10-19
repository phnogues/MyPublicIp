using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace MyPublicIp.Core.Latency;

/// <summary>
/// Pings Cloudflare's public DNS (1.1.1.1). Some networks block ICMP: the time to open a TCP
/// connection on port 443 of the same server is then used instead.
/// </summary>
public sealed class PingLatencyProbe : ILatencyProbe
{
    private static readonly IPAddress Target = IPAddress.Parse("1.1.1.1");
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(2);

    public async Task<TimeSpan?> MeasureAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var ping = new Ping();
            PingReply reply = await ping.SendPingAsync(Target, Timeout, buffer: null, options: null, cancellationToken).ConfigureAwait(false);
            if (reply.Status == IPStatus.Success)
            {
                return TimeSpan.FromMilliseconds(reply.RoundtripTime);
            }
        }
        catch (PingException)
        {
        }

        return await MeasureTcpConnectAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<TimeSpan?> MeasureTcpConnectAsync(CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(Timeout);
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        long start = Stopwatch.GetTimestamp();
        try
        {
            await socket.ConnectAsync(Target, 443, timeout.Token).ConfigureAwait(false);
            return Stopwatch.GetElapsedTime(start);
        }
        catch (Exception ex) when (ex is SocketException || (ex is OperationCanceledException && !cancellationToken.IsCancellationRequested))
        {
            return null;
        }
    }
}
