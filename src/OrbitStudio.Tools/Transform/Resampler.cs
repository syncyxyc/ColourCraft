using OrbitStudio.Domain.Imaging;
using OrbitStudio.Domain.Layers;
using OrbitStudio.Domain.Selection;

namespace OrbitStudio.Tools.Transform;

/// <summary>
/// Größenänderung und Drehung im vormultiplierten Raum (korrekte Ränder bei Transparenz).
/// Verkleinern: Flächenmittel (Box-Filter über den überdeckten Quellbereich). Vergrößern: bilinear,
/// Pixelmittelpunkte aufeinander ausgerichtet. Beide Achsen werden getrennt verarbeitet.
/// </summary>
public static class Resampler
{
    public static PixelBuffer Resize(PixelBuffer source, int width, int height)
    {
        if (width == source.Width && height == source.Height)
        {
            return source.Clone();
        }

        var horizontal = Weights(source.Width, width);
        var vertical = Weights(source.Height, height);

        // 1. Durchlauf: Breite ändern (double, 4 Kanäle)
        var temp = new double[(long)width * source.Height * 4];
        Parallel.For(0, source.Height, y =>
        {
            var row = source.GetRow(y);
            for (var x = 0; x < width; x++)
            {
                double b = 0, g = 0, r = 0, a = 0;
                foreach (var (index, weight) in horizontal[x])
                {
                    var i = index * 4;
                    b += row[i] * weight;
                    g += row[i + 1] * weight;
                    r += row[i + 2] * weight;
                    a += row[i + 3] * weight;
                }

                var o = ((long)y * width + x) * 4;
                temp[o] = b;
                temp[o + 1] = g;
                temp[o + 2] = r;
                temp[o + 3] = a;
            }
        });

        // 2. Durchlauf: Höhe ändern
        var result = PixelBuffer.Create(width, height);
        Parallel.For(0, height, y =>
        {
            var target = result.GetRow(y);
            for (var x = 0; x < width; x++)
            {
                double b = 0, g = 0, r = 0, a = 0;
                foreach (var (index, weight) in vertical[y])
                {
                    var o = ((long)index * width + x) * 4;
                    b += temp[o] * weight;
                    g += temp[o + 1] * weight;
                    r += temp[o + 2] * weight;
                    a += temp[o + 3] * weight;
                }

                Write(target, x, b, g, r, a);
            }
        });

        return result;
    }

    public static LayerMask Resize(LayerMask mask, int width, int height) =>
        MaskFromGray(Resize(GrayFromMask(mask.Width, mask.Height, mask.GetRow), width, height));

    public static SelectionMask Resize(SelectionMask selection, int width, int height)
    {
        var resized = Resize(GrayFromMask(selection.Width, selection.Height, y => selection.GetRow(y)), width, height);
        var result = SelectionMask.CreateEmpty(width, height);
        for (var y = 0; y < height; y++)
        {
            var row = resized.GetPixelRow(y);
            var target = result.GetRow(y);
            for (var x = 0; x < width; x++)
            {
                target[x] = row[x].A;
            }
        }

        return result;
    }

    /// <summary>
    /// Dreht (Grad, im Uhrzeigersinn) und skaliert um die Bildmitte; Ausgabe in gleicher Größe, bilinear,
    /// außerhalb der Quelle transparent.
    /// </summary>
    public static PixelBuffer RotateScale(PixelBuffer source, double angleDegrees, double scale)
    {
        if (scale <= 0 || !double.IsFinite(scale) || !double.IsFinite(angleDegrees))
        {
            throw new ArgumentOutOfRangeException(nameof(scale));
        }

        var result = PixelBuffer.Create(source.Width, source.Height);
        var angle = angleDegrees * Math.PI / 180;
        var cos = Math.Cos(angle);
        var sin = Math.Sin(angle);
        var cx = source.Width / 2.0;
        var cy = source.Height / 2.0;
        Parallel.For(0, source.Height, y =>
        {
            var target = result.GetRow(y);
            for (var x = 0; x < source.Width; x++)
            {
                // Zielpunkt → Quellpunkt (inverse Drehung und Skalierung)
                var dx = (x + 0.5 - cx) / scale;
                var dy = (y + 0.5 - cy) / scale;
                var sx = cos * dx + sin * dy + cx - 0.5;
                var sy = -sin * dx + cos * dy + cy - 0.5;
                var (b, g, r, a) = SampleBilinear(source, sx, sy);
                Write(target, x, b, g, r, a);
            }
        });

        return result;
    }

