using System.Collections.Immutable;
using OrbitStudio.Domain.Imaging;

namespace OrbitStudio.Rendering;

public enum GuideOrientation
{
    /// <summary>Waagerechte Linie bei y = Position.</summary>
    Horizontal,

    /// <summary>Senkrechte Linie bei x = Position.</summary>
    Vertical,
}

/// <summary>Hilfslinie in Dokumentkoordinaten (Bildpixel, Unterpixel erlaubt).</summary>
public readonly record struct Guide(GuideOrientation Orientation, double Position);

/// <summary>Einrasten: Ergebnis-Punkt und ob je Achse eingerastet wurde.</summary>
public readonly record struct SnapResult(PointD Point, bool SnappedX, bool SnappedY);

/// <summary>
/// Hilfslinien eines Dokuments (reine Überlagerung, nie in Pixeln). Unveränderlich; ungültige Positionen (NaN, ∞) werden
/// abgewiesen, es gibt höchstens <see cref="MaximumGuides"/> Linien, gleiche Linien nur einmal.
/// </summary>
public sealed class GuideSet : IEquatable<GuideSet>
{
    public const int MaximumGuides = 100;

    private readonly ImmutableArray<Guide> _guides;

    private GuideSet(ImmutableArray<Guide> guides) => _guides = guides;

    public static GuideSet Empty { get; } = new([]);

    public IReadOnlyList<Guide> Guides => _guides;

    public GuideSet Add(GuideOrientation orientation, double position)
    {
        var guide = new Guide(orientation, Check(position));
        return _guides.Contains(guide) || _guides.Length >= MaximumGuides ? this : new GuideSet(_guides.Add(guide));
    }

    /// <summary>Verschiebt eine Linie; liegt dort schon eine gleiche, entfällt die verschobene.</summary>
    public GuideSet Move(int index, double position)
    {
        CheckIndex(index);
        var moved = _guides[index] with { Position = Check(position) };
        var without = _guides.RemoveAt(index);
        return new GuideSet(without.Contains(moved) ? without : without.Insert(index, moved));
    }

    public GuideSet RemoveAt(int index)
    {
        CheckIndex(index);
        return new GuideSet(_guides.RemoveAt(index));
    }

    public GuideSet Clear() => _guides.IsEmpty ? this : Empty;

    /// <summary>Index der Linie, die dem Punkt (Dokumentkoordinaten) am nächsten liegt und höchstens <paramref name="tolerance"/> entfernt ist; sonst −1.</summary>
    public int HitTest(PointD point, double tolerance)
    {
        var best = -1;
        var bestDistance = tolerance;
        for (var i = 0; i < _guides.Length; i++)
        {
            var distance = Math.Abs(_guides[i].Position - (_guides[i].Orientation == GuideOrientation.Vertical ? point.X : point.Y));
            if (distance <= bestDistance)
            {
                best = i;
                bestDistance = distance;
            }
        }

        return best;
    }

    /// <summary>Rastet einen Punkt je Achse auf die nächste Hilfslinie innerhalb von <paramref name="tolerance"/> ein.</summary>
    public SnapResult Snap(PointD point, double tolerance)
    {
        var (x, snappedX) = SnapAxis(point.X, GuideOrientation.Vertical, tolerance);
        var (y, snappedY) = SnapAxis(point.Y, GuideOrientation.Horizontal, tolerance);
        return new SnapResult(new PointD(x, y), snappedX, snappedY);
    }

    private (double Value, bool Snapped) SnapAxis(double value, GuideOrientation orientation, double tolerance)
    {
        var best = value;
        var bestDistance = tolerance;
        var snapped = false;
        foreach (var g in _guides.Where(g => g.Orientation == orientation))
        {
            var distance = Math.Abs(g.Position - value);
            if (distance <= bestDistance)
            {
                best = g.Position;
                bestDistance = distance;
                snapped = true;
            }
        }

        return (best, snapped);
    }

