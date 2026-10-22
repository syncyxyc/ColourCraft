using System.Collections.Immutable;
using OrbitStudio.Domain.Imaging;

namespace OrbitStudio.Effects;

/// <summary>Kategorie eines Effekts (bestimmt die Gruppierung in Menüs und im Effektbrowser).</summary>
public enum EffectCategory
{
    /// <summary>Farbanpassung pro Pixel (Menü „Farben").</summary>
    Adjustment,
    Blur,
    Sharpen,
    Noise,
    Stylize,
    Distort,
}

// --- Parameter ---------------------------------------------------------------------------------

/// <summary>Beschreibung eines Effektparameters. Beschriftungen sind Textschlüssel, die Oberfläche übersetzt sie.</summary>
public abstract record EffectParameter(string Id, string LabelKey)
{
    /// <summary>Standardwert (double, bool oder <see cref="ColorRgba"/>).</summary>
    public abstract object DefaultValue { get; }

    /// <summary>Prüft einen Wert; liefert null oder den Textschlüssel der Fehlermeldung.</summary>
    public abstract string? Validate(object? value);
}

/// <summary>
/// Zahl mit Wertebereich. <paramref name="Spatial"/> = Wert ist eine Länge in Bildpixeln (Radius, Versatz, Zellgröße);
/// für Vorschauen in reduzierter Auflösung wird er mit dem Maßstab skaliert.
/// </summary>
public sealed record NumberParameter(
    string Id,
    string LabelKey,
    double Minimum,
    double Maximum,
    double Default,
    double Step = 1,
    int Decimals = 0,
    string UnitKey = "",
    bool Spatial = false) : EffectParameter(Id, LabelKey)
{
    public override object DefaultValue => Default;

    /// <summary>Begrenzt auf den Wertebereich und rundet auf die erlaubten Nachkommastellen.</summary>
    public double Clamp(double value) =>
        double.IsFinite(value) ? Math.Round(Math.Clamp(value, Minimum, Maximum), Decimals, MidpointRounding.AwayFromZero) : Default;

    public override string? Validate(object? value) =>
        value is double d && double.IsFinite(d) && d >= Minimum && d <= Maximum ? null : "Effect.Error.Range";
}

public sealed record BoolParameter(string Id, string LabelKey, bool Default) : EffectParameter(Id, LabelKey)
{
    public override object DefaultValue => Default;

    public override string? Validate(object? value) => value is bool ? null : "Effect.Error.Type";
}

public sealed record ColorParameter(string Id, string LabelKey, ColorRgba Default) : EffectParameter(Id, LabelKey)
{
    public override object DefaultValue => Default;

    public override string? Validate(object? value) => value is ColorRgba ? null : "Effect.Error.Type";
}

/// <summary>Fehler einer Validierung: Parameter (oder null für Regeln über mehrere Parameter) und Textschlüssel.</summary>
public sealed record EffectValidationError(string? ParameterId, string MessageKey);

/// <summary>Unveränderliche Parameterwerte eines Effekts.</summary>
public sealed class EffectSettings
{
    private readonly ImmutableDictionary<string, object> _values;

    private EffectSettings(ImmutableDictionary<string, object> values) => _values = values;

    public IReadOnlyDictionary<string, object> Values => _values;

    /// <summary>Standardwerte aller Parameter des Effekts.</summary>
    public static EffectSettings Defaults(IEffect effect) =>
        new(effect.Parameters.ToImmutableDictionary(p => p.Id, p => p.DefaultValue, StringComparer.Ordinal));

    /// <summary>Neuer Wert für einen Parameter. Zahlen werden auf den Bereich des Parameters begrenzt.</summary>
    public EffectSettings With(IEffect effect, string id, object value)
    {
        var parameter = effect.Parameters.FirstOrDefault(p => p.Id == id)
                        ?? throw new ArgumentException($"Unbekannter Parameter '{id}'.", nameof(id));
        if (parameter is NumberParameter number && value is double d)
        {
            value = number.Clamp(d);
        }

        return new EffectSettings(_values.SetItem(id, value));
    }

    /// <summary>Ungeprüftes Setzen (für Tests der Validierung).</summary>
    public EffectSettings WithRaw(string id, object value) => new(_values.SetItem(id, value));

    public double Number(string id) => (double)_values[id];

    public int Integer(string id) => (int)Math.Round(Number(id), MidpointRounding.AwayFromZero);

    public bool Bool(string id) => (bool)_values[id];

    public ColorRgba Color(string id) => (ColorRgba)_values[id];

    public ToneCurve Curve(string id) => (ToneCurve)_values[id];

