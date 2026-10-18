using OrbitStudio.Domain.Imaging;

namespace OrbitStudio.Domain.Selection;

/// <summary>
/// Erzeugt Auswahlmasken aus Formen und Bildinhalten. Grenzregel: Pixelmittelpunkt innerhalb der Form → 255.
/// </summary>
public static class SelectionShapes
{
    /// <summary>Rechteck zwischen zwei Eckpunkten (Dokumentkoordinaten, Reihenfolge beliebig).</summary>
    public static SelectionMask Rectangle(int width, int height, PointD a, PointD b)
    {
        var mask = SelectionMask.CreateEmpty(width, height);
        var x0 = (int)Math.Round(Math.Min(a.X, b.X));
        var y0 = (int)Math.Round(Math.Min(a.Y, b.Y));
        var x1 = (int)Math.Round(Math.Max(a.X, b.X));
        var y1 = (int)Math.Round(Math.Max(a.Y, b.Y));
        mask.Fill(new IntRect(x0, y0, x1 - x0, y1 - y0), 255);
        return mask;
    }

    /// <summary>Ellipse im umschließenden Rechteck zwischen zwei Eckpunkten.</summary>
    public static SelectionMask Ellipse(int width, int height, PointD a, PointD b)
    {
        var mask = SelectionMask.CreateEmpty(width, height);
        var left = Math.Round(Math.Min(a.X, b.X));
        var top = Math.Round(Math.Min(a.Y, b.Y));
        var right = Math.Round(Math.Max(a.X, b.X));
        var bottom = Math.Round(Math.Max(a.Y, b.Y));
        var rx = (right - left) / 2;
        var ry = (bottom - top) / 2;
        if (rx <= 0 || ry <= 0)
        {
            return mask;
        }

        var cx = left + rx;
        var cy = top + ry;
        var yStart = Math.Max(0, (int)top);
        var yEnd = Math.Min(height, (int)Math.Ceiling(bottom));
        for (var y = yStart; y < yEnd; y++)
        {
            var dy = (y + 0.5 - cy) / ry;
            var rest = 1 - dy * dy;
            if (rest < 0)
            {
                continue;
            }

            var half = rx * Math.Sqrt(rest);
            // Pixel x ausgewählt, wenn |x + 0,5 − cx| ≤ half
            var xFrom = (int)Math.Ceiling(cx - half - 0.5);
            var xTo = (int)Math.Floor(cx + half - 0.5);
            xFrom = Math.Max(0, xFrom);
            xTo = Math.Min(width - 1, xTo);
            if (xTo >= xFrom)
            {
                mask.GetRow(y).Slice(xFrom, xTo - xFrom + 1).Fill(255);
            }
        }

        return mask;
    }

    /// <summary>Polygon (geschlossen), Gerade-Ungerade-Regel, Pixelmittelpunkte.</summary>
    public static SelectionMask Polygon(int width, int height, IReadOnlyList<PointD> points)
    {
        var mask = SelectionMask.CreateEmpty(width, height);
        if (points.Count < 3)
        {
            return mask;
        }

        var minY = Math.Max(0, (int)Math.Floor(points.Min(p => p.Y)));
        var maxY = Math.Min(height - 1, (int)Math.Ceiling(points.Max(p => p.Y)));
        var crossings = new List<double>();
        for (var y = minY; y <= maxY; y++)
        {
            var sy = y + 0.5;
            crossings.Clear();
            for (var i = 0; i < points.Count; i++)
            {
                var p = points[i];
                var q = points[(i + 1) % points.Count];
                if ((p.Y <= sy && q.Y > sy) || (q.Y <= sy && p.Y > sy))
                {
                    crossings.Add(p.X + (sy - p.Y) / (q.Y - p.Y) * (q.X - p.X));
                }
            }

            crossings.Sort();
            var row = mask.GetRow(y);
            for (var i = 0; i + 1 < crossings.Count; i += 2)
            {
                var xFrom = Math.Max(0, (int)Math.Ceiling(crossings[i] - 0.5));
                var xTo = Math.Min(width - 1, (int)Math.Floor(crossings[i + 1] - 0.5));
                if (xTo >= xFrom)
                {
                    row.Slice(xFrom, xTo - xFrom + 1).Fill(255);
                }
            }
        }

        return mask;
    }

