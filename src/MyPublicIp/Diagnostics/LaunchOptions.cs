using Microsoft.UI.Xaml;

namespace MyPublicIp.Diagnostics;

/// <summary>
/// Debug-only switches used to take the README screenshots:
/// <c>--demo</c> shows documentation addresses instead of the real public IP,
/// <c>--theme=light|dark</c> forces the theme and <c>--lang=en-US</c> forces the language.
/// Release builds ignore them.
/// </summary>
internal sealed record LaunchOptions(bool Demo, ApplicationTheme? Theme, string? Language)
{
    public static LaunchOptions Current { get; } = Parse(Environment.GetCommandLineArgs());

    /// <summary>Applies the options that must be set before any resource is loaded.</summary>
    public void ApplyTo(Application app)
    {
#if DEBUG
        if (Theme is { } theme)
        {
            app.RequestedTheme = theme;
        }

        if (Language is not null)
        {
            // The Windows App SDK API is the one XAML (x:Uid) and ResourceLoader both read in this process
            Microsoft.Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride = Language;
        }
        else if (!string.IsNullOrEmpty(Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride))
        {
            // The override is persisted: a normal run goes back to the user's language. Only the Windows API
            // accepts an empty value (the Windows App SDK one throws).
            Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride = string.Empty;
        }
#else
        _ = app;
#endif
    }

    private static LaunchOptions Parse(string[] args)
    {
#if DEBUG
        string? Value(string name) => args
            .Where(a => a.StartsWith($"--{name}=", StringComparison.OrdinalIgnoreCase))
            .Select(a => a[(name.Length + 3)..])
            .LastOrDefault();

        ApplicationTheme? theme = Value("theme")?.ToLowerInvariant() switch
        {
            "light" => ApplicationTheme.Light,
            "dark" => ApplicationTheme.Dark,
            _ => null,
        };

        return new LaunchOptions(args.Contains("--demo", StringComparer.OrdinalIgnoreCase), theme, Value("lang"));
#else
        _ = args;
        return new LaunchOptions(Demo: false, Theme: null, Language: null);
#endif
    }
}
