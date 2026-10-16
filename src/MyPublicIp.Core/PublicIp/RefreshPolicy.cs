namespace MyPublicIp.Core.PublicIp;

/// <summary>When <see cref="PublicIpMonitor"/> looks the address up again.</summary>
public sealed record RefreshPolicy(
    TimeSpan Interval,
    TimeSpan NetworkSettleDelay,
    IReadOnlyList<TimeSpan> RetryDelays)
{
    public static RefreshPolicy Default { get; } = new(
        Interval: TimeSpan.FromMinutes(15),
        NetworkSettleDelay: TimeSpan.FromSeconds(2),
        RetryDelays:
        [
            TimeSpan.FromSeconds(3),
            TimeSpan.FromSeconds(5),
            TimeSpan.FromSeconds(10),
            TimeSpan.FromSeconds(30),
            TimeSpan.FromSeconds(60),
        ]);

    /// <summary>Delay before the next attempt after <paramref name="consecutiveFailures"/> failures (1 or more).</summary>
    public TimeSpan GetRetryDelay(int consecutiveFailures)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(consecutiveFailures, 1);
        return RetryDelays[Math.Min(consecutiveFailures, RetryDelays.Count) - 1];
    }
}
