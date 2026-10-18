using System.Windows.Input;

namespace OrbitStudio.Presentation.ViewModels;

/// <summary>Wandelt Registry-Tastenkürzel in WPF-Gesten und deutsche Anzeigetexte um.</summary>
public static class GestureDisplay
{
    private static readonly KeyGestureConverter Converter = new();

    public static KeyGesture? ToKeyGesture(string? gesture)
    {
        if (gesture is null)
        {
            return null;
        }

        return Converter.ConvertFromInvariantString(gesture) as KeyGesture;
    }

    /// <summary>Anzeige im Menü in deutscher Tastaturbeschriftung, z. B. "Strg+Umschalt+S".</summary>
    public static string? ToDisplayText(string? gesture)
    {
        if (gesture is null)
        {
            return null;
        }

        var parts = gesture.Split('+').Select(p => p switch
        {
            "Ctrl" => "Strg",
            "Shift" => "Umschalt",
            "OemPlus" => "+",
            "OemMinus" => "-",
            "Delete" => "Entf",
            "D0" => "0",
            "Up" => "Pfeil hoch",
            "Down" => "Pfeil runter",
            "PageUp" => "Bild auf",
            "PageDown" => "Bild ab",
            _ => p,
        });
        return string.Join('+', parts);
    }
}
