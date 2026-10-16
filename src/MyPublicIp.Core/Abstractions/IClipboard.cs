namespace MyPublicIp.Core.Abstractions;

public interface IClipboard
{
    /// <summary>Puts text on the clipboard. Returns false if the clipboard could not be opened.</summary>
    bool TrySetText(string text);
}
