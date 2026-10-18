using OrbitStudio.Domain.Imaging;

namespace OrbitStudio.Effects;

/// <summary>
/// Faltungen im vormultiplizierten Raum (kein Farbsaum an transparenten Kanten). Gerechnet wird in Bändern von Zeilen
/// mit double-Zwischenwerten; die Zahl gleichzeitig bearbeiteter Bänder wird durch ein Speicherbudget begrenzt.
/// Gerundet wird nur am Ende.
/// </summary>
public static class Convolution
{
    private const int BandRows = 32;
    private const long MemoryBudget = 256L * 1024 * 1024;

    /// <summary>Gauß-Kern: σ = Radius / 2, halbe Breite = ⌈3σ⌉, normiert auf Summe 1. Radius ≤ 0 → [1].</summary>
    public static double[] GaussianKernel(double radius)
    {
        if (radius <= 0)
        {
            return [1];
        }

        var sigma = radius / 2;
        var half = (int)Math.Ceiling(3 * sigma);
        var kernel = new double[2 * half + 1];
        var sum = 0.0;
        for (var i = -half; i <= half; i++)
        {
            sum += kernel[i + half] = Math.Exp(-(i * i) / (2 * sigma * sigma));
        }

        for (var i = 0; i < kernel.Length; i++)
        {
            kernel[i] /= sum;
        }

        return kernel;
    }

    /// <summary>Gaußscher Weichzeichner für <paramref name="region"/> von <paramref name="source"/> (Rand verlängert).</summary>
    public static void Gaussian(PixelBuffer source, IntRect region, double radius, PixelBuffer output, CancellationToken cancellation)
    {
        var kernel = GaussianKernel(radius);
        var k = kernel.Length / 2;
        var w = region.Width;
        ForEachBand(region, k, w * 4 * sizeof(double), cancellation, (y0, y1) =>
        {
            var rows = y1 - y0 + 2 * k;
            var h = new double[rows * w * 4];
            for (var r = 0; r < rows; r++)
            {
                var row = source.GetPixelRow(Math.Clamp(y0 - k + r, 0, source.Height - 1));
                for (var x = 0; x < w; x++)
                {
                    double b = 0, g = 0, rr = 0, a = 0;
                    for (var i = -k; i <= k; i++)
                    {
                        var p = row[Math.Clamp(region.X + x + i, 0, source.Width - 1)];
                        var weight = kernel[i + k];
                        b += p.B * weight;
                        g += p.G * weight;
                        rr += p.R * weight;
                        a += p.A * weight;
                    }

                    var o = (r * w + x) * 4;
                    h[o] = b;
                    h[o + 1] = g;
                    h[o + 2] = rr;
                    h[o + 3] = a;
                }
            }

            cancellation.ThrowIfCancellationRequested();
            for (var y = y0; y < y1; y++)
            {
                var target = output.GetPixelRow(y - region.Y);
                for (var x = 0; x < w; x++)
                {
                    double b = 0, g = 0, rr = 0, a = 0;
                    for (var j = 0; j <= 2 * k; j++)
                    {
                        var o = ((y - y0 + j) * w + x) * 4;
                        var weight = kernel[j];
                        b += h[o] * weight;
                        g += h[o + 1] * weight;
                        rr += h[o + 2] * weight;
                        a += h[o + 3] * weight;
                    }

                    target[x] = PixelMath.FromPremultiplied(b, g, rr, a);
                }
            }
        });
    }

