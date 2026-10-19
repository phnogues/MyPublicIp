using Microsoft.Extensions.Time.Testing;
using MyPublicIp.Core.Latency;

namespace MyPublicIp.Core.Tests;

public sealed class LatencyMonitorTests : IDisposable
{
    private readonly FakeLatencyProbe _probe = new();
    private readonly FakeTimeProvider _time = new();
    private readonly LatencyMonitor _monitor;

    public LatencyMonitorTests()
    {
        _monitor = new LatencyMonitor(_probe, _time);
    }

    public void Dispose() => _monitor.Dispose();

    [Fact]
    public async Task Measures_once_a_minute_in_the_background()
    {
        _monitor.Start();
        await Wait.UntilAsync(() => _monitor.WaitCount == 1, "the first measurement is done and the next one scheduled");

        _time.Advance(TimeSpan.FromSeconds(30));
        await Task.Delay(50, TestContext.Current.CancellationToken);
        Assert.Equal(1, _monitor.SampleCount);

        _time.Advance(TimeSpan.FromSeconds(30));
        await Wait.UntilAsync(() => _monitor.SampleCount == 2, "the next measurement is done");
    }

    [Fact]
    public async Task Measures_right_away_and_often_when_shown()
    {
        _monitor.Start();
        await Wait.UntilAsync(() => _monitor.SampleCount == 1, "the first measurement is done");

        _monitor.SetFastMode(true);
        await Wait.UntilAsync(() => _monitor.WaitCount == 2, "switching to fast mode measures at once");

        _time.Advance(LatencyMonitor.FastInterval);
        await Wait.UntilAsync(() => _monitor.SampleCount == 3, "the fast interval is used");
    }

    [Fact]
    public async Task Keeps_the_last_measurements_only()
    {
        _monitor.SetFastMode(true);
        _monitor.Start();

        for (int i = 1; i <= LatencyMonitor.MaxSamples + 3; i++)
        {
            int expected = i;
            await Wait.UntilAsync(() => _monitor.WaitCount == expected, "a measurement is done and the next one scheduled");
            _time.Advance(LatencyMonitor.FastInterval);
        }

        Assert.Equal(LatencyMonitor.MaxSamples, _monitor.Samples.Count);
    }

    [Fact]
    public async Task Records_a_missing_answer()
    {
        _probe.Default = null;
        _monitor.Start();
        await Wait.UntilAsync(() => _monitor.SampleCount == 1, "the first measurement is done");

        Assert.True(_monitor.HasSamples);
        Assert.Null(_monitor.Latest);
    }

    [Theory]
    [InlineData(12, ConnectionQuality.Excellent)]
    [InlineData(65, ConnectionQuality.Good)]
    [InlineData(150, ConnectionQuality.Fair)]
    [InlineData(450, ConnectionQuality.Poor)]
    public void Rates_the_latency(int milliseconds, ConnectionQuality expected)
    {
        Assert.Equal(expected, ConnectionQualityRating.From(TimeSpan.FromMilliseconds(milliseconds)));
    }

    [Fact]
    public void No_answer_means_unreachable()
    {
        Assert.Equal(ConnectionQuality.Unreachable, ConnectionQualityRating.From(null));
    }
}
