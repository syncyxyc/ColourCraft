using System.Globalization;
using OrbitStudio.Domain.Layers;
using OrbitStudio.Presentation.Localization;

namespace OrbitStudio.Presentation.Services;

/// <summary>Werte des Dialogs „Ebeneneigenschaften".</summary>
public sealed record LayerProperties(string Name, bool IsVisible, bool IsLocked, double OpacityPercent, BlendMode BlendMode);

/// <summary>Welches Feld beim Öffnen des Dialogs den Fokus bekommt.</summary>
public enum LayerPropertyFocus
{
    Name,
    Opacity,
    BlendMode,
}

/// <summary>Prüft die Eingaben des Dialogs „Ebeneneigenschaften".</summary>
public static class LayerPropertiesValidator
{
    public static (LayerProperties? Result, string? Error) Validate(string? name, string? opacityText, bool visible,
        bool locked, BlendMode mode)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return (null, Loc.Get("Dlg.Layer.Error.Name"));
        }

        var text = opacityText?.Trim().TrimEnd('%').Trim();
        if (!double.TryParse(text, NumberStyles.Float, Loc.Culture, out var opacity)
            || !double.IsFinite(opacity) || opacity < 0 || opacity > 100)
        {
            return (null, Loc.Get("Dlg.Layer.Error.Opacity"));
        }

        return (new LayerProperties(name.Trim(), visible, locked, opacity, mode), null);
    }
}
