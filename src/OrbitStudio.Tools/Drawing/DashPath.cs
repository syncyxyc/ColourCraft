using OrbitStudio.Domain.Imaging;

namespace OrbitStudio.Tools.Drawing;

/// <summary>Gerades Teilstück einer Linie; <see cref="ArcStart"/> ist seine Position entlang der gesamten Linie.</summary>
public readonly record struct PathPiece(PointD A, PointD B, double ArcStart, double Length, PointD Direction);

/// <summary>
/// Strichmusterlinie durch mehrere Kontrollpunkte. Zwei Punkte ergeben eine gerade Linie. Ab drei Punkten verläuft die
/// Linie als uniformer Catmull-Rom-Spline durch alle Kontrollpunkte (Endpunkte werden dafür verdoppelt). Für Geometrie
/// und Rasterung wird sie in gerade Stücke von höchstens <see cref="SampleStep"/> px zerlegt.
/// Strichlängen und Abstände werden entlang der Bogenlänge gemessen: Periode = Strichlänge + Strichabstand, Strich i
/// liegt bei [i · Periode, i · Periode + Strichlänge], der letzte am Ende gekürzt (wie bei <see cref="DashLine"/>).
/// </summary>
public sealed class DashPath
{
    /// <summary>Höchstzahl der Kontrollpunkte einer Linie (Anfangs- und Endpunkt eingeschlossen).</summary>
    public const int MaxControlPoints = 16;

    /// <summary>Längste Strecke eines geraden Stücks beim Zerlegen einer Biegung (Dokumentpixel).</summary>
    public const double SampleStep = 0.5;

    /// <summary>Obergrenze der Stücke einer Biegung (Schutz vor Speicherüberlauf bei sehr langen Linien).</summary>
    private const double MaxPieces = 1_000_000;

    private readonly PathPiece[] _pieces;

    public DashPath(IReadOnlyList<PointD> controls, double dashLength, double gapLength)
    {
        ArgumentNullException.ThrowIfNull(controls);
        if (controls.Count < 2 || controls.Count > MaxControlPoints)
        {
            throw new ArgumentException($"Eine Linie braucht 2 bis {MaxControlPoints} Punkte.", nameof(controls));
        }

        foreach (var p in controls)
        {
            if (!double.IsFinite(p.X) || !double.IsFinite(p.Y))
            {
                throw new ArgumentException("Die Koordinaten müssen endliche Zahlen sein.", nameof(controls));
            }
        }

        if (!double.IsFinite(dashLength) || dashLength <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(dashLength), dashLength, "Die Strichlänge muss größer als 0 sein.");
        }

