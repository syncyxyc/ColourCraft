using OrbitStudio.Domain.Imaging;

namespace OrbitStudio.Effects;

/// <summary>
/// Gemeinsame Rechenregeln aller Effekte: gerundet wird kaufmännisch (0,5 → auf), begrenzt auf 0–255, Farbkanäle nie
/// größer als Alpha. Außerhalb des Bildes gelesene Pixel wiederholen den Rand („Rand verlängern").
/// </summary>
public static class PixelMath
{
    public static byte ToByte(double value)
    {
        var rounded = Math.Round(value, MidpointRounding.AwayFromZero);
        return rounded <= 0 ? (byte)0 : rounded >= 255 ? (byte)255 : (byte)rounded;
    }

    /// <summary>Pixel mit auf den Bildrand begrenzten Koordinaten.</summary>
    public static Bgra32 SampleClamped(PixelBuffer buffer, int x, int y) =>
        buffer.GetPixel(Math.Clamp(x, 0, buffer.Width - 1), Math.Clamp(y, 0, buffer.Height - 1));

    /// <summary>Gerade (nicht vormultiplizierte) Farbe 0–255 als double; bei Alpha 0 Schwarz.</summary>
    public static (double R, double G, double B) Straight(Bgra32 p) =>
        p.A == 0 ? (0, 0, 0) : (p.R * 255.0 / p.A, p.G * 255.0 / p.A, p.B * 255.0 / p.A);

    /// <summary>Gerade Farbe (wird auf 0–255 begrenzt) mit Alpha vormultiplizieren und runden.</summary>
    public static Bgra32 Premultiply(double r, double g, double b, byte alpha)
    {
        if (alpha == 0)
        {
            return Bgra32.Transparent;
        }

        var f = alpha / 255.0;
        return new Bgra32(
            Math.Min(ToByte(Math.Clamp(b, 0, 255) * f), alpha),
            Math.Min(ToByte(Math.Clamp(g, 0, 255) * f), alpha),
            Math.Min(ToByte(Math.Clamp(r, 0, 255) * f), alpha),
            alpha);
    }

    /// <summary>Vormultiplizierte Kanäle (double) runden; Farbe auf Alpha begrenzen.</summary>
    public static Bgra32 FromPremultiplied(double b, double g, double r, double a)
    {
        var alpha = ToByte(a);
        return new Bgra32(Math.Min(ToByte(b), alpha), Math.Min(ToByte(g), alpha), Math.Min(ToByte(r), alpha), alpha);
    }

    /// <summary>Helligkeit nach ITU-R BT.601 (wie Masken aus Farben).</summary>
    public static double Luma(double r, double g, double b) => 0.299 * r + 0.587 * g + 0.114 * b;

    /// <summary>Zeilen des Bereichs parallel bearbeiten; Abbruch über das Token des Kontexts.</summary>
    public static void ForEachRow(EffectContext context, Action<int> row) =>
        Parallel.For(context.Region.Y, context.Region.Bottom, context.ParallelOptions, row);

    // --- HSL (Farbton 0–360, Sättigung und Helligkeit 0–1) ------------------------------------------

    public static (double H, double S, double L) ToHsl(double r, double g, double b)
    {
        r /= 255;
        g /= 255;
        b /= 255;
        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var l = (max + min) / 2;
        var d = max - min;
        if (d <= 1e-12)
        {
            return (0, 0, l);
        }

        var s = l > 0.5 ? d / (2 - max - min) : d / (max + min);
        double h;
        if (max == r)
        {
            h = (g - b) / d + (g < b ? 6 : 0);
        }
        else if (max == g)
        {
            h = (b - r) / d + 2;
        }
        else
        {
            h = (r - g) / d + 4;
        }

        return (h * 60, s, l);
    }

    public static (double R, double G, double B) FromHsl(double h, double s, double l)
    {
        if (s <= 0)
        {
            return (l * 255, l * 255, l * 255);
        }

        h = ((h % 360) + 360) % 360 / 360;
        var q = l < 0.5 ? l * (1 + s) : l + s - l * s;
        var p = 2 * l - q;
        return (Hue(p, q, h + 1.0 / 3) * 255, Hue(p, q, h) * 255, Hue(p, q, h - 1.0 / 3) * 255);
    }

    private static double Hue(double p, double q, double t)
    {
        if (t < 0)
        {
            t += 1;
        }

        if (t > 1)
        {
            t -= 1;
        }

        return t < 1.0 / 6 ? p + (q - p) * 6 * t
            : t < 0.5 ? q
            : t < 2.0 / 3 ? p + (q - p) * (2.0 / 3 - t) * 6
            : p;
    }
}
