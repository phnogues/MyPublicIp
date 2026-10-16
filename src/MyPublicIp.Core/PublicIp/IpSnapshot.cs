namespace MyPublicIp.Core.PublicIp;

public enum IpLookupStatus
{
    /// <summary>No lookup has completed yet.</summary>
    Pending,

    /// <summary>At least one public address was found.</summary>
    Ok,

    /// <summary>The machine has no network connection.</summary>
    Offline,

    /// <summary>The network is up but no lookup service answered.</summary>
    Failed,
}

/// <summary>Immutable state of the public IP lookup.</summary>
public sealed record IpSnapshot(
    IpLookupStatus Status,
    string? IPv4,
    string? IPv6,
    DateTimeOffset? UpdatedAt,
    bool IsRefreshing)
{
    public static IpSnapshot Initial { get; } = new(IpLookupStatus.Pending, null, null, null, IsRefreshing: true);

    public bool HasAddress => IPv4 is not null || IPv6 is not null;
}
