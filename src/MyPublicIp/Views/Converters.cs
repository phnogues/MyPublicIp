using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using MyPublicIp.Core.Latency;
using MyPublicIp.Core.ViewModels;
using Windows.Foundation;

namespace MyPublicIp.Views;

/// <summary>Functions used by x:Bind to turn view model values into XAML values.</summary>
internal static class Converters
{
    private const string CopyGlyphValue = "";
    private const string CheckGlyph = "";
    private const string ShieldCheckGlyph = "";
    private const string ShieldGlyph = "";

    public static string CopyGlyph(string? copiedItem, string item) => copiedItem == item ? CheckGlyph : CopyGlyphValue;

    public static Visibility CollapsedWhen(bool value) => value ? Visibility.Collapsed : Visibility.Visible;

    public static Visibility HasText(string? value) => string.IsNullOrEmpty(value) ? Visibility.Collapsed : Visibility.Visible;

    public static Windows.UI.Text.FontWeight WeightFor(bool emphasized) =>
        emphasized ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal;

    public static InfoBarSeverity SeverityFor(StatusKind status) =>
        status == StatusKind.Failed ? InfoBarSeverity.Error : InfoBarSeverity.Warning;

    public static string VpnGlyph(bool active) => active ? ShieldCheckGlyph : ShieldGlyph;

    public static Brush VpnBrush(bool active) => ThemeBrush(active ? "SystemFillColorSuccessBrush" : "TextFillColorSecondaryBrush");

    public static Brush QualityBrush(ConnectionQuality quality) => ThemeBrush(quality switch
    {
        ConnectionQuality.Excellent or ConnectionQuality.Good => "SystemFillColorSuccessBrush",
        ConnectionQuality.Fair => "SystemFillColorCautionBrush",
        ConnectionQuality.Poor or ConnectionQuality.Unreachable => "SystemFillColorCriticalBrush",
        _ => "TextFillColorSecondaryBrush",
    });

    public static Brush RowBackground(bool current) =>
        current ? ThemeBrush("SubtleFillColorSecondaryBrush") : new SolidColorBrush(Colors.Transparent);

    public static string SignalText(int bars) => new string('●', Math.Clamp(bars, 0, 5)) + new string('○', 5 - Math.Clamp(bars, 0, 5));

    /// <summary>Latency history as a small line chart; a missing answer is drawn at the top (worst).</summary>
    public static PointCollection Sparkline(IReadOnlyList<double?> values, double width, double height)
    {
        var points = new PointCollection();
        if (values.Count == 0)
        {
            return points;
        }

        double max = Math.Max(50, values.Max(v => v ?? 0) * 1.2);
        double step = values.Count > 1 ? width / (values.Count - 1) : 0;
        for (int i = 0; i < values.Count; i++)
        {
            double value = values[i] ?? max;
            points.Add(new Point(i * step, height - (Math.Min(value, max) / max * height)));
        }

        return points;
    }

    private static Brush ThemeBrush(string key) => (Brush)Application.Current.Resources[key];
}
