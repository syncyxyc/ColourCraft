using System.Buffers;
using OrbitStudio.Domain.Documents;
using OrbitStudio.Domain.Imaging;
using OrbitStudio.Domain.Layers;

namespace OrbitStudio.Rendering;

/// <summary>
/// CPU-Komposition des Ebenenbaums (Referenzpfad). Rechnet zeilenweise in <c>double</c> mit vormultipliertem Alpha
/// nach W3C „Compositing and Blending Level 1" (separierbare Mischmodi, Source-over):
/// <code>
///   co = cs·(1 − αb) + cb·(1 − αs) + αs·αb·B(Cb, Cs)
///   αo = αs + αb − αs·αb
/// </code>
/// cs, cb: vormultiplierte Kanäle; Cs = cs/αs, Cb = cb/αb. Deckkraft und Maske skalieren cs und αs.
/// Gruppen werden isoliert zusammengesetzt und dann wie eine Ebene eingefügt.
/// Ergebnis je Kanal: kaufmännisch auf 0–255 gerundet, Farbe ≤ Alpha.
/// </summary>
public static class LayerCompositor
{
    private const double Inv255 = 1.0 / 255.0;

    /// <summary>Setzt das ganze Dokument zu einem neuen Puffer zusammen (z. B. für den Export).</summary>
    public static PixelBuffer Composite(ImageDocument document)
    {
        var target = PixelBuffer.Create(document.Width, document.Height);
        CompositeRegion(document.Root, target, document.Bounds);
        return target;
    }

    /// <summary>Setzt den Bereich neu zusammen und überschreibt ihn im Ziel (Grundlage: transparent).</summary>
    public static void CompositeRegion(LayerGroup root, PixelBuffer target, IntRect region)
    {
        var r = region.Intersect(target.Bounds);
        if (r.IsEmpty)
        {
            return;
        }

        void Row(int y)
        {
            var length = r.Width * 4;
            var accumulator = ArrayPool<double>.Shared.Rent(length);
            try
            {
                Array.Clear(accumulator, 0, length);
                CompositeGroupRow(root, y, r.X, r.Width, accumulator);
                WriteRow(accumulator, target.GetRow(y).Slice(r.X * 4, length));
            }
            finally
            {
                ArrayPool<double>.Shared.Return(accumulator);
            }
        }

        // Kleine Bereiche (z. B. Pinselstriche) seriell, große parallel über Zeilen.
        if ((long)r.Width * r.Height < 64 * 1024)
        {
            for (var y = r.Y; y < r.Bottom; y++)
            {
                Row(y);
            }
        }
        else
        {
            Parallel.For(r.Y, r.Bottom, Row);
        }
    }

    /// <summary>Zusammengesetzter Wert eines einzelnen Pixels (z. B. Pipette), ohne großen Zielpuffer.</summary>
    public static Bgra32 CompositePixel(LayerGroup root, int x, int y)
    {
        var accumulator = new double[4];
        CompositeGroupRow(root, y, x, 1, accumulator);
        Span<byte> result = stackalloc byte[4];
        WriteRow(accumulator, result);
        return new Bgra32(result[0], result[1], result[2], result[3]);
    }

    /// <summary>
    /// Mischt eine Quelle mit Deckkraft, Maske und Mischmodus direkt in einen Puffer (für „Zusammenführen").
    /// </summary>
    public static void BlendInto(PixelBuffer target, PixelBuffer source, LayerMask? mask, double opacity, BlendMode mode)
    {
        if (source.Width != target.Width || source.Height != target.Height)
        {
            throw new ArgumentException("Quelle und Ziel müssen gleich groß sein.", nameof(source));
        }

        var width = target.Width;
        Parallel.For(0, target.Height, y =>
        {
            var length = width * 4;
            var accumulator = ArrayPool<double>.Shared.Rent(length);
            try
            {
                var row = target.GetRow(y);
                for (var i = 0; i < length; i++)
                {
                    accumulator[i] = row[i] * Inv255;
                }

                BlendBytesRow(accumulator, source.GetRow(y), mask is { IsEnabled: true } ? mask.GetRow(y) : default,
                    opacity, mode);
                WriteRow(accumulator, row);
            }
            finally
            {
                ArrayPool<double>.Shared.Return(accumulator);
            }
        });
    }

    private static void CompositeGroupRow(LayerGroup group, int y, int x0, int width, double[] destination)
    {
        foreach (var child in group.Children)
        {
            if (!child.IsVisible || child.Opacity <= 0)
            {
                continue;
            }

            switch (child)
            {
                case PixelLayer layer:
                {
                    var mask = layer.Mask is { IsEnabled: true } m ? m.GetRow(y).Slice(x0, width) : default;
                    BlendBytesRow(destination, layer.Pixels.GetRow(y).Slice(x0 * 4, width * 4), mask, layer.Opacity,
                        layer.BlendMode);
                    break;
                }

                case LayerGroup nested:
                {
                    var length = width * 4;
                    var isolated = ArrayPool<double>.Shared.Rent(length);
                    try
                    {
                        Array.Clear(isolated, 0, length);
                        CompositeGroupRow(nested, y, x0, width, isolated);
                        BlendDoublesRow(destination, isolated, length, nested.Opacity, nested.BlendMode);
                    }
                    finally
                    {
                        ArrayPool<double>.Shared.Return(isolated);
                    }

                    break;
                }
            }
        }
    }