    /// <summary>
    /// Einfacher Weichzeichner: Mittelwert über (2r + 1)² Pixel (Rand verlängert), exakt in Ganzzahlen gerechnet:
    /// (Summe + n/2) / n mit n = (2r + 1)². Aufwand unabhängig vom Radius (gleitende Summen).
    /// </summary>
    public static void Box(PixelBuffer source, IntRect region, int radius, PixelBuffer output, CancellationToken cancellation)
    {
        var w = region.Width;
        var n = (2 * radius + 1) * (2 * radius + 1);
        ForEachBand(region, radius, w * 4 * sizeof(int), cancellation, (y0, y1) =>
        {
            var rows = y1 - y0 + 2 * radius;
            var h = new int[rows * w * 4];
            for (var r = 0; r < rows; r++)
            {
                var sy = Math.Clamp(y0 - radius + r, 0, source.Height - 1);
                Bgra32 At(int x) => source.GetPixel(Math.Clamp(x, 0, source.Width - 1), sy);
                int sb = 0, sg = 0, sr = 0, sa = 0;
                for (var i = -radius; i <= radius; i++)
                {
                    var p = At(region.X + i);
                    sb += p.B;
                    sg += p.G;
                    sr += p.R;
                    sa += p.A;
                }

                for (var x = 0; x < w; x++)
                {
                    var o = (r * w + x) * 4;
                    h[o] = sb;
                    h[o + 1] = sg;
                    h[o + 2] = sr;
                    h[o + 3] = sa;
                    var add = At(region.X + x + radius + 1);
                    var remove = At(region.X + x - radius);
                    sb += add.B - remove.B;
                    sg += add.G - remove.G;
                    sr += add.R - remove.R;
                    sa += add.A - remove.A;
                }
            }

            cancellation.ThrowIfCancellationRequested();
            var sums = new long[w * 4];
            for (var j = 0; j <= 2 * radius; j++)
            {
                for (var i = 0; i < w * 4; i++)
                {
                    sums[i] += h[j * w * 4 + i];
                }
            }

            for (var y = y0; y < y1; y++)
            {
                var target = output.GetPixelRow(y - region.Y);
                for (var x = 0; x < w; x++)
                {
                    var o = x * 4;
                    var alpha = Divide(sums[o + 3], n);
                    target[x] = new Bgra32(Math.Min(Divide(sums[o], n), alpha), Math.Min(Divide(sums[o + 1], n), alpha),
                        Math.Min(Divide(sums[o + 2], n), alpha), alpha);
                }

                if (y + 1 < y1)
                {
                    var add = (y + 1 - y0 + 2 * radius) * w * 4;
                    var remove = (y - y0) * w * 4;
                    for (var i = 0; i < w * 4; i++)
                    {
                        sums[i] += h[add + i] - h[remove + i];
                    }
                }
            }
        });
    }

    private static byte Divide(long sum, int n) => (byte)((sum + n / 2) / n);

    /// <summary>Bereich in Zeilenbändern parallel bearbeiten; je Band wird ein Puffer mit Randzeilen angelegt.</summary>
    private static void ForEachBand(IntRect region, int margin, long bytesPerRow, CancellationToken cancellation,
        Action<int, int> band)
    {
        var bands = (region.Height + BandRows - 1) / BandRows;
        var bandBytes = Math.Max(1, (BandRows + 2L * margin) * bytesPerRow);
        var parallel = (int)Math.Clamp(MemoryBudget / bandBytes, 1, Environment.ProcessorCount);
        var options = new ParallelOptions { CancellationToken = cancellation, MaxDegreeOfParallelism = parallel };
        Parallel.For(0, bands, options, b =>
        {
            var y0 = region.Y + b * BandRows;
            band(y0, Math.Min(region.Bottom, y0 + BandRows));
        });
    }
}

/// <summary>Gaußscher Weichzeichner (Radius 0–200 px, σ = Radius / 2).</summary>
public sealed class GaussianBlurEffect : EffectBase
{
    public static readonly NumberParameter Radius = new("radius", "EffectParam.Radius", 0, 200, 4, 0.5, 1, "Unit.Px", Spatial: true);

    public override string Id => "gaussianBlur";

    public override EffectCategory Category => EffectCategory.Blur;

    public override IReadOnlyList<EffectParameter> Parameters => [Radius];

    public override void Render(EffectContext c) =>
        Convolution.Gaussian(c.Source, c.Region, c.Settings.Number(Radius.Id), c.Output, c.Cancellation);
}

