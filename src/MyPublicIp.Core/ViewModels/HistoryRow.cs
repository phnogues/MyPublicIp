namespace MyPublicIp.Core.ViewModels;

/// <summary>One line of the "History" tab.</summary>
public sealed record HistoryRow(string When, string Address, string CountryCode, bool ThroughVpn, bool IsCurrent);
