namespace MyPublicIp.Core.History;

/// <summary>Persists the address history between sessions.</summary>
public interface IIpHistoryStore
{
    /// <returns>Entries, most recent first. Empty when nothing was saved or the data is unreadable.</returns>
    IReadOnlyList<IpHistoryEntry> Load();

    void Save(IReadOnlyList<IpHistoryEntry> entries);
}
