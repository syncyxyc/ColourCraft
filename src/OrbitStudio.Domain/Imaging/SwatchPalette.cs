using System.Collections.Immutable;

namespace OrbitStudio.Domain.Imaging;

/// <summary>
/// Farbfelder (Palette): geordnete Liste gespeicherter Farben, aus der Nutzer Vorder- und Hintergrundfarbe wählen.
/// Unveränderlich; jede Änderung liefert eine neue Palette. Gleiche Farben (inkl. Alpha) kommen nur einmal vor.
/// </summary>
public sealed class SwatchPalette : IEquatable<SwatchPalette>
{
    public const int MaximumColors = 256;

    private readonly ImmutableArray<ColorRgba> _colors;

    private SwatchPalette(ImmutableArray<ColorRgba> colors) => _colors = colors;

    public static SwatchPalette Empty { get; } = new([]);

    /// <summary>Mitgelieferte Palette: Schwarz, Grautöne, Weiß und die Grund- und Zwischenfarben.</summary>
    public static SwatchPalette Default { get; } = FromColors(
    [
        new ColorRgba(0, 0, 0), new ColorRgba(64, 64, 64), new ColorRgba(128, 128, 128),
        new ColorRgba(192, 192, 192), new ColorRgba(255, 255, 255),
        new ColorRgba(255, 0, 0), new ColorRgba(255, 128, 0), new ColorRgba(255, 255, 0),
        new ColorRgba(128, 255, 0), new ColorRgba(0, 255, 0), new ColorRgba(0, 255, 128),
        new ColorRgba(0, 255, 255), new ColorRgba(0, 128, 255), new ColorRgba(0, 0, 255),
        new ColorRgba(128, 0, 255), new ColorRgba(255, 0, 255), new ColorRgba(255, 0, 128),
        new ColorRgba(128, 64, 0), new ColorRgba(255, 192, 203),
    ]);

    public IReadOnlyList<ColorRgba> Colors => _colors;

    public int Count => _colors.Length;

    public bool IsFull => _colors.Length >= MaximumColors;

    /// <summary>Palette aus Farben; Doppelte entfallen (die erste bleibt), der Rest über <see cref="MaximumColors"/> wird verworfen.</summary>
    public static SwatchPalette FromColors(IEnumerable<ColorRgba> colors) =>
        new(colors.Distinct().Take(MaximumColors).ToImmutableArray());

    /// <summary>
    /// Palette aus gespeicherten Hex-Texten („#RRGGBB" oder „#RRGGBBAA"). Ungültige Einträge werden übersprungen;
    /// <paramref name="skipped"/> zählt sie samt Doppelten und Überzähligen.
    /// </summary>
    public static SwatchPalette FromHex(IEnumerable<string?> hexValues, out int skipped)
    {
        var parsed = new List<ColorRgba>();
        var total = 0;
        foreach (var text in hexValues)
        {
            total++;
            if (ColorRgba.TryParseHex(text, out var color))
            {
                parsed.Add(color);
            }
        }

        var palette = FromColors(parsed);
        skipped = total - palette.Count;
        return palette;
    }

    public IReadOnlyList<string> ToHex() => _colors.Select(c => c.ToHex()).ToList();

    public bool Contains(ColorRgba color) => _colors.Contains(color);

    /// <summary>Hängt eine Farbe an. Ist sie schon vorhanden oder die Palette voll, bleibt die Palette unverändert.</summary>
    public SwatchPalette Add(ColorRgba color) =>
        IsFull || _colors.Contains(color) ? this : new SwatchPalette(_colors.Add(color));

    public SwatchPalette RemoveAt(int index)
    {
        CheckIndex(index);
        return new SwatchPalette(_colors.RemoveAt(index));
    }

    /// <summary>Ersetzt ein Farbfeld. Gibt es die neue Farbe schon an anderer Stelle, bleibt die Palette unverändert.</summary>
    public SwatchPalette Replace(int index, ColorRgba color)
    {
        CheckIndex(index);
        var existing = _colors.IndexOf(color);
        return existing >= 0 && existing != index ? this : new SwatchPalette(_colors.SetItem(index, color));
    }

    /// <summary>Verschiebt ein Farbfeld an eine neue Position (0 … Count − 1; wird begrenzt).</summary>
    public SwatchPalette Move(int from, int to)
    {
        CheckIndex(from);
        to = Math.Clamp(to, 0, _colors.Length - 1);
        if (from == to)
        {
            return this;
        }

        var color = _colors[from];
        return new SwatchPalette(_colors.RemoveAt(from).Insert(to, color));
    }

    /// <summary>Index des Farbfeldes mit dieser Farbe oder −1.</summary>
    public int IndexOf(ColorRgba color) => _colors.IndexOf(color);

    public bool Equals(SwatchPalette? other) => other is not null && _colors.AsSpan().SequenceEqual(other._colors.AsSpan());

    public override bool Equals(object? obj) => obj is SwatchPalette other && Equals(other);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var c in _colors)
        {
            hash.Add(c);
        }

        return hash.ToHashCode();
    }

    private void CheckIndex(int index)
    {
        if ((uint)index >= (uint)_colors.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }
    }
}