    /// <summary>Auswahl aus der Deckkraft (Alpha) eines Pixelpuffers.</summary>
    public static SelectionMask FromAlpha(PixelBuffer pixels)
    {
        var mask = SelectionMask.CreateEmpty(pixels.Width, pixels.Height);
        for (var y = 0; y < pixels.Height; y++)
        {
            var source = pixels.GetPixelRow(y);
            var row = mask.GetRow(y);
            for (var x = 0; x < source.Length; x++)
            {
                row[x] = source[x].A;
            }
        }

        return mask;
    }

    /// <summary>
    /// Pixel mit ähnlicher Farbe wie der Startpunkt (Zauberstab bzw. Farbbereich).
    /// Ähnlich: größte Abweichung eines Kanals (R, G, B, A, gerades Alpha) ≤ Toleranz·255/100.
    /// </summary>
    /// <param name="contiguous">True = nur zusammenhängende Pixel (4er-Nachbarschaft), false = im ganzen Bild.</param>
    public static SelectionMask SimilarColor(PixelBuffer pixels, int seedX, int seedY, double tolerancePercent,
        bool contiguous)
    {
        var mask = SelectionMask.CreateEmpty(pixels.Width, pixels.Height);
        if (!pixels.Contains(seedX, seedY))
        {
            return mask;
        }

        var threshold = Math.Clamp(tolerancePercent, 0, 100) * 255 / 100;
        var seed = pixels.GetPixel(seedX, seedY).ToStraight();

        bool Matches(Bgra32 p)
        {
            var (r, g, b, a) = p.ToStraight();
            var diff = Math.Max(Math.Max(Math.Abs(r - seed.R), Math.Abs(g - seed.G)),
                Math.Max(Math.Abs(b - seed.B), Math.Abs(a - seed.A)));
            return diff <= threshold + 1e-9;
        }

        if (!contiguous)
        {
            for (var y = 0; y < pixels.Height; y++)
            {
                var source = pixels.GetPixelRow(y);
                var row = mask.GetRow(y);
                for (var x = 0; x < source.Length; x++)
                {
                    if (Matches(source[x]))
                    {
                        row[x] = 255;
                    }
                }
            }

            return mask;
        }

        // Scanline-Flutfüllung mit eigenem Stapel (keine Rekursion → kein Stapelüberlauf bei großen Flächen).
        var stack = new Stack<(int X, int Y)>();
        stack.Push((seedX, seedY));
        while (stack.Count > 0)
        {
            var (x, y) = stack.Pop();
            var source = pixels.GetPixelRow(y);
            var row = mask.GetRow(y);
            if (row[x] != 0 || !Matches(source[x]))
            {
                continue;
            }

            var left = x;
            while (left > 0 && row[left - 1] == 0 && Matches(source[left - 1]))
            {
                left--;
            }

            var right = x;
            while (right < pixels.Width - 1 && row[right + 1] == 0 && Matches(source[right + 1]))
            {
                right++;
            }

            row.Slice(left, right - left + 1).Fill(255);
            foreach (var ny in new[] { y - 1, y + 1 })
            {
                if (ny < 0 || ny >= pixels.Height)
                {
                    continue;
                }

                var nSource = pixels.GetPixelRow(ny);
                var nRow = mask.GetRow(ny);
                var inRun = false;
                for (var nx = left; nx <= right; nx++)
                {
                    var candidate = nRow[nx] == 0 && Matches(nSource[nx]);
                    if (candidate && !inRun)
                    {
                        stack.Push((nx, ny));
                    }

                    inRun = candidate;
                }
            }
        }

        return mask;
    }

