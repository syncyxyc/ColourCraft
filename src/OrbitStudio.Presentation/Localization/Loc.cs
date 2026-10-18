using System.Globalization;
using System.Windows;

namespace OrbitStudio.Presentation.Localization;

/// <summary>
/// Zugriff auf die zentralen Sprachressourcen (Resources/Strings.*.xaml) aus Code.
/// Fehlende Schlüssel werden sichtbar als [Schlüssel] dargestellt, damit sie auffallen.
/// </summary>
public static class Loc
{
    public const string StringsResourceUri = "pack://application:,,,/OrbitStudio.Presentation;component/Resources/Strings.de.xaml";

    private static ResourceDictionary? _fallback;

    public static CultureInfo Culture { get; } = CultureInfo.GetCultureInfo("de-DE");

    public static string Get(string key)
    {
        var value = System.Windows.Application.Current?.TryFindResource(key) as string
                    ?? Fallback()[key] as string;
        return value ?? $"[{key}]";
    }

    public static string Format(string key, params object?[] args) =>
        string.Format(Culture, Get(key), args);

    /// <summary>Entfernt die Zugriffstasten-Markierung (_) für Texte außerhalb von Menüs.</summary>
    public static string WithoutAccessKey(string text) => text.Replace("_", string.Empty, StringComparison.Ordinal);

    // Ermöglicht Zugriff auch ohne laufende WPF-Application (z. B. in Tests).
    private static ResourceDictionary Fallback() =>
        _fallback ??= new ResourceDictionary { Source = new Uri(StringsResourceUri, UriKind.Absolute) };
}
