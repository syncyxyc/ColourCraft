using OrbitStudio.Domain.Imaging;

namespace OrbitStudio.Tools.Drawing;

/// <summary>Ein Strich des Musters: Abschnitt [StartDistance, EndDistance] entlang der Linie (Dokumentpixel).</summary>
public readonly record struct DashSegment(double StartDistance, double EndDistance, PointD Start, PointD End)
{
    public double Length => EndDistance - StartDistance;
}

/// <summary>
/// Geometrie einer Strichmusterlinie (Spezifikation 1.1), ohne Oberfläche testbar.
/// <list type="bullet">
/// <item>Periode = Strichlänge + Strichabstand. Strich i beginnt bei i · Periode und ist Strichlänge lang.</item>
/// <item>Das letzte Segment wird am Endpunkt gekürzt; ein Strich, der genau am Endpunkt beginnen würde, entfällt.</item>
/// <item>Abstand 0 ergibt ein einziges durchgehendes Segment (keine Nahtstellen zwischen Strichen).</item>
/// <item>Identische Punkte (Länge &lt; <see cref="MinLength"/>) ergeben kein Segment und keine Richtung.</item>
/// </list>
/// Segmente werden nicht gespeichert, sondern bei Bedarf berechnet: konstanter Speicher auch bei sehr vielen Strichen.
/// </summary>
public sealed class DashLine
{
    /// <summary>Kürzere Linien gelten als Punkt.</summary>
    public const double MinLength = 1e-9;

    /// <summary>Obergrenze der Segmentanzahl (Schutz vor Überlauf bei absurden Werten).</summary>
    public const long MaxSegments = 1_000_000_000_000;

    public DashLine(PointD start, PointD end, double dashLength, double gapLength)
    {
        if (!double.IsFinite(start.X) || !double.IsFinite(start.Y) || !double.IsFinite(end.X) || !double.IsFinite(end.Y))
        {
            throw new ArgumentException("Die Koordinaten müssen endliche Zahlen sein.");
        }

        if (!double.IsFinite(dashLength) || dashLength <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(dashLength), dashLength, "Die Strichlänge muss größer als 0 sein.");
        }

        if (!double.IsFinite(gapLength) || gapLength < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(gapLength), gapLength, "Der Strichabstand darf nicht negativ sein.");
        }

        Start = start;
        End = end;
        DashLength = dashLength;
        GapLength = gapLength;

        var dx = end.X - start.X;
        var dy = end.Y - start.Y;
        var scale = Math.Max(Math.Abs(dx), Math.Abs(dy));
        Length = scale == 0 ? 0 : scale * Math.Sqrt((dx / scale) * (dx / scale) + (dy / scale) * (dy / scale));
        if (!double.IsFinite(Length))
        {
            throw new ArgumentException("Die Linie ist zu lang.");
        }

        if (Length < MinLength)
        {
            Length = 0;
            return;
        }

        Direction = new PointD(dx / Length, dy / Length);

        // Bildschirmkoordinaten (y nach unten): 0° = nach rechts, 90° = nach oben, gegen den Uhrzeigersinn.
        var angle = Math.Atan2(-dy, dx) * 180 / Math.PI;
        if (angle < 0)
        {
            angle += 360;
        }

        AngleDegrees = angle >= 360 || angle == 0 ? 0 : angle;
        Count = CountSegments();
    }

    public PointD Start { get; }

    public PointD End { get; }

    public double DashLength { get; }

    public double GapLength { get; }

    public double Period => DashLength + GapLength;

    /// <summary>Gesamtlänge der Linie.</summary>
    public double Length { get; }

    /// <summary>Normierter Richtungsvektor vom Start- zum Endpunkt; (0, 0) bei identischen Punkten.</summary>
    public PointD Direction { get; }

    /// <summary>Winkel in Grad, 0 ≤ Winkel &lt; 360; 0° = rechts, 90° = oben (gegen den Uhrzeigersinn).</summary>
    public double AngleDegrees { get; }

    public PointD Midpoint => new((Start.X + End.X) / 2, (Start.Y + End.Y) / 2);

    /// <summary>Anzahl der Striche.</summary>
    public long Count { get; }

    /// <summary>True, wenn Start und Ende zusammenfallen.</summary>
    public bool IsPoint => Length == 0;

    /// <summary>Punkt im Abstand <paramref name="distance"/> vom Start auf der Linie; am Ende exakt der Endpunkt.</summary>
    public PointD PointAt(double distance) =>
        distance >= Length ? End : new PointD(Start.X + Direction.X * distance, Start.Y + Direction.Y * distance);

    public DashSegment SegmentAt(long index)
    {
        if (index < 0 || index >= Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index), index, "Kein Strich mit diesem Index.");
        }

        if (GapLength == 0)
        {
            return new DashSegment(0, Length, Start, End);
        }

        var from = index * Period;
        var to = Math.Min(from + DashLength, Length);
        return new DashSegment(from, to, PointAt(from), PointAt(to));
    }

    /// <summary>Alle Striche in Reihenfolge (werden beim Aufzählen berechnet).</summary>
    public IEnumerable<DashSegment> Segments
    {
        get
        {
            for (long i = 0; i < Count; i++)
            {
                yield return SegmentAt(i);
            }
        }
    }

    private long CountSegments()
    {
        if (GapLength == 0 || DashLength >= Length)
        {
            return 1;
        }

        var ratio = Length / Period;
        if (ratio > MaxSegments)
        {
            throw new ArgumentOutOfRangeException(nameof(Length), Length, "Zu viele Striche für diese Linie.");
        }

        // Strich i existiert, solange er vor dem Endpunkt beginnt: i · Periode < Länge (gleiche Rechnung wie SegmentAt).
        var count = (long)Math.Ceiling(ratio);
        while (count > 1 && (count - 1) * Period >= Length)
        {
            count--;
        }

        while (count * Period < Length)
        {
            count++;
        }

        return count;
    }
}
