using System.Globalization;

namespace OrbitStudio.Domain.Imaging;

/// <summary>
/// Farbe mit geradem (nicht vormultipliziertem) Alpha – so, wie Nutzer Farben wählen und eingeben.
/// Für Pixeloperationen mit <see cref="ToPremultiplied"/> in das interne Format umrechnen.
/// </summary>
public readonly record struct ColorRgba(byte R, byte G, byte B, byte A = 255)
{
    public static readonly ColorRgba Black = new(0, 0, 0);
    public static readonly ColorRgba White = new(255, 255, 255);
    public static readonly ColorRgba Transparent = new(0, 0, 0, 0);

    public Bgra32 ToPremultiplied() => Bgra32.FromStraight(R, G, B, A);

    public static ColorRgba FromPremultiplied(Bgra32 pixel)
    {
        var (r, g, b, a) = pixel.ToStraight();
        return new ColorRgba(r, g, b, a);
    }

    /// <summary>„#RRGGBB" bzw. „#RRGGBBAA", wenn nicht deckend.</summary>
    public string ToHex() => A == 255
        ? string.Create(CultureInfo.InvariantCulture, $"#{R:X2}{G:X2}{B:X2}")
        : string.Create(CultureInfo.InvariantCulture, $"#{R:X2}{G:X2}{B:X2}{A:X2}");

    /// <summary>Akzeptiert #RGB, #RRGGBB und #RRGGBBAA, jeweils mit oder ohne „#".</summary>
    public static bool TryParseHex(string? text, out ColorRgba color)
    {
        color = default;
        var s = text?.Trim().TrimStart('#');
        if (string.IsNullOrEmpty(s) || !s.All(Uri.IsHexDigit))
        {
            return false;
        }

        if (s.Length == 3)
        {
            s = string.Concat(s.Select(c => new string(c, 2)));
        }

        if (s.Length is not (6 or 8))
        {
            return false;
        }

        byte Part(int i) => byte.Parse(s.AsSpan(i, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        color = new ColorRgba(Part(0), Part(2), Part(4), s.Length == 8 ? Part(6) : (byte)255);
        return true;
    }
}
