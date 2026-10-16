using Windows.UI;
using Windows.UI.ViewManagement;

namespace MyPublicIp.Platform;

/// <summary>
/// Win32 popup menus (the tray context menu) are light by default: follow the Windows app theme,
/// including when the user switches between light and dark mode.
/// </summary>
internal sealed class MenuTheme : IDisposable
{
    private readonly UISettings _settings = new();

    public MenuTheme()
    {
        Apply();
        _settings.ColorValuesChanged += OnColorValuesChanged;
    }

    public void Dispose() => _settings.ColorValuesChanged -= OnColorValuesChanged;

    private void OnColorValuesChanged(UISettings sender, object args) => Apply();

    private void Apply()
    {
        // The app background is black in dark mode and white in light mode
        Color background = _settings.GetColorValue(UIColorType.Background);
        NativeMethods.SetMenuTheme(dark: background.R < 128);
    }
}