/// <summary>Einfacher Weichzeichner: Mittelwert eines Quadrats der Kantenlänge 2 · Radius + 1.</summary>
public sealed class BoxBlurEffect : EffectBase
{
    public static readonly NumberParameter Radius = new("radius", "EffectParam.Radius", 0, 200, 2, UnitKey: "Unit.Px", Spatial: true);

    public override string Id => "boxBlur";

    public override EffectCategory Category => EffectCategory.Blur;

    public override IReadOnlyList<EffectParameter> Parameters => [Radius];

    public override void Render(EffectContext c) =>
        Convolution.Box(c.Source, c.Region, c.Settings.Integer(Radius.Id), c.Output, c.Cancellation);
}

/// <summary>
/// Schärfen (Unscharf maskieren): c' = c + Stärke · (c − Gauß(c)) für die Farbkanäle (vormultipliziert),
/// Alpha bleibt; Ergebnis auf 0 … Alpha begrenzt.
/// </summary>
public sealed class SharpenEffect : EffectBase
{
    public static readonly NumberParameter Amount = new("amount", "EffectParam.Amount", 0, 500, 100, UnitKey: "Unit.Percent");
    public static readonly NumberParameter Radius = new("radius", "EffectParam.Radius", 0.5, 50, 2, 0.5, 1, "Unit.Px", Spatial: true);

    public override string Id => "sharpen";

    public override EffectCategory Category => EffectCategory.Sharpen;

    public override IReadOnlyList<EffectParameter> Parameters => [Amount, Radius];

    public override void Render(EffectContext c)
    {
        var region = c.Region;
        var blurred = PixelBuffer.Create(region.Width, region.Height);
        Convolution.Gaussian(c.Source, region, c.Settings.Number(Radius.Id), blurred, c.Cancellation);
        var amount = c.Settings.Number(Amount.Id) / 100;
        PixelMath.ForEachRow(c, y =>
        {
            var source = c.Source.GetPixelRow(y);
            var blur = blurred.GetPixelRow(y - region.Y);
            var target = c.Output.GetPixelRow(y - region.Y);
            for (var x = 0; x < region.Width; x++)
            {
                var p = source[region.X + x];
                var q = blur[x];
                target[x] = PixelMath.FromPremultiplied(
                    p.B + amount * (p.B - q.B), p.G + amount * (p.G - q.G), p.R + amount * (p.R - q.R), p.A);
            }
        });
    }
}

/// <summary>
/// Kantenerkennung (Sobel) auf der Helligkeit (BT.601) der vormultiplizierten Farbe. Grauwert = |∇| / 4 · Stärke,
/// begrenzt auf 255 (eine harte Kante 0 → 255 ergibt 255 bei 100 %); optional invertiert (dunkle Kanten auf Weiß).
/// Alpha bleibt.
/// </summary>
public sealed class EdgeDetectEffect : EffectBase
{
    public static readonly NumberParameter Strength = new("strength", "EffectParam.Strength", 10, 500, 100, UnitKey: "Unit.Percent");
    public static readonly BoolParameter Invert = new("invert", "EffectParam.InvertEdges", false);

    public override string Id => "edges";

    public override EffectCategory Category => EffectCategory.Stylize;

    public override IReadOnlyList<EffectParameter> Parameters => [Strength, Invert];

    public override void Render(EffectContext c)
    {
        var scale = c.Settings.Number(Strength.Id) / 100 / 4;
        var invert = c.Settings.Bool(Invert.Id);
        var source = c.Source;
        double L(int x, int y)
        {
            var p = PixelMath.SampleClamped(source, x, y);
            return PixelMath.Luma(p.R, p.G, p.B);
        }

        var region = c.Region;
        PixelMath.ForEachRow(c, y =>
        {
            var target = c.Output.GetPixelRow(y - region.Y);
            for (var x = region.X; x < region.Right; x++)
            {
                var gx = L(x + 1, y - 1) + 2 * L(x + 1, y) + L(x + 1, y + 1) - L(x - 1, y - 1) - 2 * L(x - 1, y) - L(x - 1, y + 1);
                var gy = L(x - 1, y + 1) + 2 * L(x, y + 1) + L(x + 1, y + 1) - L(x - 1, y - 1) - 2 * L(x, y - 1) - L(x + 1, y - 1);
                var value = Math.Min(255, Math.Sqrt(gx * gx + gy * gy) * scale);
                if (invert)
                {
                    value = 255 - value;
                }

                target[x - region.X] = PixelMath.Premultiply(value, value, value, source.GetPixel(x, y).A);
            }
        });
    }
}

