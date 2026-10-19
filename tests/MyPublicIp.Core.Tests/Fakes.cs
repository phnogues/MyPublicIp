using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using MyPublicIp.Core.Abstractions;
using MyPublicIp.Core.Geo;
using MyPublicIp.Core.History;
using MyPublicIp.Core.Latency;
using MyPublicIp.Core.LocalNetwork;
using MyPublicIp.Core.PublicIp;

namespace MyPublicIp.Core.Tests;

internal sealed class FakeResolver : IPublicIpResolver
{
    private readonly ConcurrentDictionary<AddressFamily, Func<IPAddress?>> _answers = new();
    private int _calls;

    public int IPv4Calls => Volatile.Read(ref _calls);

    public void Answer(AddressFamily family, string? address) =>
        _answers[family] = () => address is null ? null : IPAddress.Parse(address);

    public void Throw(AddressFamily family) =>
        _answers[family] = () => throw new InvalidOperationException("Resolver bug");

    public Task<IPAddress?> ResolveAsync(AddressFamily family, CancellationToken cancellationToken)
    {
        if (family == AddressFamily.InterNetwork)
        {
            Interlocked.Increment(ref _calls);
        }

        return Task.FromResult(_answers.TryGetValue(family, out var answer) ? answer() : null);
    }
}

internal sealed class FakeNetwork : INetworkMonitor
{
    public event EventHandler? NetworkChanged;

    public bool IsNetworkAvailable { get; set; } = true;

    public void RaiseChanged() => NetworkChanged?.Invoke(this, EventArgs.Empty);
}

internal sealed class FakeMonitor : IPublicIpMonitor
{
    public event EventHandler? Changed;

    public IpSnapshot Current { get; private set; } = IpSnapshot.Initial;

    public int RefreshRequests { get; private set; }

    public void Publish(IpSnapshot snapshot)
    {
        Current = snapshot;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void RequestRefresh() => RefreshRequests++;
}

internal sealed class FakeStartupManager(StartupState state) : IStartupManager
{
    public StartupState State { get; set; } = state;

    public List<bool> Requests { get; } = [];

    public Task<StartupState> GetStateAsync() => Task.FromResult(State);

    public Task<StartupState> SetEnabledAsync(bool enabled)
    {
        Requests.Add(enabled);
        if (State.CanChange() && State != StartupState.DisabledByUser)
        {
            State = enabled ? StartupState.Enabled : StartupState.Disabled;
        }

        return Task.FromResult(State);
    }
}

internal sealed class FakeClipboard : IClipboard
{
    public string? Text { get; private set; }

    public bool TrySetText(string text)
    {
        Text = text;
        return true;
    }
}

internal sealed class FakeSettings : IAppSettings
{
    public bool FirstRunDone { get; set; }

    public bool PinTipDismissed { get; set; }

    public bool NotifyOnIpChange { get; set; } = true;

    public bool HotkeyEnabled { get; set; } = true;
}

internal sealed class FakeHistoryStore(params IpHistoryEntry[] saved) : IIpHistoryStore
{
    public IReadOnlyList<IpHistoryEntry> Saved { get; private set; } = saved;

    public int SaveCount { get; private set; }

    public IReadOnlyList<IpHistoryEntry> Load() => Saved;

    public void Save(IReadOnlyList<IpHistoryEntry> entries)
    {
        Saved = [.. entries];
        SaveCount++;
    }
}

internal sealed class FakeNotifier : IUserNotifier
{
    public List<(IpHistoryEntry Previous, IpHistoryEntry Current)> Notifications { get; } = [];

    public List<string> Copied { get; } = [];

    public void NotifyIpChanged(IpHistoryEntry previous, IpHistoryEntry current) => Notifications.Add((previous, current));

    public void NotifyCopied(string address) => Copied.Add(address);
}

internal sealed class FakeLocalNetwork : ILocalNetworkInfo
{
    public NetworkDetails? Details { get; set; } = new(
        "Wi-Fi", "Intel(R) Wi-Fi 6 AX201", ConnectionKind.WiFi, "192.168.1.42", "192.168.1.1",
        ["192.168.1.1", "1.1.1.1"], "00:15:5D:12:34:56", 866_000_000)
    {
        NetworkName = "Home-5G",
        SignalBars = 4,
    };

