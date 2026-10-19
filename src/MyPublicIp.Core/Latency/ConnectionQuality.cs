namespace MyPublicIp.Core.Latency;

public enum ConnectionQuality
{
    Unknown,
    Excellent,
    Good,
    Fair,
    Poor,
    Unreachable,
}

public static class ConnectionQualityRating
{
    /// <summary>Thresholds commonly used for interactive use: calls, games, remote desktop.</summary>
    public static ConnectionQuality From(TimeSpan? latency) => latency?.TotalMilliseconds switch
    {
        null => ConnectionQuality.Unreachable,
        < 40 => ConnectionQuality.Excellent,
        < 100 => ConnectionQuality.Good,
        < 200 => ConnectionQuality.Fair,
        _ => ConnectionQuality.Poor,
    };
}
