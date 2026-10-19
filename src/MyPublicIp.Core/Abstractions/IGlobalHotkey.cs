namespace MyPublicIp.Core.Abstractions;

/// <summary>A keyboard shortcut that works from any app (Win+Shift+I).</summary>
public interface IGlobalHotkey
{
    /// <summary>Raised on the UI thread when the shortcut is pressed.</summary>
    event EventHandler? Pressed;

    /// <summary>Display text of the shortcut, such as "Win + Shift + I".</summary>
    string DisplayText { get; }

    /// <returns>False when another app already uses the shortcut.</returns>
    bool TryRegister();

    void Unregister();
}
