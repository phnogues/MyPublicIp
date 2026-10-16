namespace MyPublicIp.Core.Layout;

/// <summary>Rectangle in physical pixels.</summary>
public readonly record struct PixelRect(int X, int Y, int Width, int Height)
{
    public int Right => X + Width;

    public int Bottom => Y + Height;
}

/// <summary>
/// Docks the flyout in the corner next to the clock, like the Windows 11 quick settings,
/// whatever the side of the screen the taskbar is on.
/// </summary>
public static class FlyoutPlacement
{
    public static PixelRect Compute(PixelRect workArea, PixelRect screen, int width, int height, int margin)
    {
        // The work area is the screen minus the taskbar: the side where they differ is the taskbar side
        bool taskbarOnTop = workArea.Y > screen.Y;
        bool taskbarOnLeft = workArea.X > screen.X;

        int x = taskbarOnLeft ? workArea.X + margin : workArea.Right - width - margin;
        int y = taskbarOnTop ? workArea.Y + margin : workArea.Bottom - height - margin;

        return new PixelRect(x, y, width, height);
    }
}
