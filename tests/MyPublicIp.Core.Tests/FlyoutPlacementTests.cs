using MyPublicIp.Core.Layout;

namespace MyPublicIp.Core.Tests;

public sealed class FlyoutPlacementTests
{
    private static readonly PixelRect Screen = new(0, 0, 1920, 1080);
    private const int Width = 340;
    private const int Height = 300;
    private const int Margin = 12;

    [Fact]
    public void Taskbar_at_the_bottom_puts_the_flyout_above_the_clock()
    {
        PixelRect workArea = new(0, 0, 1920, 1032);

        PixelRect flyout = FlyoutPlacement.Compute(workArea, Screen, Width, Height, Margin);

        Assert.Equal(new PixelRect(1920 - Width - Margin, 1032 - Height - Margin, Width, Height), flyout);
    }

    [Fact]
    public void Taskbar_at_the_top_puts_the_flyout_below_it()
    {
        PixelRect workArea = new(0, 48, 1920, 1032);

        PixelRect flyout = FlyoutPlacement.Compute(workArea, Screen, Width, Height, Margin);

        Assert.Equal(new PixelRect(1920 - Width - Margin, 48 + Margin, Width, Height), flyout);
    }

    [Fact]
    public void Taskbar_on_the_left_puts_the_flyout_in_the_bottom_left_corner()
    {
        PixelRect workArea = new(62, 0, 1858, 1080);

        PixelRect flyout = FlyoutPlacement.Compute(workArea, Screen, Width, Height, Margin);

        Assert.Equal(new PixelRect(62 + Margin, 1080 - Height - Margin, Width, Height), flyout);
    }

    [Fact]
    public void Works_on_a_secondary_screen()
    {
        PixelRect screen = new(-2560, 0, 2560, 1440);
        PixelRect workArea = new(-2560, 0, 2560, 1392);

        PixelRect flyout = FlyoutPlacement.Compute(workArea, screen, Width, Height, Margin);

        Assert.Equal(new PixelRect(-Width - Margin, 1392 - Height - Margin, Width, Height), flyout);
    }
}