    /// <summary>
    /// Kopie für eine Vorschau im Maßstab <paramref name="scale"/>: Längen (Spatial) werden skaliert, aber nicht unter
    /// das Minimum des Parameters (ganzzahlige Parameter werden gerundet).
    /// </summary>
    public EffectSettings Scaled(IEffect effect, double scale)
    {
        if (scale == 1)
        {
            return this;
        }

        var values = _values;
        foreach (var p in effect.Parameters.OfType<NumberParameter>().Where(p => p.Spatial))
        {
            var original = Number(p.Id);
            var scaled = original * scale;
            if (p.Decimals == 0)
            {
                scaled = Math.Round(scaled, MidpointRounding.AwayFromZero);
            }

            // Ein von 0 verschiedener Wert bleibt mindestens 1 Pixel groß, damit die Vorschau die Wirkung zeigt.
            if (original != 0 && Math.Abs(scaled) < 1)
            {
                scaled = Math.Sign(original);
            }

            values = values.SetItem(p.Id, Math.Clamp(scaled, p.Minimum, p.Maximum));
        }

        return new EffectSettings(values);
    }

    public override bool Equals(object? obj) =>
        obj is EffectSettings other && other._values.Count == _values.Count
        && _values.All(kv => other._values.TryGetValue(kv.Key, out var v) && Equals(v, kv.Value));

    public override int GetHashCode() => _values.Count;
}

// --- Effekt ------------------------------------------------------------------------------------

/// <summary>Alles, was ein Effekt zum Berechnen braucht.</summary>
public sealed class EffectContext(PixelBuffer source, IntRect region, PixelBuffer output, EffectSettings settings,
    CancellationToken cancellation)
{
    /// <summary>Eingabe (vormultipliertes BGRA, nur lesen). Nachbarschaftseffekte lesen auch außerhalb von <see cref="Region"/>.</summary>
    public PixelBuffer Source { get; } = source;

    /// <summary>Zu berechnender Bereich in Koordinaten von <see cref="Source"/>.</summary>
    public IntRect Region { get; } = region;

    /// <summary>Ergebnis in der Größe von <see cref="Region"/>: Pixel (x, y) der Quelle → (x − Region.X, y − Region.Y).</summary>
    public PixelBuffer Output { get; } = output;

    public EffectSettings Settings { get; } = settings;

    public CancellationToken Cancellation { get; } = cancellation;

    public ParallelOptions ParallelOptions => new() { CancellationToken = Cancellation };
}

/// <summary>
/// Registrierbarer Effekt (Spezifikation 9): ID, Name, Kategorie, Beschreibung, Parameter mit Standardwerten und
/// Wertebereichen, Validierung, Berechnung. Vorschau, Abbruch und Fehlerbehandlung übernimmt die <see cref="EffectEngine"/>.
/// </summary>
public interface IEffect
{
    string Id { get; }

    /// <summary>Textschlüssel „Effect.&lt;Id&gt;".</summary>
    string NameKey { get; }

    /// <summary>Textschlüssel „Effect.&lt;Id&gt;.Description".</summary>
    string DescriptionKey { get; }

    EffectCategory Category { get; }

    IReadOnlyList<EffectParameter> Parameters { get; }

    /// <summary>Regeln über mehrere Parameter (z. B. Schwarzpunkt &lt; Weißpunkt). Einzelwerte prüft die Engine.</summary>
    IEnumerable<EffectValidationError> ValidateRules(EffectSettings settings);

    /// <summary>Berechnet <see cref="EffectContext.Region"/> in <see cref="EffectContext.Output"/>. Prüft regelmäßig den Abbruch.</summary>
    void Render(EffectContext context);
}

public abstract class EffectBase : IEffect
{
    public abstract string Id { get; }

    public string NameKey => "Effect." + Id;

    public string DescriptionKey => "Effect." + Id + ".Description";

    public abstract EffectCategory Category { get; }

    public virtual IReadOnlyList<EffectParameter> Parameters => [];

    public virtual IEnumerable<EffectValidationError> ValidateRules(EffectSettings settings) => [];

    public abstract void Render(EffectContext context);
}

/// <summary>Alle verfügbaren Effekte (Standardsatz; Plugins können ab M10 weitere registrieren).</summary>
public sealed class EffectRegistry
{
    private readonly Dictionary<string, IEffect> _effects = new(StringComparer.Ordinal);
    private readonly List<IEffect> _ordered = [];

    public IReadOnlyList<IEffect> All => _ordered;

    public void Register(IEffect effect)
    {
        if (!_effects.TryAdd(effect.Id, effect))
        {
            throw new InvalidOperationException($"Effekt '{effect.Id}' ist bereits registriert.");
        }

        var ids = effect.Parameters.Select(p => p.Id).ToList();
        if (ids.Distinct(StringComparer.Ordinal).Count() != ids.Count)
        {
            throw new InvalidOperationException($"Effekt '{effect.Id}' hat doppelte Parameter-IDs.");
        }

        _ordered.Add(effect);
    }

    public IEffect Get(string id) =>
        _effects.TryGetValue(id, out var effect) ? effect : throw new KeyNotFoundException($"Effekt '{id}' fehlt.");

    public IEffect? Find(string id) => _effects.GetValueOrDefault(id);

    public static EffectRegistry CreateDefault()
    {
        var registry = new EffectRegistry();
        foreach (var effect in StandardEffects.Create())
        {
            registry.Register(effect);
        }

        return registry;
    }
}
