using System.Collections.Immutable;
using System.Globalization;

namespace OrbitStudio.Themes;

/// <summary>Namen der Farb-Tokens, die jedes Theme definieren muss.</summary>
public static class ThemeTokens
{
    public const string WindowBackground = "windowBackground";
    public const string PanelBackground = "panelBackground";
    public const string Text = "text";
    public const string TextSecondary = "textSecondary";
    public const string Border = "border";
    public const string Accent = "accent";
    public const string AccentText = "accentText";
    public const string HoverBackground = "hoverBackground";
    public const string FocusBorder = "focusBorder";
    public const string SelectionBackground = "selectionBackground";
    public const string ErrorText = "errorText";

    public static IReadOnlyList<string> All { get; } =
    [
        WindowBackground, PanelBackground, Text, TextSecondary, Border, Accent, AccentText, HoverBackground,
        FocusBorder, SelectionBackground, ErrorText,
    ];
}

public enum ThemeKind
{
    Light,
    Dark,
}

/// <summary>
/// Theme: Farben als „#RRGGBB“ je Token plus Abstand, Eckenradius und UI-Größe. Unveränderlich;
/// <see cref="ThemeValidator"/> prüft, ob es benutzbar ist (alle Tokens, gültige Werte, lesbarer Kontrast).
/// </summary>
public sealed record Theme(
    string Id,
    string Name,
    ThemeKind Kind,
    IReadOnlyDictionary<string, string> Colors,
    double Spacing = 8,
    double CornerRadius = 3,
    double UiScale = 1)
{
    public const string LightId = "light";
    public const string DarkId = "dark";

    public static Theme Light { get; } = new(LightId, "Hell", ThemeKind.Light, Colors2(
        windowBackground: "#F3F3F3", panelBackground: "#FFFFFF", text: "#1A1A1A", textSecondary: "#595959",
        border: "#C8C8C8", accent: "#0F5FC2", accentText: "#FFFFFF", hoverBackground: "#E5E5E5",
        focusBorder: "#0F5FC2", selectionBackground: "#CCE0F8", errorText: "#B00020"));

    public static Theme Dark { get; } = new(DarkId, "Dunkel", ThemeKind.Dark, Colors2(
        windowBackground: "#1E1E1E", panelBackground: "#262626", text: "#F0F0F0", textSecondary: "#B3B3B3",
        border: "#4A4A4A", accent: "#4C9AFF", accentText: "#0A0A0A", hoverBackground: "#333333",
        focusBorder: "#4C9AFF", selectionBackground: "#1F3F66", errorText: "#FF8A80"));

    public static IReadOnlyList<Theme> BuiltIn { get; } = [Light, Dark];

    /// <summary>Farbwert eines Tokens (Großbuchstaben, „#RRGGBB“).</summary>
    public string Color(string token) => Colors[token];

    private static ImmutableDictionary<string, string> Colors2(string windowBackground, string panelBackground, string text,
        string textSecondary, string border, string accent, string accentText, string hoverBackground, string focusBorder,
        string selectionBackground, string errorText) =>
        new Dictionary<string, string>
        {
            [ThemeTokens.WindowBackground] = windowBackground,
            [ThemeTokens.PanelBackground] = panelBackground,
            [ThemeTokens.Text] = text,
            [ThemeTokens.TextSecondary] = textSecondary,
            [ThemeTokens.Border] = border,
            [ThemeTokens.Accent] = accent,
            [ThemeTokens.AccentText] = accentText,
            [ThemeTokens.HoverBackground] = hoverBackground,
            [ThemeTokens.FocusBorder] = focusBorder,
            [ThemeTokens.SelectionBackground] = selectionBackground,
            [ThemeTokens.ErrorText] = errorText,
        }.ToImmutableDictionary(StringComparer.Ordinal);

    /// <summary>Kopie mit einer geänderten Farbe (Themeeditor). Wertprüfung übernimmt <see cref="ThemeValidator"/>.</summary>
    public Theme WithColor(string token, string hex)
    {
        var colors = Colors.ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal);
        colors[token] = hex;
        return this with { Colors = colors };
    }
}

public sealed record ThemeIssue(string? Token, string Message);

public static class ThemeValidator
{
    public const double MinTextContrast = 4.5;
    public const double MinAccentContrast = 3.0;
    public const int MaxNameLength = 60;

    public static (double Min, double Max) SpacingRange { get; } = (2, 24);

    public static (double Min, double Max) CornerRadiusRange { get; } = (0, 16);

    public static (double Min, double Max) UiScaleRange { get; } = (0.75, 2);

    /// <summary>Alle Probleme eines Themes; leere Liste = gültig.</summary>
    public static IReadOnlyList<ThemeIssue> Validate(Theme? theme)
    {
        if (theme is null)
        {
            return [new ThemeIssue(null, "Kein Theme vorhanden.")];
        }

        var issues = new List<ThemeIssue>();
        if (string.IsNullOrWhiteSpace(theme.Id) || !theme.Id.All(c => char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c) || c == '-'))
        {
            issues.Add(new ThemeIssue(null, "Die ID darf nur Kleinbuchstaben, Ziffern und „-“ enthalten."));
        }