        if (!double.IsFinite(gapLength) || gapLength < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(gapLength), gapLength, "Der Strichabstand darf nicht negativ sein.");
        }

        Controls = controls.ToArray();
        DashLength = dashLength;
        GapLength = gapLength;
        _pieces = Flatten(controls.ToArray());
        Length = _pieces.Length == 0 ? 0 : _pieces[^1].ArcStart + _pieces[^1].Length;
        Count = CountDashes();
    }

    /// <summary>Kontrollpunkte in Reihenfolge.</summary>
    public IReadOnlyList<PointD> Controls { get; }

    public double DashLength { get; }

    public double GapLength { get; }

    public double Period => DashLength + GapLength;

    /// <summary>Gesamte Bogenlänge.</summary>
    public double Length { get; }

    /// <summary>Anzahl der Striche.</summary>
    public long Count { get; }

    /// <summary>Gerade Stücke in Reihenfolge (leer, wenn alle Punkte zusammenfallen).</summary>
    public IReadOnlyList<PathPiece> Pieces => _pieces;

    /// <summary>Anfangs- und Endpunkt eines Strichs in Bogenlängen-Koordinaten (ohne Lücke: die ganze Linie).</summary>
    public (double From, double To) Interval(long index)
    {
        if (GapLength == 0)
        {
            return (0, Length);
        }

        var from = index * Period;
        return (from, Math.Min(from + DashLength, Length));
    }

    /// <summary>Punkt im Abstand <paramref name="arc"/> vom Anfang entlang der Linie (am Ende der Endpunkt).</summary>
    public PointD PointAt(double arc)
    {
        if (_pieces.Length == 0)
        {
            return Controls[0];
        }

        if (arc <= 0)
        {
            return _pieces[0].A;
        }

        if (arc >= Length)
        {
            return _pieces[^1].B;
        }

        var lo = 0;
        var hi = _pieces.Length - 1;
        while (lo < hi)
        {
            var mid = (lo + hi + 1) / 2;
            if (_pieces[mid].ArcStart <= arc)
            {
                lo = mid;
            }
            else
            {
                hi = mid - 1;
            }
        }

        var piece = _pieces[lo];
        var d = Math.Min(arc - piece.ArcStart, piece.Length);
        return new PointD(piece.A.X + piece.Direction.X * d, piece.A.Y + piece.Direction.Y * d);
    }

    /// <summary>Umschließendes Pixelrechteck der Linie, um <paramref name="margin"/> erweitert.</summary>
    public IntRect Bounds(double margin)
    {
        var points = _pieces.Length == 0 ? Controls : _pieces.Select(p => p.A).Append(_pieces[^1].B).ToList();
        var minX = points.Min(p => p.X) - margin;
        var minY = points.Min(p => p.Y) - margin;
        var maxX = points.Max(p => p.X) + margin;
        var maxY = points.Max(p => p.Y) + margin;
        var left = (int)Math.Floor(minX);
        var top = (int)Math.Floor(minY);
        return new IntRect(left, top, (int)Math.Ceiling(maxX) - left + 1, (int)Math.Ceiling(maxY) - top + 1);
    }

    private static PathPiece[] Flatten(PointD[] c)
    {
        var pieces = new List<PathPiece>();
        if (c.Length == 2)
        {
            AddPiece(pieces, c[0], c[1], 0);
            return pieces.ToArray();
        }

        // Schrittweite so wählen, dass die Stückzahl beherrschbar bleibt.
        var estimate = 0.0;
        for (var i = 0; i < c.Length - 1; i++)
        {
            estimate += Distance(c[i], c[i + 1]);
        }

        var step = Math.Max(SampleStep, estimate / MaxPieces);
        double arc = 0;
        for (var i = 0; i < c.Length - 1; i++)
        {
            var p0 = c[Math.Max(i - 1, 0)];
            var p1 = c[i];
            var p2 = c[i + 1];
            var p3 = c[Math.Min(i + 2, c.Length - 1)];
            var steps = Math.Max(1, (int)Math.Ceiling(Distance(p1, p2) / step));
            var previous = p1;
            for (var k = 1; k <= steps; k++)
            {
                var point = k == steps ? p2 : Spline(p0, p1, p2, p3, (double)k / steps);
                arc = AddPiece(pieces, previous, point, arc);
                previous = point;
            }
        }

        return pieces.ToArray();
    }

    /// <summary>Uniformer Catmull-Rom-Abschnitt zwischen p1 und p2 bei Parameter u ∈ [0, 1].</summary>
    private static PointD Spline(PointD p0, PointD p1, PointD p2, PointD p3, double u)
    {
        var u2 = u * u;
        var u3 = u2 * u;
        double Axis(double a, double b, double c, double d) =>
            0.5 * (2 * b + (-a + c) * u + (2 * a - 5 * b + 4 * c - d) * u2 + (-a + 3 * b - 3 * c + d) * u3);
        return new PointD(Axis(p0.X, p1.X, p2.X, p3.X), Axis(p0.Y, p1.Y, p2.Y, p3.Y));
    }

    private static double AddPiece(List<PathPiece> pieces, PointD a, PointD b, double arc)
    {
        var length = Distance(a, b);
        if (length < DashLine.MinLength)
        {
            return arc;
        }

        var direction = new PointD((b.X - a.X) / length, (b.Y - a.Y) / length);
        pieces.Add(new PathPiece(a, b, arc, length, direction));
        return arc + length;
    }

    private static double Distance(PointD a, PointD b) => (b - a).Length;

    private long CountDashes()
    {
        if (Length <= DashLine.MinLength)
        {
            return 0;
        }

        if (GapLength == 0 || DashLength >= Length)
        {
            return 1;
        }

        var ratio = Length / Period;
        if (ratio > DashLine.MaxSegments)
        {
            throw new ArgumentOutOfRangeException(nameof(Length), Length, "Zu viele Striche für diese Linie.");
        }

        // Gleiche Rechnung wie DashLine: Strich i existiert, solange er vor dem Ende beginnt.
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

/// <summary>
/// Rasterung einer <see cref="DashPath"/>. Jedes gerade Stück wird im Band um seine Strecke ausgewertet; je Zelle zählt der
/// kleinste signierte Abstand zu einem Strich (Vereinigung der Striche, flache Enden). Abdeckung wie bei
/// <see cref="DashRasterizer"/>: Kantenglättung über eine Zelle.
/// </summary>
public static class DashPathRasterizer
{
    /// <summary>Anzahl der Rasterzellen für eine Ausdehnung in Dokumentpixeln.</summary>
    public static int GridSize(int extent, double scale) => Math.Max(1, (int)Math.Ceiling(extent * scale - 1e-9));

    /// <summary>
    /// Ruft <paramref name="sink"/>(i, j, Abdeckung) für jede Zelle mit Abdeckung &gt; 0 im Raster über
    /// <paramref name="region"/> auf. Zelle (i, j) liegt bei (region.X + (i + 0,5) / scale; region.Y + (j + 0,5) / scale).
    /// </summary>
    public static void Rasterize(DashPath path, double thickness, bool antialias, IntRect region, double scale,
        Action<int, int, double> sink)
    {
        if (path.Count == 0 || !(thickness > 0) || region.IsEmpty || !(scale > 0))
        {
            return;
        }

        var columns = GridSize(region.Width, scale);
        var rows = GridSize(region.Height, scale);
        var distance = new float[columns * rows];
        Array.Fill(distance, float.PositiveInfinity);
        var half = thickness / 2;
        var margin = antialias ? 0.5 / scale : 0;
        foreach (var piece in path.Pieces)
        {
            MarkPiece(path, piece, half, margin, region, scale, columns, rows, distance);
        }

        for (var j = 0; j < rows; j++)
        {
            for (var i = 0; i < columns; i++)
            {
                var sd = distance[j * columns + i];
                if (float.IsPositiveInfinity(sd))
                {
                    continue;
                }

                var coverage = antialias ? Math.Clamp(0.5 - sd * scale, 0, 1) : sd <= 0 ? 1 : 0;
                if (coverage > 0)
                {
                    sink(i, j, coverage);
                }
            }
        }
    }

    private static void MarkPiece(DashPath path, PathPiece piece, double half, double margin, IntRect region, double scale,
        int columns, int rows, float[] distance)
    {
        var reach = half + margin;
        var minY = Math.Min(piece.A.Y, piece.B.Y) - reach;
        var maxY = Math.Max(piece.A.Y, piece.B.Y) + reach;
        var first = (int)Math.Max(0, Math.Floor((minY - region.Y) * scale) - 1);
        var last = (int)Math.Min(rows - 1, Math.Ceiling((maxY - region.Y) * scale) + 1);
        var dir = piece.Direction;
        var nx = -dir.Y;
        var ny = dir.X;
        for (var j = first; j <= last; j++)
        {
            var y = region.Y + (j + 0.5) / scale;
            var dy = y - piece.A.Y;

            // Abstand zur Linie (senkrecht) und Lage entlang der Linie sind lineare Funktionen von x.
            var (perpLo, perpHi) = Solve(nx, ny * dy - nx * piece.A.X, -reach, reach);
            var (alongLo, alongHi) = Solve(dir.X, dir.Y * dy - dir.X * piece.A.X, -margin, piece.Length + margin);
            var lo = Math.Max(perpLo, alongLo);
            var hi = Math.Min(perpHi, alongHi);
            if (lo > hi)
            {
                continue;
            }

            var from = (int)Math.Clamp(Math.Floor((lo - region.X) * scale - 0.5) - 1, 0, columns);
            var to = (int)Math.Clamp(Math.Ceiling((hi - region.X) * scale - 0.5) + 1, -1, columns - 1);
            for (var i = from; i <= to; i++)
            {
                var x = region.X + (i + 0.5) / scale;
                var dx = x - piece.A.X;
                var along = dir.X * dx + dir.Y * dy;
                var perpendicular = nx * dx + ny * dy;
                var sd = DashSignedDistance(path, piece.ArcStart + along, Math.Abs(perpendicular), half);
                var index = j * columns + i;
                if (sd < distance[index])
                {
                    distance[index] = (float)sd;
                }
            }
        }
    }

    /// <summary>Signierter Abstand (Dokumentpixel) zum nächstgelegenen Strich bei Bogenlänge t und Abstand d zur Linie.</summary>
    private static double DashSignedDistance(DashPath path, double t, double absDistance, double half)
    {
        var index = path.GapLength == 0 ? 0 : (long)Math.Floor(t / path.Period);
        var best = double.PositiveInfinity;
        for (var k = index - 1; k <= index + 1; k++)
        {
            if (k < 0 || k >= path.Count)
            {
                continue;
            }

            var (from, to) = path.Interval(k);
            var center = (from + to) / 2;
            var extent = (to - from) / 2;
            var qx = Math.Abs(t - center) - extent;
            var qy = absDistance - half;
            var outside = Math.Sqrt(Math.Max(qx, 0) * Math.Max(qx, 0) + Math.Max(qy, 0) * Math.Max(qy, 0));
            best = Math.Min(best, outside + Math.Min(Math.Max(qx, qy), 0));
        }

        return best;
    }

    /// <summary>x-Bereich, in dem lo ≤ a·x + b ≤ hi gilt (a ≈ 0: alles oder nichts).</summary>
    private static (double Lo, double Hi) Solve(double a, double b, double lo, double hi)
    {
        if (Math.Abs(a) < 1e-12)
        {
            return b >= lo && b <= hi ? (double.NegativeInfinity, double.PositiveInfinity) : (1, 0);
        }

        var x1 = (lo - b) / a;
        var x2 = (hi - b) / a;
        return x1 <= x2 ? (x1, x2) : (x2, x1);
    }
}
