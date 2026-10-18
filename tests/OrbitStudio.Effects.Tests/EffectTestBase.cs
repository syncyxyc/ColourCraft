using OrbitStudio.Domain.Imaging;
using OrbitStudio.Domain.Selection;

namespace OrbitStudio.Effects.Tests;

/// <summary>Kleine, exakt definierte Testbilder und Hilfen zum Ausführen von Effekten.</summary>
public abstract class EffectTestBase
{
    protected static Bgra32 Rgb(byte r, byte g, byte b) => Bgra32.FromStraight(r, g, b, 255);

    protected static Bgra32 Gray(byte v) => Rgb(v, v, v);

    protected static PixelBuffer Image(int width, int height, Bgra32 fill)
    {
        var buffer = PixelBuffer.Create(width, height);
        buffer.Fill(fill);
        return buffer;
    }

    /// <summary>Graustufenbild aus Zeilen (deckend).</summary>
    protected static PixelBuffer GrayRows(params byte[][] rows)
    {
        var buffer = PixelBuffer.Create(rows[0].Length, rows.Length);
        for (var y = 0; y < rows.Length; y++)
        {
            for (var x = 0; x < rows[y].Length; x++)
            {
                buffer.SetPixel(x, y, Gray(rows[y][x]));
            }
        }

        return buffer;
    }

    /// <summary>Zufälliges, gültiges vormultipliziertes Bild (fester Startwert).</summary>
    protected static PixelBuffer RandomImage(int width, int height, int seed, bool opaque = false)
    {
        var random = new Random(seed);
        var buffer = PixelBuffer.Create(width, height);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var a = opaque ? (byte)255 : (byte)random.Next(256);
                buffer.SetPixel(x, y, Bgra32.FromStraight((byte)random.Next(256), (byte)random.Next(256), (byte)random.Next(256), a));
            }
        }

        return buffer;
    }

    protected static EffectSettings Settings(IEffect effect, params (string Id, object Value)[] values)
    {
        var settings = EffectSettings.Defaults(effect);
        foreach (var (id, value) in values)
        {
            settings = settings.With(effect, id, value);
        }

        return settings;
    }

    protected static PixelBuffer Run(IEffect effect, PixelBuffer source, params (string Id, object Value)[] values) =>
        EffectEngine.Render(effect, Settings(effect, values), source, null, CancellationToken.None).Pixels;

    protected static PixelBuffer Run(IEffect effect, PixelBuffer source, SelectionMask? selection,
        params (string Id, object Value)[] values) =>
        EffectEngine.Render(effect, Settings(effect, values), source, selection, CancellationToken.None, source.Bounds).Pixels;

    protected static byte R(PixelBuffer buffer, int x, int y = 0) => buffer.GetPixel(x, y).R;
}
