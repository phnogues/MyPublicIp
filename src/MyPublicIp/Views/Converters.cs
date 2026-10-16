using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MyPublicIp.Core.ViewModels;

namespace MyPublicIp.Views;

/// <summary>Functions used by x:Bind to turn view model values into XAML values.</summary>
internal static class Converters
{
    private const string CopyGlyph = "\uE8C8";
    private const string CheckGlyph = "\uE73E";

    public static string CopyGlyphFor(bool copied) => copied ? CheckGlyph : CopyGlyph;

    public static Visibility CollapsedWhen(bool value) => value ? Visibility.Collapsed : Visibility.Visible;

    public static Windows.UI.Text.FontWeight WeightFor(bool emphasized) =>
        emphasized ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal;

    public static InfoBarSeverity SeverityFor(StatusKind status) =>
        status == StatusKind.Failed ? InfoBarSeverity.Error : InfoBarSeverity.Warning;
}
