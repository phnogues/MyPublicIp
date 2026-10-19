using MyPublicIp.Core.Geo;
using MyPublicIp.Core.History;
using MyPublicIp.Core.Latency;
using MyPublicIp.Core.LocalNetwork;

namespace MyPublicIp.Diagnostics;

/// <summary>A believable history for screenshots, kept in memory so the real one is never touched.</summary>
internal sealed class DemoIpHistoryStore : IIpHistoryStore
{
    private IReadOnlyList<IpHistoryEntry> _entries;

    public DemoIpHistoryStore(TimeProvider time)
    {
        DateTimeOffset now = time.GetLocalNow();
        _entries =
        [
            new IpHistoryEntry(now.Date.AddDays(-1).AddHours(18).AddMinutes(3), "198.51.100.7", null, "Nimbus VPN", "DE"),
            new IpHistoryEntry(now.Date.AddDays(-3).AddHours(8).AddMinutes(12), "203.0.113.42", null, null, "FR"),
            new IpHistoryEntry(now.Date.AddDays(-4).AddHours(19).AddMinutes(30), "192.0.2.81", null, "Nimbus VPN", "NL"),
            new IpHistoryEntry(now.Date.AddDays(-6).AddHours(21).AddMinutes(47), "203.0.113.42", null, null, "FR"),
        ];
    }

    public IReadOnlyList<IpHistoryEntry> Load() => _entries;

    public void Save(IReadOnlyList<IpHistoryEntry> entries) => _entries = entries;
}

/// <summary>Locations for the documentation addresses used by <see cref="DemoPublicIpResolver"/>.</summary>
internal sealed class DemoGeoLocator : IGeoLocator
{
    public async Task<GeoInfo?> LocateAsync(string address, CancellationToken cancellationToken)
    {
        await Task.Delay(300, cancellationToken);
        return address == DemoPublicIpResolver.VpnAddress
            ? new GeoInfo("DE", "Frankfurt am Main", "Hesse", "Nimbus VPN")
            : new GeoInfo("FR", "Paris", "Île-de-France", "Aurora Telecom");
    }
}

/// <summary>A home Wi-Fi network, with the VPN on whenever the demo resolver answers its VPN address.</summary>
internal sealed class DemoLocalNetworkInfo(DemoPublicIpResolver resolver) : ILocalNetworkInfo
{
    public NetworkDetails? GetPrimaryNetwork() => new(
        "Wi-Fi", "Intel(R) Wi-Fi 6E AX211 160MHz", ConnectionKind.WiFi, "192.168.1.42", "192.168.1.1",
        ["192.168.1.1", "1.1.1.1"], "3C:A9:F4:5B:21:7E", 1_201_000_000)
    {
        NetworkName = "Home-5G",
        SignalBars = 4,
    };

    public VpnStatus GetVpnStatus() => resolver.UsingVpn ? new VpnStatus(true, "Nimbus VPN") : VpnStatus.Off;
}

/// <summary>A good fibre connection: 12 to 24 ms.</summary>
internal sealed class DemoLatencyProbe : ILatencyProbe
{
    public async Task<TimeSpan?> MeasureAsync(CancellationToken cancellationToken)
    {
        await Task.Delay(50, cancellationToken);
        return TimeSpan.FromMilliseconds(Random.Shared.Next(12, 25));
    }
}