    private static double Check(double position) =>
        double.IsFinite(position) ? position : throw new ArgumentOutOfRangeException(nameof(position), "Position muss eine endliche Zahl sein.");

    private void CheckIndex(int index)
    {
        if ((uint)index >= (uint)_guides.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }
    }

    public bool Equals(GuideSet? other) => other is not null && _guides.AsSpan().SequenceEqual(other._guides.AsSpan());

    public override bool Equals(object? obj) => obj is GuideSet other && Equals(other);

    public override int GetHashCode() => _guides.Length;
}

/// <summary>Raster in Bildpixeln: Abstand ≥ 1, Ursprung (0, 0).</summary>
public readonly record struct GridSettings(int Spacing, bool SnapEnabled)
{
    public const int MinSpacing = 1;
    public const int MaxSpacing = 10_000;

    public static GridSettings Default { get; } = new(16, false);

    public GridSettings Normalized() => this with { Spacing = Math.Clamp(Spacing, MinSpacing, MaxSpacing) };

    /// <summary>Nächster Rasterpunkt (kaufmännisch gerundet, bei Mitte zum größeren Wert). Unendlich/NaN bleiben unverändert.</summary>
    public PointD Snap(PointD point)
    {
        var s = Math.Clamp(Spacing, MinSpacing, MaxSpacing);
        double Axis(double v) => double.IsFinite(v) ? Math.Floor(v / s + 0.5) * s : v;
        return new PointD(Axis(point.X), Axis(point.Y));
    }
}

/// <summary>Teilstrich eines Lineals.</summary>
public readonly record struct RulerTick(double Position, bool Major, string? Label);

/// <summary>
/// Teilung der Lineale: Der Hauptstrich-Abstand ist 1, 2 oder 5 mal einer Zehnerpotenz (in Bildpixeln) und so klein, dass
/// er auf dem Bildschirm mindestens <see cref="MinMajorSpacingDevicePx"/> Gerätepixel misst; dazwischen liegen je 5 Teilstriche
/// (also mindestens 12 Gerätepixel auseinander).
/// </summary>
public static class RulerScale
{
    public const double MinMajorSpacingDevicePx = 60;

    /// <summary>Hauptstrich-Abstand in Bildpixeln für einen Zoom (Gerätepixel je Bildpixel).</summary>
    public static double MajorStep(double zoom)
    {
        if (!double.IsFinite(zoom) || zoom <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(zoom));
        }

        var minimum = MinMajorSpacingDevicePx / zoom;
        var magnitude = Math.Pow(10, Math.Floor(Math.Log10(minimum)));
        foreach (var factor in new[] { 1.0, 2.0, 5.0, 10.0 })
        {
            var step = factor * magnitude;
            if (step >= minimum * (1 - 1e-12))
            {
                return step;
            }
        }

        return 10 * magnitude;
    }

    /// <summary>Teilstriche im sichtbaren Bereich [<paramref name="from"/>, <paramref name="to"/>] (Bildpixel), aufsteigend.</summary>
    public static IReadOnlyList<RulerTick> Ticks(double zoom, double from, double to)
    {
        if (!double.IsFinite(from) || !double.IsFinite(to) || to < from)
        {
            return [];
        }

        var major = MajorStep(zoom);
        var step = major / 5;

        var first = (long)Math.Ceiling(from / step - 1e-9);
        var last = (long)Math.Floor(to / step + 1e-9);
        if (last - first > 20_000)
        {
            return [];
        }

        var ticks = new List<RulerTick>((int)(last - first + 1));
        for (var i = first; i <= last; i++)
        {
            var isMajor = i % 5 == 0;
            var position = Math.Round(i * step, 9);
            ticks.Add(new RulerTick(position, isMajor, isMajor ? FormatLabel(position) : null));
        }

        return ticks;
    }

    private static string FormatLabel(double position) =>
        position.ToString(position == Math.Floor(position) ? "0" : "0.####", System.Globalization.CultureInfo.InvariantCulture);
}