        if (string.IsNullOrWhiteSpace(theme.Name) || theme.Name.Length > MaxNameLength || theme.Name.Any(char.IsControl))
        {
            issues.Add(new ThemeIssue(null, $"Der Name muss 1 bis {MaxNameLength} Zeichen lang sein."));
        }

        if (!Enum.IsDefined(theme.Kind))
        {
            issues.Add(new ThemeIssue(null, "Ungültige Art (hell/dunkel)."));
        }

        CheckRange(issues, nameof(theme.Spacing), theme.Spacing, SpacingRange);
        CheckRange(issues, nameof(theme.CornerRadius), theme.CornerRadius, CornerRadiusRange);
        CheckRange(issues, nameof(theme.UiScale), theme.UiScale, UiScaleRange);

        var colors = theme.Colors;
        var parsed = new Dictionary<string, (double R, double G, double B)>();
        foreach (var token in ThemeTokens.All)
        {
            if (colors is null || !colors.TryGetValue(token, out var hex))
            {
                issues.Add(new ThemeIssue(token, $"Die Farbe „{token}“ fehlt."));
            }
            else if (!TryParseHex(hex, out var rgb))
            {
                issues.Add(new ThemeIssue(token, $"„{hex}“ ist kein gültiger Farbwert (erwartet #RRGGBB)."));
            }
            else
            {
                parsed[token] = rgb;
            }
        }

        foreach (var unknown in colors?.Keys.Except(ThemeTokens.All) ?? [])
        {
            issues.Add(new ThemeIssue(unknown, $"Unbekanntes Token „{unknown}“."));
        }

        CheckContrast(issues, parsed, ThemeTokens.Text, ThemeTokens.WindowBackground, MinTextContrast);
        CheckContrast(issues, parsed, ThemeTokens.Text, ThemeTokens.PanelBackground, MinTextContrast);
        CheckContrast(issues, parsed, ThemeTokens.TextSecondary, ThemeTokens.PanelBackground, MinTextContrast);
        CheckContrast(issues, parsed, ThemeTokens.ErrorText, ThemeTokens.PanelBackground, MinTextContrast);
        CheckContrast(issues, parsed, ThemeTokens.AccentText, ThemeTokens.Accent, MinTextContrast);
        CheckContrast(issues, parsed, ThemeTokens.FocusBorder, ThemeTokens.PanelBackground, MinAccentContrast);
        return issues;
    }

    /// <summary>Kontrastverhältnis nach WCAG 2.x (1 … 21).</summary>
    public static double ContrastRatio(string hexA, string hexB)
    {
        if (!TryParseHex(hexA, out var a) || !TryParseHex(hexB, out var b))
        {
            throw new FormatException("Ungültiger Farbwert.");
        }

        return Ratio(a, b);
    }

    public static bool TryParseHex(string? hex, out (double R, double G, double B) rgb)
    {
        rgb = default;
        if (hex is not { Length: 7 } || hex[0] != '#' || !hex.Skip(1).All(Uri.IsHexDigit))
        {
            return false;
        }

        double Part(int i) => int.Parse(hex.AsSpan(i, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        rgb = (Part(1), Part(3), Part(5));
        return true;
    }

    private static void CheckRange(List<ThemeIssue> issues, string name, double value, (double Min, double Max) range)
    {
        if (!double.IsFinite(value) || value < range.Min || value > range.Max)
        {
            issues.Add(new ThemeIssue(null, $"{name} muss zwischen {range.Min.ToString(CultureInfo.InvariantCulture)} und {range.Max.ToString(CultureInfo.InvariantCulture)} liegen."));
        }
    }

    private static void CheckContrast(List<ThemeIssue> issues, Dictionary<string, (double R, double G, double B)> parsed,
        string foreground, string background, double minimum)
    {
        if (parsed.TryGetValue(foreground, out var fg) && parsed.TryGetValue(background, out var bg) && Ratio(fg, bg) < minimum)
        {
            issues.Add(new ThemeIssue(foreground,
                $"Zu geringer Kontrast zwischen „{foreground}“ und „{background}“ (mindestens {minimum.ToString(CultureInfo.InvariantCulture)}:1)."));
        }
    }

    private static double Ratio((double R, double G, double B) a, (double R, double G, double B) b)
    {
        var la = Luminance(a);
        var lb = Luminance(b);
        return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
    }

    private static double Luminance((double R, double G, double B) c)
    {
        static double Channel(double v)
        {
            var s = v / 255;
            return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
        }

        return 0.2126 * Channel(c.R) + 0.7152 * Channel(c.G) + 0.0722 * Channel(c.B);
    }
}
