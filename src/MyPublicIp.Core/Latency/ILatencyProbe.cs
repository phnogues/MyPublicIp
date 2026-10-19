namespace MyPublicIp.Core.Latency;

public interface ILatencyProbe
{
    /// <returns>Round-trip time to a reference server on the internet, or null when it did not answer.</returns>
    Task<TimeSpan?> MeasureAsync(CancellationToken cancellationToken);
}
