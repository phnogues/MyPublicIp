using System.Runtime.InteropServices;
using MyPublicIp.Core.Abstractions;
using Windows.ApplicationModel.DataTransfer;

namespace MyPublicIp.Platform;

internal sealed class WindowsClipboard : IClipboard
{
    private const int Attempts = 5;

    /// <summary>
    /// Another app may hold the clipboard open for a moment (clipboard managers, remote desktop...):
    /// retry a few times instead of crashing like the first version of this app did.
    /// </summary>
    public bool TrySetText(string text)
    {
        var package = new DataPackage();
        package.SetText(text);

        for (int attempt = 1; attempt <= Attempts; attempt++)
        {
            try
            {
                Clipboard.SetContent(package);
                // Keep the text available after the app exits
                Clipboard.Flush();
                return true;
            }
            catch (COMException) when (attempt < Attempts)
            {
                Thread.Sleep(40);
            }
            catch (COMException)
            {
            }
        }

        return false;
    }
}
