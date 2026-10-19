using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using MyPublicIp.Core.Abstractions;

namespace MyPublicIp.Platform;

/// <summary>
/// Win+Shift+I from any app, through RegisterHotKey and a message-only window created on the UI
/// thread: WM_HOTKEY then arrives through the app's own message loop, on the UI thread.
/// </summary>
internal sealed unsafe partial class GlobalHotkey : IGlobalHotkey, IDisposable
{
    private const int HotkeyId = 0x4950; // "IP"
    private const uint ModShift = 0x0004;
    private const uint ModWin = 0x0008;
    private const uint ModNoRepeat = 0x4000;
    private const uint VirtualKeyI = 0x49;
    private const uint WmHotkey = 0x0312;
    private const string ClassName = "MyPublicIp.HotkeyWindow";

    private static readonly nint MessageOnlyParent = -3; // HWND_MESSAGE
    private static GlobalHotkey? s_instance;

    private readonly nint _window;
    private bool _registered;

    /// <param name="displayText">The keys in the user's language ("Win + Shift + I", "Win + Maj + I").</param>
    public GlobalHotkey(string displayText)
    {
        DisplayText = displayText;
        s_instance = this;
        nint module = GetModuleHandleW(null);
        fixed (char* className = ClassName)
        {
            var windowClass = new WNDCLASSEXW
            {
                cbSize = (uint)sizeof(WNDCLASSEXW),
                lpfnWndProc = &WindowProcedure,
                hInstance = module,
                lpszClassName = className,
            };
            RegisterClassExW(&windowClass);
            _window = CreateWindowExW(0, className, className, 0, 0, 0, 0, 0, MessageOnlyParent, 0, module, 0);
        }
    }

    public event EventHandler? Pressed;

    public string DisplayText { get; }

    public bool TryRegister()
    {
        if (!_registered && _window != 0)
        {
            _registered = RegisterHotKey(_window, HotkeyId, ModWin | ModShift | ModNoRepeat, VirtualKeyI);
        }

        return _registered;
    }

    public void Unregister()
    {
        if (_registered)
        {
            UnregisterHotKey(_window, HotkeyId);
            _registered = false;
        }
    }

    public void Dispose()
    {
        Unregister();
        if (_window != 0)
        {
            DestroyWindow(_window);
        }

        s_instance = null;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static nint WindowProcedure(nint window, uint message, nint wParam, nint lParam)
    {
        if (message == WmHotkey && wParam == HotkeyId)
        {
            s_instance?.Pressed?.Invoke(s_instance, EventArgs.Empty);
            return 0;
        }

        return DefWindowProcW(window, message, wParam, lParam);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WNDCLASSEXW
    {
        public uint cbSize;
        public uint style;
        public delegate* unmanaged[Stdcall]<nint, uint, nint, nint, nint> lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public nint hInstance;
        public nint hIcon;
        public nint hCursor;
        public nint hbrBackground;
        public char* lpszMenuName;
        public char* lpszClassName;
        public nint hIconSm;
    }

    [LibraryImport("kernel32.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static partial nint GetModuleHandleW(string? moduleName);

    [LibraryImport("user32.dll")]
    private static partial ushort RegisterClassExW(WNDCLASSEXW* windowClass);

    [LibraryImport("user32.dll")]
    private static partial nint CreateWindowExW(
        uint exStyle, char* className, char* windowName, uint style, int x, int y, int width, int height,
        nint parent, nint menu, nint instance, nint param);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DestroyWindow(nint window);

    [LibraryImport("user32.dll")]
    private static partial nint DefWindowProcW(nint window, uint message, nint wParam, nint lParam);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool RegisterHotKey(nint window, int id, uint modifiers, uint virtualKey);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool UnregisterHotKey(nint window, int id);
}