/// <summary>
/// Rauschen: gaußverteilt mit Standardabweichung = Menge (in Farbwerten 0–255). Farbsättigung 0 % = gleiches Rauschen
/// in allen Kanälen, 100 % = unabhängig je Kanal. Das Rauschen hängt nur von Startwert und Pixelposition ab und ist
/// damit reproduzierbar (gleicher Startwert = gleiches Ergebnis, unabhängig vom bearbeiteten Ausschnitt).
/// </summary>
public sealed class NoiseEffect : PointEffect
{
    public static readonly NumberParameter Amount = new("amount", "EffectParam.Amount", 0, 100, 20);
    public static readonly NumberParameter ColorSaturation = new("colorSaturation", "EffectParam.ColorSaturation", 0, 100, 100,
        UnitKey: "Unit.Percent");
    public static readonly NumberParameter Seed = new("seed", "EffectParam.Seed", 0, 99999, 1);

    public override string Id => "noise";

    public override EffectCategory Category => EffectCategory.Noise;

    public override IReadOnlyList<EffectParameter> Parameters => [Amount, ColorSaturation, Seed];

    protected override ColorMap Prepare(EffectSettings settings)
    {
        var sigma = settings.Number(Amount.Id);
        var color = settings.Number(ColorSaturation.Id) / 100;
        var seed = (ulong)settings.Integer(Seed.Id);
        return (r, g, b, x, y) =>
        {
            var mono = Gaussian(seed, x, y, 3);
            double N(int channel) => sigma * (mono + (Gaussian(seed, x, y, channel) - mono) * color);
            return (r + N(0), g + N(1), b + N(2));
        };
    }

    /// <summary>Standardnormalverteilte Zahl aus (Startwert, x, y, Kanal) – SplitMix64 + Box-Muller.</summary>
    public static double Gaussian(ulong seed, int x, int y, int channel)
    {
        var state = seed * 0x9E3779B97F4A7C15UL ^ ((ulong)(uint)x << 32 | (uint)y) ^ ((ulong)channel << 61);
        var u1 = ((Mix(ref state) >> 11) + 1) * (1.0 / 9007199254740993.0); // (0, 1]
        var u2 = (Mix(ref state) >> 11) * (1.0 / 9007199254740992.0);
        return Math.Sqrt(-2 * Math.Log(u1)) * Math.Cos(2 * Math.PI * u2);
    }

    private static ulong Mix(ref ulong state)
    {
        var z = state += 0x9E3779B97F4A7C15UL;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }
}

/// <summary>
/// Rauschreduktion: Median je Kanal (vormultipliziert) über (2 · Radius + 1)² Pixel, Rand verlängert; mit Stärke
/// 0–100 % zwischen Original und Median gemischt; Farbe danach auf Alpha begrenzt. Gleitende Histogramme je Zeile.
/// </summary>
public sealed class DenoiseEffect : EffectBase
{
    public static readonly NumberParameter Radius = new("radius", "EffectParam.Radius", 1, 10, 1, UnitKey: "Unit.Px", Spatial: true);
    public static readonly NumberParameter Strength = new("strength", "EffectParam.Strength", 0, 100, 100, UnitKey: "Unit.Percent");

    public override string Id => "denoise";

    public override EffectCategory Category => EffectCategory.Noise;

    public override IReadOnlyList<EffectParameter> Parameters => [Radius, Strength];

