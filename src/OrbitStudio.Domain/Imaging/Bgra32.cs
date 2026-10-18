namespace OrbitStudio.Domain.Imaging;

/// <summary>
/// Ein Pixel im internen Format: BGRA, 8 Bit je Kanal, <b>vormultipliertes Alpha</b>
/// (jeder Farbkanal ist bereits mit Alpha/255 multipliert, daher gilt immer B, G, R ≤ A).
/// </summary>
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, Pack = 1)]
public readonly record struct Bgra32(byte B, byte G, byte R, byte A)
{
    public static readonly Bgra32 Transparent = new(0, 0, 0, 0);
    public static readonly Bgra32 White = new(255, 255, 255, 255);
    public static readonly Bgra32 Black = new(0, 0, 0, 255);

    /// <summary>Erzeugt ein Pixel aus nicht vormultiplizierten Werten (wie in Farbwählern und Dateien üblich).</summary>
    public static Bgra32 FromStraight(byte r, byte g, byte b, byte a) =>
        new(ColorMath.Premultiply(b, a), ColorMath.Premultiply(g, a), ColorMath.Premultiply(r, a), a);

    /// <summary>Liefert die nicht vormultiplizierten Werte (R, G, B, A).</summary>
    public (byte R, byte G, byte B, byte A) ToStraight() =>
        (ColorMath.Unpremultiply(R, A), ColorMath.Unpremultiply(G, A), ColorMath.Unpremultiply(B, A), A);

    /// <summary>True, wenn die Invariante des vormultiplizierten Formats erfüllt ist.</summary>
    public bool IsValidPremultiplied => B <= A && G <= A && R <= A;

    public override string ToString() => $"BGRA({B}, {G}, {R}, {A})";
}

/// <summary>Umrechnung zwischen vormultipliertem und geradem Alpha mit definierter Rundung.</summary>
public static class ColorMath
{
    /// <summary>c · a / 255, kaufmännisch gerundet.</summary>
    public static byte Premultiply(byte c, byte a) => (byte)((c * a + 127) / 255);

    /// <summary>c · 255 / a, kaufmännisch gerundet und auf 255 begrenzt; bei a = 0 ist das Ergebnis 0.</summary>
    public static byte Unpremultiply(byte c, byte a)
    {
        if (a == 0)
        {
            return 0;
        }

        var value = (c * 255 + a / 2) / a;
        return (byte)Math.Min(255, value);
    }
}