    /// <summary>
    /// Vergrößert (radius &gt; 0) oder verkleinert (radius &lt; 0) die Auswahl um einen Kreis mit dem Radius in Pixeln.
    /// Basis ist die Maske mit Schwelle 128; das Ergebnis ist hart (0/255). Exakte euklidische Distanz.
    /// </summary>
    public static SelectionMask GrowOrShrink(SelectionMask source, double radius)
    {
        var width = source.Width;
        var height = source.Height;
        var result = SelectionMask.CreateEmpty(width, height);
        if (radius == 0)
        {
            for (var y = 0; y < height; y++)
            {
                var src = source.GetRow(y);
                var dst = result.GetRow(y);
                for (var x = 0; x < width; x++)
                {
                    dst[x] = src[x] >= 128 ? (byte)255 : (byte)0;
                }
            }

            return result;
        }

        var grow = radius > 0;
        var r2 = radius * radius;
        // Wachsen: Abstand jedes Pixels zum nächsten ausgewählten Pixel ≤ r → ausgewählt.
        // Schrumpfen: Abstand zum nächsten nicht ausgewählten Pixel ≤ r → nicht ausgewählt.
        var distance = SquaredDistanceTransform(source, featureIsSelected: grow);
        for (var i = 0; i < distance.Length; i++)
        {
            var within = distance[i] <= r2 + 1e-9;
            result.Data[i] = grow ? (within ? (byte)255 : (byte)0) : (within ? (byte)0 : (byte)255);
        }

        return result;
    }

    /// <summary>Quadrierte euklidische Distanz zum nächsten Merkmalspixel (Felzenszwalb/Huttenlocher, linear).</summary>
    private static double[] SquaredDistanceTransform(SelectionMask mask, bool featureIsSelected)
    {
        const double Infinity = 1e20;
        var width = mask.Width;
        var height = mask.Height;
        var grid = new double[width * height];
        for (var i = 0; i < grid.Length; i++)
        {
            var selected = mask.Data[i] >= 128;
            grid[i] = selected == featureIsSelected ? 0 : Infinity;
        }

        // Beim Schrumpfen gilt alles außerhalb des Bildes als „nicht ausgewählt": Je eine virtuelle Merkmalszeile
        // bzw. -spalte (Wert 0) vor und hinter dem Bild. Der nächste Punkt außerhalb eines Rechtecks liegt immer
        // senkrecht zu einer Kante, daher genügen diese Ränder in beiden Durchläufen.
        var border = !featureIsSelected;
        var pad = border ? 1 : 0;
        var capacity = Math.Max(width, height) + 2;
        var f = new double[capacity];
        var d = new double[capacity];
        var v = new int[capacity];
        var z = new double[capacity + 1];

        // Untere Einhüllende von Parabeln (Felzenszwalb/Huttenlocher 2012), n Werte in f → d.
        void Transform1D(int n)
        {
            var k = 0;
            v[0] = 0;
            z[0] = double.NegativeInfinity;
            z[1] = double.PositiveInfinity;
            for (var q = 1; q < n; q++)
            {
                var s = Intersection(q, v[k]);
                while (s <= z[k])
                {
                    k--;
                    s = Intersection(q, v[k]);
                }

                k++;
                v[k] = q;
                z[k] = s;
                z[k + 1] = double.PositiveInfinity;
            }

            k = 0;
            for (var q = 0; q < n; q++)
            {
                while (z[k + 1] < q)
                {
                    k++;
                }

                d[q] = (double)(q - v[k]) * (q - v[k]) + f[v[k]];
            }
        }

        double Intersection(int q, int p) => (f[q] + (double)q * q - (f[p] + (double)p * p)) / (2.0 * q - 2.0 * p);

        void Pass(int length, Func<int, int> index)
        {
            var n = length + 2 * pad;
            if (border)
            {
                f[0] = 0;
                f[n - 1] = 0;
            }

            for (var i = 0; i < length; i++)
            {
                f[i + pad] = grid[index(i)];
            }

            Transform1D(n);
            for (var i = 0; i < length; i++)
            {
                grid[index(i)] = d[i + pad];
            }
        }

        for (var x = 0; x < width; x++)
        {
            var column = x;
            Pass(height, y => y * width + column);
        }

        for (var y = 0; y < height; y++)
        {
            var rowStart = y * width;
            Pass(width, x => rowStart + x);
        }

        return grid;
    }
}