    /// <summary>Quelle als Bytes (BGRA vormultipliert) mit optionaler Maskenzeile.</summary>
    private static void BlendBytesRow(double[] destination, ReadOnlySpan<byte> source, ReadOnlySpan<byte> mask,
        double opacity, BlendMode mode)
    {
        var count = source.Length / 4;
        for (var x = 0; x < count; x++)
        {
            var i = x * 4;
            var factor = opacity * Inv255;
            if (!mask.IsEmpty)
            {
                factor *= mask[x] * Inv255;
            }

            var sa = source[i + 3] * factor;
            if (sa <= 0)
            {
                continue;
            }

            BlendPixel(destination, i, source[i] * factor, source[i + 1] * factor, source[i + 2] * factor, sa, mode);
        }
    }

    /// <summary>
    /// Quelle als bereits zusammengesetzte Gruppe (Werte 0–1, vormultipliert). <paramref name="length"/> ist die
    /// genutzte Länge – gemietete Arrays können länger sein.
    /// </summary>
    private static void BlendDoublesRow(double[] destination, double[] source, int length, double opacity, BlendMode mode)
    {
        for (var i = 0; i + 3 < length; i += 4)
        {
            var sa = source[i + 3] * opacity;
            if (sa <= 0)
            {
                continue;
            }

            BlendPixel(destination, i, source[i] * opacity, source[i + 1] * opacity, source[i + 2] * opacity, sa, mode);
        }
    }

    /// <summary>Source-over mit Mischfunktion; alle Werte 0–1, vormultipliert, Reihenfolge B, G, R, A.</summary>
    private static void BlendPixel(double[] d, int i, double sb, double sg, double sr, double sa, BlendMode mode)
    {
        var ab = d[i + 3];
        if (mode == BlendMode.Normal || ab <= 0)
        {
            // Bei leerem Hintergrund reduzieren sich alle Modi auf Normal.
            var keep = 1 - sa;
            d[i] = sb + d[i] * keep;
            d[i + 1] = sg + d[i + 1] * keep;
            d[i + 2] = sr + d[i + 2] * keep;
            d[i + 3] = sa + ab * keep;
            return;
        }

        var both = sa * ab;
        d[i] = Channel(d[i], sb, ab, sa, both, mode);
        d[i + 1] = Channel(d[i + 1], sg, ab, sa, both, mode);
        d[i + 2] = Channel(d[i + 2], sr, ab, sa, both, mode);
        d[i + 3] = sa + ab - both;
    }

    private static double Channel(double cb, double cs, double ab, double sa, double both, BlendMode mode)
    {
        var backdrop = cb / ab;
        var src = cs / sa;
        return cs * (1 - ab) + cb * (1 - sa) + both * Blend(backdrop, src, mode);
    }

    /// <summary>Mischfunktion B(Cb, Cs) für nicht vormultiplizierte Werte 0–1.</summary>
    public static double Blend(double cb, double cs, BlendMode mode) => mode switch
    {
        BlendMode.Normal => cs,
        BlendMode.Multiply => cb * cs,
        BlendMode.Screen => cb + cs - cb * cs,
        BlendMode.Add => Math.Min(1, cb + cs),
        BlendMode.Negation => 1 - Math.Abs(1 - cb - cs),
        BlendMode.Difference => Math.Abs(cb - cs),
        BlendMode.Darken => Math.Min(cb, cs),
        BlendMode.Lighten => Math.Max(cb, cs),
        BlendMode.Overlay => cb <= 0.5 ? 2 * cs * cb : 1 - 2 * (1 - cs) * (1 - cb),
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null),
    };

    private static void WriteRow(double[] values, Span<byte> target)
    {
        for (var i = 0; i + 3 < target.Length; i += 4)
        {
            var a = ToByte(values[i + 3]);
            target[i] = Math.Min(ToByte(values[i]), a);
            target[i + 1] = Math.Min(ToByte(values[i + 1]), a);
            target[i + 2] = Math.Min(ToByte(values[i + 2]), a);
            target[i + 3] = a;
        }
    }

    private static byte ToByte(double value)
    {
        var scaled = Math.Round(value * 255, MidpointRounding.AwayFromZero);
        return scaled <= 0 ? (byte)0 : scaled >= 255 ? (byte)255 : (byte)scaled;
    }
}