    private static (double B, double G, double R, double A) SampleBilinear(PixelBuffer source, double x, double y)
    {
        var x0 = (int)Math.Floor(x);
        var y0 = (int)Math.Floor(y);
        var fx = x - x0;
        var fy = y - y0;
        double b = 0, g = 0, r = 0, a = 0;
        for (var j = 0; j <= 1; j++)
        {
            for (var i = 0; i <= 1; i++)
            {
                var px = x0 + i;
                var py = y0 + j;
                if (!source.Contains(px, py))
                {
                    continue; // außerhalb: transparent
                }

                var w = (i == 0 ? 1 - fx : fx) * (j == 0 ? 1 - fy : fy);
                var p = source.GetPixel(px, py);
                b += p.B * w;
                g += p.G * w;
                r += p.R * w;
                a += p.A * w;
            }
        }

        return (b, g, r, a);
    }

    /// <summary>Gewichte je Zielindex: Box-Filter beim Verkleinern, bilinear beim Vergrößern. Summe = 1.</summary>
    private static (int Index, double Weight)[][] Weights(int sourceSize, int targetSize)
    {
        var scale = (double)sourceSize / targetSize;
        var result = new (int, double)[targetSize][];
        for (var t = 0; t < targetSize; t++)
        {
            if (scale >= 1)
            {
                var start = t * scale;
                var end = (t + 1) * scale;
                var list = new List<(int, double)>();
                for (var s = (int)Math.Floor(start); s < Math.Ceiling(end) && s < sourceSize; s++)
                {
                    var overlap = Math.Min(end, s + 1) - Math.Max(start, s);
                    if (overlap > 1e-12)
                    {
                        list.Add((s, overlap / scale));
                    }
                }

                result[t] = [.. list];
            }
            else
            {
                var center = (t + 0.5) * scale - 0.5;
                var i0 = (int)Math.Floor(center);
                var frac = center - i0;
                var a = Math.Clamp(i0, 0, sourceSize - 1);
                var b = Math.Clamp(i0 + 1, 0, sourceSize - 1);
                result[t] = a == b ? [(a, 1.0)] : [(a, 1 - frac), (b, frac)];
            }
        }

        return result;
    }

    private static void Write(Span<byte> target, int x, double b, double g, double r, double a)
    {
        var i = x * 4;
        var alpha = ToByte(a);
        target[i] = Math.Min(ToByte(b), alpha);
        target[i + 1] = Math.Min(ToByte(g), alpha);
        target[i + 2] = Math.Min(ToByte(r), alpha);
        target[i + 3] = alpha;
    }

    private static byte ToByte(double v)
    {
        var rounded = Math.Round(v, MidpointRounding.AwayFromZero);
        return rounded <= 0 ? (byte)0 : rounded >= 255 ? (byte)255 : (byte)rounded;
    }

    /// <summary>Maske als graues, vollständig gültiges vormultipliertes Bild (B = G = R = A = Maskenwert).</summary>
    internal static PixelBuffer GrayFromMask(int width, int height, Func<int, ReadOnlySpan<byte>> row)
    {
        var buffer = PixelBuffer.Create(width, height);
        for (var y = 0; y < height; y++)
        {
            var source = row(y);
            var target = buffer.GetPixelRow(y);
            for (var x = 0; x < width; x++)
            {
                var v = source[x];
                target[x] = new Bgra32(v, v, v, v);
            }
        }

        return buffer;
    }

    internal static LayerMask MaskFromGray(PixelBuffer gray)
    {
        var data = new byte[(long)gray.Width * gray.Height];
        for (var y = 0; y < gray.Height; y++)
        {
            var row = gray.GetPixelRow(y);
            for (var x = 0; x < gray.Width; x++)
            {
                data[y * gray.Width + x] = row[x].A;
            }
        }

        return LayerMask.FromBytes(gray.Width, gray.Height, data);
    }
}
