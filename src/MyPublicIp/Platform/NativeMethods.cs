using System.Runtime.InteropServices;

namespace MyPublicIp.Platform;

/// <summary>The few Win32 calls that WinUI and the Windows App SDK do not expose.</summary>
internal static partial class NativeMethods
{
    private const uint MonitorDefaultToNearest = 2;
    private const int EffectiveDpi = 0;
    private const int SmallIconWidthMetric = 49; // SM_CXSMICON

    private enum PreferredAppMode
    {
        Default,
        AllowDark,
        ForceDark,
        ForceLight,
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X;
        public int Y;
    }

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetCursorPos(out POINT point);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetForegroundWindow(nint hwnd);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool AllowSetForegroundWindow(uint processId);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool DestroyIcon(nint icon);

    /// <summary>Display scale factor (1.0 = 96 DPI) of the monitor containing the point.</summary>
    public static double GetScaleAt(POINT point)
    {
        nint monitor = MonitorFromPoint(point, MonitorDefaultToNearest);
        return GetDpiForMonitor(monitor, EffectiveDpi, out uint dpi, out _) == 0 ? dpi / 96.0 : 1.0;
    }

    /// <summary>Size in pixels of the icons shown in the notification area.</summary>
    public static int GetSmallIconSize() => GetSystemMetricsForDpi(SmallIconWidthMetric, GetDpiForSystem());

    /// <summary>Makes Win32 popup menus dark or light.</summary>
    public static void SetMenuTheme(bool dark)
    {
        try
        {
            SetPreferredAppMode(dark ? PreferredAppMode.ForceDark : PreferredAppMode.ForceLight);
            FlushMenuThemes();
        }
        catch (EntryPointNotFoundException)
        {
            // Windows 10 before 1903: menus stay light
        }
    }

    [LibraryImport("user32.dll")]
    private static partial nint MonitorFromPoint(POINT point, uint flags);

    [LibraryImport("shcore.dll")]
    private static partial int GetDpiForMonitor(nint monitor, int dpiType, out uint dpiX, out uint dpiY);

    [LibraryImport("user32.dll")]
    private static partial uint GetDpiForSystem();

    [LibraryImport("user32.dll")]
    private static partial int GetSystemMetricsForDpi(int index, uint dpi);

    // Undocumented but stable since Windows 10 1903 (used by Explorer, Notepad++, PowerToys...)
    [LibraryImport("uxtheme.dll", EntryPoint = "#135")]
    private static partial PreferredAppMode SetPreferredAppMode(PreferredAppMode mode);

    [LibraryImport("uxtheme.dll", EntryPoint = "#136")]
    private static partial void FlushMenuThemes();
}
