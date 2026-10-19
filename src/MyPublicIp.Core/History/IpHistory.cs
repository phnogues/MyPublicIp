using System.Net;
using MyPublicIp.Core.Abstractions;
using MyPublicIp.Core.LocalNetwork;
using MyPublicIp.Core.PublicIp;

namespace MyPublicIp.Core.History;

/// <summary>
/// Records each new public address and notifies the user when it changes during a session,
/// which is how a dropped VPN or a new address from the internet provider gets noticed.
/// </summary>
public sealed class IpHistory
{
    public const int MaxEntries = 20;

    /// <summary>Size of an IPv6 network prefix: the rest of the address changes regularly for privacy.</summary>
    private const int IPv6PrefixLength = 64;

    private readonly IIpHistoryStore _store;
    private readonly IUserNotifier _notifier;
    private readonly IAppSettings _settings;
    private readonly List<IpHistoryEntry> _entries;
    private bool _observedThisSession;

    public IpHistory(IIpHistoryStore store, IUserNotifier notifier, IAppSettings settings)
    {
        _store = store;
        _notifier = notifier;
        _settings = settings;
        _entries = [.. store.Load().Take(MaxEntries)];
    }

    public event EventHandler? Changed;

    /// <summary>Most recent first.</summary>
    public IReadOnlyList<IpHistoryEntry> Entries => _entries;

    /// <summary>
    /// Takes into account the result of a complete lookup, with the VPN connected at that time.
    /// The first result of a session is only recorded: a notification at every login because the
    /// address changed overnight would be noise.
    /// </summary>
    public void Observe(IpSnapshot snapshot, VpnStatus vpn)
    {
        if (snapshot.Status != IpLookupStatus.Ok || snapshot.IsRefreshing || snapshot.UpdatedAt is not { } seenAt)
        {
            return;
        }

        var current = new IpHistoryEntry(seenAt, snapshot.IPv4, snapshot.IPv6, vpn.IsActive ? vpn.Name : null);
        IpHistoryEntry? previous = _entries.FirstOrDefault();
        bool firstOfSession = !_observedThisSession;
        _observedThisSession = true;

        if (previous is not null && IsSameConnection(previous, current))
        {
            return;
        }

        _entries.Insert(0, current);
        if (_entries.Count > MaxEntries)
        {
            _entries.RemoveRange(MaxEntries, _entries.Count - MaxEntries);
        }

        _store.Save(_entries);

        if (previous is not null && !firstOfSession && _settings.NotifyOnIpChange)
        {
            _notifier.NotifyIpChanged(previous, current);
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Adds the country of an address once the geolocation service has answered.</summary>
    public void SetCountry(string address, string countryCode)
    {
        int index = _entries.FindIndex(e => e.MainAddress == address && e.CountryCode != countryCode);
        if (index < 0)
        {
            return;
        }

        _entries[index] = _entries[index] with { CountryCode = countryCode };
        _store.Save(_entries);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Same IPv4 and same IPv6 network. IPv6 privacy extensions give a new address every day or so
    /// within the same /64 network: that is not a change worth recording.
    /// </summary>
    internal static bool IsSameConnection(IpHistoryEntry a, IpHistoryEntry b) =>
        a.IPv4 == b.IPv4 && IPv6Prefix(a.IPv6) == IPv6Prefix(b.IPv6);

    private static string? IPv6Prefix(string? address)
    {
        if (address is null || !IPAddress.TryParse(address, out IPAddress? parsed))
        {
            return address;
        }

        byte[] bytes = parsed.GetAddressBytes();
        Array.Clear(bytes, IPv6PrefixLength / 8, bytes.Length - (IPv6PrefixLength / 8));
        return new IPAddress(bytes).ToString();
    }
}