    public VpnStatus Vpn { get; set; } = VpnStatus.Off;

    public NetworkDetails? GetPrimaryNetwork() => Details;

    public VpnStatus GetVpnStatus() => Vpn;
}

internal sealed class FakeGeoLocator : IGeoLocator
{
    public Dictionary<string, GeoInfo> Known { get; } = new()
    {
        ["203.0.113.42"] = new GeoInfo("FR", "Paris", "Île-de-France", "Aurora Telecom"),
        ["198.51.100.7"] = new GeoInfo("DE", "Frankfurt am Main", "Hesse", "Nimbus VPN"),
    };

    public List<string> Requests { get; } = [];

    public Task<GeoInfo?> LocateAsync(string address, CancellationToken cancellationToken)
    {
        Requests.Add(address);
        return Task.FromResult(Known.GetValueOrDefault(address));
    }
}

internal sealed class FakeLatencyProbe : ILatencyProbe
{
    private readonly ConcurrentQueue<TimeSpan?> _answers = new();

    public TimeSpan? Default { get; set; } = TimeSpan.FromMilliseconds(18);

    public void Enqueue(params TimeSpan?[] answers)
    {
        foreach (TimeSpan? answer in answers)
        {
            _answers.Enqueue(answer);
        }
    }

    public Task<TimeSpan?> MeasureAsync(CancellationToken cancellationToken) =>
        Task.FromResult(_answers.TryDequeue(out TimeSpan? answer) ? answer : Default);
}

internal sealed class FakeHotkey : IGlobalHotkey
{
    public event EventHandler? Pressed;

    public bool Available { get; set; } = true;

    public bool IsRegistered { get; private set; }

    public string DisplayText => "Win + Shift + I";

    public bool TryRegister() => IsRegistered = Available;

    public void Unregister() => IsRegistered = false;

    public void Press() => Pressed?.Invoke(this, EventArgs.Empty);
}

/// <summary>Returns the key itself, or "key(arg1|arg2)" when formatting.</summary>
internal sealed class KeyLocalizer : ILocalizer
{
    public string Get(string key) => key;

    public string Format(string key, params object?[] args) => $"{key}({string.Join('|', args)})";
}

/// <summary>Answers each URL with a fixed status code and body.</summary>
internal sealed class StubHttpHandler : HttpMessageHandler
{
    private readonly Dictionary<string, Func<HttpResponseMessage>> _responses = [];

    public List<string> Requests { get; } = [];

    public StubHttpHandler Respond(string url, HttpStatusCode status, string body = "")
    {
        _responses[new Uri(url).AbsoluteUri] = () => new HttpResponseMessage(status) { Content = new StringContent(body) };
        return this;
    }

    public StubHttpHandler Fail(string url)
    {
        _responses[new Uri(url).AbsoluteUri] = () => throw new HttpRequestException("Connection refused");
        return this;
    }

    public StubHttpHandler TimeOut(string url)
    {
        _responses[new Uri(url).AbsoluteUri] = () => throw new TaskCanceledException("Timeout");
        return this;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        string url = request.RequestUri!.AbsoluteUri;
        Requests.Add(url);
        return _responses.TryGetValue(url, out var respond)
            ? Task.FromResult(respond())
            : Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
    }
}

internal static class Wait
{
    /// <summary>Waits for a condition reached by background work (the monitor loop runs on the thread pool).</summary>
    public static async Task UntilAsync(Func<bool> condition, string because)
    {
        var timeout = Stopwatch.StartNew();
        while (!condition())
        {
            if (timeout.Elapsed > TimeSpan.FromSeconds(5))
            {
                Assert.Fail($"Timed out waiting until {because}.");
            }

            await Task.Delay(10, TestContext.Current.CancellationToken);
        }
    }
}
