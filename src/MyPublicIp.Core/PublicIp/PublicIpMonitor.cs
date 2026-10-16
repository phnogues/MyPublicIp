using System.Net;
using System.Net.Sockets;

namespace MyPublicIp.Core.PublicIp;

/// <summary>
/// Keeps the public IPv4/IPv6 addresses up to date: periodically, after a network change,
/// on demand, and with a short backoff after a failure. A failure is never cached as an address.
/// </summary>
public sealed class PublicIpMonitor : IPublicIpMonitor, IDisposable
{
    private readonly IPublicIpResolver _resolver;
    private readonly INetworkMonitor _network;
    private readonly TimeProvider _time;
    private readonly RefreshPolicy _policy;
    private readonly CancellationTokenSource _stopping = new();
    private readonly Lock _gate = new();
    private TaskCompletionSource _refreshRequested = NewSignal();
    private IpSnapshot _current = IpSnapshot.Initial;
    private int _networkChangeCount;
    private int _waitCount;
    private Task? _loop;

    public PublicIpMonitor(IPublicIpResolver resolver, INetworkMonitor network, TimeProvider time, RefreshPolicy? policy = null)
    {
        _resolver = resolver;
        _network = network;
        _time = time;
        _policy = policy ?? RefreshPolicy.Default;
    }

    public event EventHandler? Changed;

    public IpSnapshot Current
    {
        get
        {
            lock (_gate)
            {
                return _current;
            }
        }
    }

    public void Start()
    {
        if (_loop is not null)
        {
            return;
        }

        _network.NetworkChanged += OnNetworkChanged;
        _loop = Task.Run(() => RunAsync(_stopping.Token));
    }

    /// <summary>Number of times the loop started waiting for the next refresh (lets tests advance time deterministically).</summary>
    internal int WaitCount => Volatile.Read(ref _waitCount);

    public void RequestRefresh() => Volatile.Read(ref _refreshRequested).TrySetResult();

    public void Dispose()
    {
        _network.NetworkChanged -= OnNetworkChanged;
        _stopping.Cancel();
    }

    private static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private async void OnNetworkChanged(object? sender, EventArgs e)
    {
        // Let the new connection get its routes and DNS, and let bursts of events settle
        // (adapters coming up one after another, VPN, Hyper-V...) before looking up once
        int change = Interlocked.Increment(ref _networkChangeCount);
        try
        {
            await Task.Delay(_policy.NetworkSettleDelay, _time, _stopping.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (change == Volatile.Read(ref _networkChangeCount))
        {
            RequestRefresh();
        }
    }

    private async Task RunAsync(CancellationToken stopping)
    {
        int failures = 0;
        while (!stopping.IsCancellationRequested)
        {
            // Reset before looking up: a request made during the lookup triggers another one right after
            Interlocked.Exchange(ref _refreshRequested, NewSignal());

            bool found;
            try
            {
                found = await RefreshAsync(stopping).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            failures = found ? 0 : failures + 1;
            TimeSpan delay = found ? _policy.Interval : _policy.GetRetryDelay(failures);
            await WaitForNextRefreshAsync(delay, stopping).ConfigureAwait(false);
        }
    }

    private async Task WaitForNextRefreshAsync(TimeSpan delay, CancellationToken stopping)
    {
        using var timerCancellation = CancellationTokenSource.CreateLinkedTokenSource(stopping);
        Task timer = Task.Delay(delay, _time, timerCancellation.Token);
        Task requested = Volatile.Read(ref _refreshRequested).Task;
        Interlocked.Increment(ref _waitCount);

        await Task.WhenAny(timer, requested).ConfigureAwait(false);
        await timerCancellation.CancelAsync().ConfigureAwait(false);
    }

    private async Task<bool> RefreshAsync(CancellationToken cancellationToken)
    {
        Update(s => s with { IsRefreshing = true });

        Task<IPAddress?> ipv4Lookup = ResolveAsync(AddressFamily.InterNetwork, cancellationToken);
        Task<IPAddress?> ipv6Lookup = ResolveAsync(AddressFamily.InterNetworkV6, cancellationToken);

        // IPv4 is what most people need: publish it without waiting for IPv6
        string? ipv4 = (await ipv4Lookup.ConfigureAwait(false))?.ToString();
        if (ipv4 is not null)
        {
            Update(s => s with { Status = IpLookupStatus.Ok, IPv4 = ipv4, UpdatedAt = _time.GetLocalNow() });
        }

        string? ipv6 = (await ipv6Lookup.ConfigureAwait(false))?.ToString();
        cancellationToken.ThrowIfCancellationRequested();

        if (ipv4 is null && ipv6 is null)
        {
            IpLookupStatus status = _network.IsNetworkAvailable ? IpLookupStatus.Failed : IpLookupStatus.Offline;
            Update(s => new IpSnapshot(status, null, null, s.UpdatedAt, IsRefreshing: false));
            return false;
        }

        Update(_ => new IpSnapshot(IpLookupStatus.Ok, ipv4, ipv6, _time.GetLocalNow(), IsRefreshing: false));
        return true;
    }

    private async Task<IPAddress?> ResolveAsync(AddressFamily family, CancellationToken cancellationToken)
    {
        try
        {
            return await _resolver.ResolveAsync(family, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // A resolver bug must not stop the refresh loop
            return null;
        }
    }

    private void Update(Func<IpSnapshot, IpSnapshot> change)
    {
        lock (_gate)
        {
            IpSnapshot next = change(_current);
            if (next == _current)
            {
                return;
            }

            _current = next;
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }
}