    public override void Render(EffectContext c)
    {
        var radius = c.Settings.Integer(Radius.Id);
        var k = c.Settings.Number(Strength.Id) / 100;
        var source = c.Source;
        var region = c.Region;
        var rank = (2 * radius + 1) * (2 * radius + 1) / 2;
        PixelMath.ForEachRow(c, y =>
        {
            var histogram = new int[4 * 256];
            void Column(int x, int delta)
            {
                for (var j = -radius; j <= radius; j++)
                {
                    var p = PixelMath.SampleClamped(source, x, y + j);
                    histogram[p.B] += delta;
                    histogram[256 + p.G] += delta;
                    histogram[512 + p.R] += delta;
                    histogram[768 + p.A] += delta;
                }
            }

            int Median(int channel)
            {
                var count = 0;
                for (var v = 0; v < 256; v++)
                {
                    count += histogram[channel * 256 + v];
                    if (count > rank)
                    {
                        return v;
                    }
                }

                return 255;
            }

            for (var i = -radius; i <= radius; i++)
            {
                Column(region.X + i, 1);
            }

            var target = c.Output.GetPixelRow(y - region.Y);
            var row = source.GetPixelRow(y);
            for (var x = region.X; x < region.Right; x++)
            {
                var p = row[x];
                target[x - region.X] = PixelMath.FromPremultiplied(
                    p.B + (Median(0) - p.B) * k, p.G + (Median(1) - p.G) * k, p.R + (Median(2) - p.R) * k,
                    p.A + (Median(3) - p.A) * k);
                Column(x - radius, -1);
                Column(x + radius + 1, 1);
            }
        });
    }
}

/// <summary>Verpixeln: Mittelwert (vormultipliziert) je Zelle; Zellen beginnen bei (0, 0) der Ebene, am Rand gekürzt.</summary>
public sealed class PixelateEffect : EffectBase
{
    public static readonly NumberParameter CellSize = new("cellSize", "EffectParam.CellSize", 2, 200, 8, UnitKey: "Unit.Px", Spatial: true);

    public override string Id => "pixelate";

    public override EffectCategory Category => EffectCategory.Distort;

    public override IReadOnlyList<EffectParameter> Parameters => [CellSize];

    public override void Render(EffectContext c)
    {
        var size = c.Settings.Integer(CellSize.Id);
        var source = c.Source;
        var region = c.Region;
        var firstRow = region.Y / size;
        var lastRow = (region.Bottom - 1) / size;
        Parallel.For(firstRow, lastRow + 1, c.ParallelOptions, cy =>
        {
            var y0 = cy * size;
            var y1 = Math.Min(source.Height, y0 + size);
            for (var cx = region.X / size; cx <= (region.Right - 1) / size; cx++)
            {
                var x0 = cx * size;
                var x1 = Math.Min(source.Width, x0 + size);
                double b = 0, g = 0, r = 0, a = 0;
                for (var y = y0; y < y1; y++)
                {
                    var row = source.GetPixelRow(y);
                    for (var x = x0; x < x1; x++)
                    {
                        b += row[x].B;
                        g += row[x].G;
                        r += row[x].R;
                        a += row[x].A;
                    }
                }

                var n = (double)(x1 - x0) * (y1 - y0);
                var value = PixelMath.FromPremultiplied(b / n, g / n, r / n, a / n);
                for (var y = Math.Max(y0, region.Y); y < Math.Min(y1, region.Bottom); y++)
                {
                    c.Output.GetPixelRow(y - region.Y)
                        .Slice(Math.Max(x0, region.X) - region.X, Math.Min(x1, region.Right) - Math.Max(x0, region.X))
                        .Fill(value);
                }
            }
        });
    }
}

