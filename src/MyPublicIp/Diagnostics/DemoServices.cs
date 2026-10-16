using MyPublicIp.Core.History;
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
            new IpHistoryEntry(now.Date.AddDays(-1).AddHours(18).AddMinutes(3), "198.51.100.7", null),
            new IpHistoryEntry(now.Date.AddDays(-3).AddHours(8).AddMinutes(12), "192.0.2.81", null),
            new IpHistoryEntry(now.Date.AddDays(-6).AddHours(21).AddMinutes(47), "198.51.100.7", null),
        ];
    }

    public IReadOnlyList<IpHistoryEntry> Load() => _entries;

    public void Save(IReadOnlyList<IpHistoryEntry> entries) => _entries = entries;
}

internal sealed class DemoLocalNetworkInfo : ILocalNetworkInfo
{
    public LocalAddress? GetPrimaryIPv4() => new("192.168.1.42", "Wi-Fi");
}
