using System.Text.Json;
using System.Text.Json.Serialization;
using MyPublicIp.Core.History;
using Windows.Storage;

namespace MyPublicIp.Platform;

/// <summary>History saved as a small JSON file in the package local folder.</summary>
internal sealed class JsonIpHistoryStore : IIpHistoryStore
{
    private const string FileName = "history.json";

    public IReadOnlyList<IpHistoryEntry> Load()
    {
        try
        {
            string? path = GetPath();
            if (path is null || !File.Exists(path))
            {
                return [];
            }

            using FileStream stream = File.OpenRead(path);
            return JsonSerializer.Deserialize(stream, HistoryJsonContext.Default.ListIpHistoryEntry) ?? [];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            // A damaged history is not worth an error: start a new one
            return [];
        }
    }

    public void Save(IReadOnlyList<IpHistoryEntry> entries)
    {
        try
        {
            if (GetPath() is not { } path)
            {
                return;
            }

            // Write then replace, so that a crash never leaves a truncated file
            string temporary = path + ".tmp";
            using (FileStream stream = File.Create(temporary))
            {
                JsonSerializer.Serialize(stream, [.. entries], HistoryJsonContext.Default.ListIpHistoryEntry);
            }

            File.Move(temporary, path, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The history is a convenience: never fail the app because it could not be saved
        }
    }

    private static string? GetPath()
    {
        try
        {
            return Path.Combine(ApplicationData.Current.LocalFolder.Path, FileName);
        }
        catch (InvalidOperationException)
        {
            // No package identity (unpackaged run)
            return null;
        }
    }
}

/// <summary>Source-generated serializer: no reflection, safe with the trimmed Store build.</summary>
[JsonSerializable(typeof(List<IpHistoryEntry>))]
[JsonSourceGenerationOptions(WriteIndented = true)]
internal sealed partial class HistoryJsonContext : JsonSerializerContext;
