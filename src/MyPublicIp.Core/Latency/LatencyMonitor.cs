namespace MyPublicIp.Core.Latency;

/// <summary>
/// Measures the latency regularly: every few seconds while the user looks at it, once a minute
/// otherwise, so that the app stays light in the background.
/// </summary>
public sealed class LatencyMonitor : IDisposable
{
    public const int MaxSamples = 30;

    public static readonly TimeSpan FastInterval = TimeSpan.FromSeconds(2);
    public static readonly TimeSpan SlowInterval = TimeSpan.FromMinutes(1);

    private readonly ILatencyProbe _probe;
    private readonly TimeProvider _time;
    private readonly CancellationTokenSource _stopping = new();
    private readonly Lock _gate = new();
    private readonly List<TimeSpan?> _samples = [];
    private TaskCompletionSource _wake = NewSignal();
    private bool _fast;
    private bool _started;
    private int _sampleCount;
    private int _waitCount;

    public LatencyMonitor(ILatencyProbe probe, TimeProvider time)
    {
        _probe = probe;
        _time = time;
    }

    /// <summary>Raised on a background thread after each measurement.</summary>
    public event EventHandler? Changed;

    /// <summary>Oldest first; null means the server did not answer.</summary>
    public IReadOnlyList<TimeSpan?> Samples
    {
        get
        {
            lock (_gate)
            {
                return [.. _samples];
            }
        }
    }

    public TimeSpan? Latest
    {
        get
        {
            lock (_gate)
            {
                return _samples.Count > 0 ? _samples[^1] : null;
            }
        }
    }

    public bool HasSamples
    {
        get
        {
            lock (_gate)
            {
                return _samples.Count > 0;
            }
        }
    }

    /// <summary>Number of measurements done so far (lets tests advance time deterministically).</summary>
    internal int SampleCount => Volatile.Read(ref _sampleCount);

    /// <summary>Number of times the loop started waiting for the next measurement.</summary>
    internal int WaitCount => Volatile.Read(ref _waitCount);

    public void Start()
    {
        if (_started)
        {
            return;
        }

        _started = true;
        _ = Task.Run(() => RunAsync(_stopping.Token));
    }

    /// <summary>Fast mode while the flyout is open; switching to it measures right away.</summary>
    public void SetFastMode(bool fast)
    {
        bool changed = _fast != fast;
        _fast = fast;
        if (changed && fast)
        {
            Volatile.Read(ref _wake).TrySetResult();
        }
    }

    public void Dispose() => _stopping.Cancel();

    private static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private async Task RunAsync(CancellationToken stopping)
    {
        while (!stopping.IsCancellationRequested)
        {
            Interlocked.Exchange(ref _wake, NewSignal());

            TimeSpan? latency;
            try
            {
                latency = await _probe.MeasureAsync(stopping).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                latency = null;
            }

            lock (_gate)
            {
                _samples.Add(latency);
                if (_samples.Count > MaxSamples)
                {
                    _samples.RemoveAt(0);
                }
            }

            Interlocked.Increment(ref _sampleCount);
            Changed?.Invoke(this, EventArgs.Empty);

            using var timerCancellation = CancellationTokenSource.CreateLinkedTokenSource(stopping);
            Task timer = Task.Delay(_fast ? FastInterval : SlowInterval, _time, timerCancellation.Token);
            Interlocked.Increment(ref _waitCount);
            await Task.WhenAny(timer, Volatile.Read(ref _wake).Task).ConfigureAwait(false);
            await timerCancellation.CancelAsync().ConfigureAwait(false);
        }
    }
}
