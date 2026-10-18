namespace OrbitStudio.Application.Commands;

/// <summary>
/// Normalisiert Tastenkürzel im Textformat ("ctrl+shift+s" → "Ctrl+Shift+S"), damit Konflikte
/// unabhängig von Schreibweise und Reihenfolge der Modifizierer erkannt werden. WPF-frei.
/// </summary>
public static class KeyGestureText
{
    private static readonly string[] ModifierOrder = ["Ctrl", "Shift", "Alt", "Win"];

    public static string? Normalize(string? gesture)
    {
        if (string.IsNullOrWhiteSpace(gesture))
        {
            return null;
        }

        var parts = gesture.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return null;
        }

        var modifiers = new HashSet<string>(StringComparer.Ordinal);
        foreach (var part in parts[..^1])
        {
            var modifier = NormalizeModifier(part)
                           ?? throw new FormatException($"Unbekannter Modifizierer '{part}' in '{gesture}'.");
            modifiers.Add(modifier);
        }

        var key = parts[^1];
        if (NormalizeModifier(key) is not null)
        {
            throw new FormatException($"Tastenkürzel '{gesture}' enthält keine Haupttaste.");
        }

        key = key.Length == 1 ? key.ToUpperInvariant() : char.ToUpperInvariant(key[0]) + key[1..];
        var ordered = ModifierOrder.Where(modifiers.Contains).Append(key);
        return string.Join('+', ordered);
    }

    private static string? NormalizeModifier(string part) => part.ToLowerInvariant() switch
    {
        "ctrl" or "control" or "strg" => "Ctrl",
        "shift" or "umschalt" => "Shift",
        "alt" => "Alt",
        "win" or "windows" => "Win",
        _ => null,
    };
}
