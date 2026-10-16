namespace MyPublicIp.Core.ViewModels;

/// <summary>One line of the "recent addresses" list.</summary>
public sealed record HistoryRow(string When, string Address, bool IsCurrent);