/// <summary>
/// Schlagschatten: Alpha der Ebene um (Versatz X, Versatz Y) verschoben, mit Gauß (Weichheit = Radius) weichgezeichnet,
/// mit Deckkraft und Schattenfarbe eingefärbt und unter den Inhalt gelegt (Inhalt über Schatten).
/// </summary>
public sealed class DropShadowEffect : EffectBase
{
    public static readonly NumberParameter OffsetX = new("offsetX", "EffectParam.OffsetX", -200, 200, 6, UnitKey: "Unit.Px", Spatial: true);
    public static readonly NumberParameter OffsetY = new("offsetY", "EffectParam.OffsetY", -200, 200, 6, UnitKey: "Unit.Px", Spatial: true);
    public static readonly NumberParameter Softness = new("softness", "EffectParam.Softness", 0, 100, 4, UnitKey: "Unit.Px", Spatial: true);
    public static readonly NumberParameter Opacity = new("opacity", "EffectParam.Opacity", 0, 100, 60, UnitKey: "Unit.Percent");
    public static readonly ColorParameter ShadowColor = new("color", "EffectParam.ShadowColor", ColorRgba.Black);

    public override string Id => "dropShadow";

    public override EffectCategory Category => EffectCategory.Stylize;

    public override IReadOnlyList<EffectParameter> Parameters => [OffsetX, OffsetY, Softness, Opacity, ShadowColor];

    public override void Render(EffectContext c)
    {
        var s = c.Settings;
        var dx = s.Integer(OffsetX.Id);
        var dy = s.Integer(OffsetY.Id);
        var radius = s.Number(Softness.Id);
        var color = s.Color(ShadowColor.Id);
        var opacity = s.Number(Opacity.Id) / 100 * color.A / 255;
        var source = c.Source;
        var region = c.Region;

        // Verschobenes Alpha mit Rand (halbe Kernbreite), außerhalb der Ebene 0.
        var k = Convolution.GaussianKernel(radius).Length / 2;
        var mask = PixelBuffer.Create(region.Width + 2 * k, region.Height + 2 * k);
        for (var j = 0; j < mask.Height; j++)
        {
            var sy = region.Y - k + j - dy;
            if (sy < 0 || sy >= source.Height)
            {
                continue;
            }

            var row = source.GetPixelRow(sy);
            var target = mask.GetPixelRow(j);
            for (var i = 0; i < mask.Width; i++)
            {
                var sx = region.X - k + i - dx;
                if (sx >= 0 && sx < source.Width)
                {
                    target[i] = new Bgra32(0, 0, 0, row[sx].A);
                }
            }
        }

        var shadow = PixelBuffer.Create(region.Width, region.Height);
        Convolution.Gaussian(mask, new IntRect(k, k, region.Width, region.Height), radius, shadow, c.Cancellation);
        PixelMath.ForEachRow(c, y =>
        {
            var row = source.GetPixelRow(y);
            var shadowRow = shadow.GetPixelRow(y - region.Y);
            var target = c.Output.GetPixelRow(y - region.Y);
            for (var x = 0; x < region.Width; x++)
            {
                var p = row[region.X + x];
                var sa = shadowRow[x].A * opacity; // 0–255
                var keep = 1 - p.A / 255.0;
                target[x] = PixelMath.FromPremultiplied(
                    p.B + color.B * sa / 255 * keep, p.G + color.G * sa / 255 * keep, p.R + color.R * sa / 255 * keep,
                    p.A + sa * keep);
            }
        });
    }
}

/// <summary>Standardsatz der Effekte in Menüreihenfolge.</summary>
public static class StandardEffects
{
    public static IReadOnlyList<IEffect> Create()
    {
        var brightness = new BrightnessEffect();
        var contrast = new ContrastEffect();
        var hue = new HueEffect();
        var saturation = new SaturationEffect();
        return
        [
            // Farben
            new CompositeEffect("brightnessContrast", EffectCategory.Adjustment, brightness, contrast),
            brightness,
            contrast,
            new CompositeEffect("hueSaturation", EffectCategory.Adjustment, hue, saturation),
            hue,
            saturation,
            new LevelsEffect(),
            new ChannelEffect(),
            new BlackWhiteEffect(),
            new InvertEffect(),
            new SepiaEffect(),

            // Effekte
            new GaussianBlurEffect(),
            new BoxBlurEffect(),
            new SharpenEffect(),
            new NoiseEffect(),
            new DenoiseEffect(),
            new EdgeDetectEffect(),
            new DropShadowEffect(),
            new PixelateEffect(),
        ];
    }
}
