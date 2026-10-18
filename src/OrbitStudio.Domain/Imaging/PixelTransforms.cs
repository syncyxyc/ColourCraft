namespace OrbitStudio.Domain.Imaging;

/// <summary>Verlustfreie Drehungen und Spiegelungen. Liefern stets einen neuen Puffer.</summary>
public static class PixelTransforms
{
    public static PixelBuffer FlipHorizontal(PixelBuffer source) =>
        Map(source, source.Width, source.Height, (x, y, w, h) => (w - 1 - x, y));

    public static PixelBuffer FlipVertical(PixelBuffer source) =>
        Map(source, source.Width, source.Height, (x, y, w, h) => (x, h - 1 - y));

    public static PixelBuffer Rotate180(PixelBuffer source) =>
        Map(source, source.Width, source.Height, (x, y, w, h) => (w - 1 - x, h - 1 - y));

    /// <summary>90° im Uhrzeigersinn: Quelle (x, y) → Ziel (H − 1 − y, x).</summary>
    public static PixelBuffer Rotate90Clockwise(PixelBuffer source) =>
        Map(source, source.Height, source.Width, (x, y, w, h) => (h - 1 - y, x));

    /// <summary>90° gegen den Uhrzeigersinn: Quelle (x, y) → Ziel (y, W − 1 − x).</summary>
    public static PixelBuffer Rotate90CounterClockwise(PixelBuffer source) =>
        Map(source, source.Height, source.Width, (x, y, w, h) => (y, w - 1 - x));

    /// <summary>Spiegelung an der Hauptdiagonalen: (x, y) → (y, x).</summary>
    public static PixelBuffer Transpose(PixelBuffer source) =>
        Map(source, source.Height, source.Width, (x, y, w, h) => (y, x));

    /// <summary>Spiegelung an der Nebendiagonalen: (x, y) → (H − 1 − y, W − 1 − x).</summary>
    public static PixelBuffer Transverse(PixelBuffer source) =>
        Map(source, source.Height, source.Width, (x, y, w, h) => (h - 1 - y, w - 1 - x));

    /// <param name="map">Bildet Quellkoordinate (x, y, Quellbreite, Quellhöhe) auf die Zielkoordinate ab.</param>
    private static PixelBuffer Map(PixelBuffer source, int targetWidth, int targetHeight,
        Func<int, int, int, int, (int X, int Y)> map)
    {
        var target = PixelBuffer.Create(targetWidth, targetHeight);
        for (var y = 0; y < source.Height; y++)
        {
            var row = source.GetPixelRow(y);
            for (var x = 0; x < source.Width; x++)
            {
                var (tx, ty) = map(x, y, source.Width, source.Height);
                target.GetPixelRow(ty)[tx] = row[x];
            }
        }

        return target;
    }
}
