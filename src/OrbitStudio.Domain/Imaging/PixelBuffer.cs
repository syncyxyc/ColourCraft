using System.Runtime.InteropServices;
using OrbitStudio.Domain.Documents;

namespace OrbitStudio.Domain.Imaging;

/// <summary>
/// Rasterbild im internen Format (siehe <see cref="Bgra32"/>).
/// Speicherlayout: zeilenweise ab oben links, 4 Byte je Pixel in der Reihenfolge B, G, R, A,
/// Stride = Breite · 4 ohne Auffüllung. Pixel (x, y) liegt bei Offset y · Stride + x · 4.
/// </summary>
public sealed class PixelBuffer
{
    public const int BytesPerPixel = 4;

    private readonly byte[] _data;

    private PixelBuffer(int width, int height, byte[] data)
    {
        Width = width;
        Height = height;
        _data = data;
    }

    public int Width { get; }

    public int Height { get; }

    public int Stride => Width * BytesPerPixel;

    public IntRect Bounds => new(0, 0, Width, Height);

    /// <summary>Gesamte Pixeldaten (Länge = Stride · Höhe).</summary>
    public Span<byte> Data => _data;

    /// <summary>
    /// Legt einen vollständig transparenten Puffer an. Ungültige Größen werden vor der Speicherreservierung abgewiesen.
    /// </summary>
    /// <exception cref="DocumentValidationException">Größe ungültig oder zu groß.</exception>
    /// <exception cref="OutOfMemoryException">Nicht genug Arbeitsspeicher.</exception>
    public static PixelBuffer Create(int width, int height)
    {
        var result = DocumentLimits.ValidateSize(width, height);
        if (result != SizeValidationResult.Valid)
        {
            throw new DocumentValidationException(result);
        }

        var length = (long)width * height * BytesPerPixel;
        if (length > Array.MaxLength)
        {
            throw new DocumentValidationException(SizeValidationResult.ExceedsMaxPixelCount);
        }

        // GC.AllocateUninitializedArray wäre schneller, der Puffer muss hier aber definiert transparent sein.
        return new PixelBuffer(width, height, new byte[length]);
    }

    /// <summary>Übernimmt vorhandene Daten (Länge muss exakt Breite · Höhe · 4 sein). Die Daten werden kopiert.</summary>
    public static PixelBuffer FromBytes(int width, int height, ReadOnlySpan<byte> data)
    {
        var buffer = Create(width, height);
        if (data.Length != buffer._data.Length)
        {
            throw new ArgumentException($"Erwartet {buffer._data.Length} Byte, erhalten {data.Length}.", nameof(data));
        }

        data.CopyTo(buffer._data);
        return buffer;
    }

    public Span<byte> GetRow(int y)
    {
        if ((uint)y >= (uint)Height)
        {
            throw new ArgumentOutOfRangeException(nameof(y));
        }

        return _data.AsSpan(y * Stride, Stride);
    }

    /// <summary>Zeile als Pixelspanne.</summary>
    public Span<Bgra32> GetPixelRow(int y) => MemoryMarshal.Cast<byte, Bgra32>(GetRow(y));

    public bool Contains(int x, int y) => (uint)x < (uint)Width && (uint)y < (uint)Height;

    public Bgra32 GetPixel(int x, int y)
    {
        EnsureInside(x, y);
        var i = y * Stride + x * BytesPerPixel;
        return new Bgra32(_data[i], _data[i + 1], _data[i + 2], _data[i + 3]);
    }

    public void SetPixel(int x, int y, Bgra32 color)
    {
        EnsureInside(x, y);
        var i = y * Stride + x * BytesPerPixel;
        _data[i] = color.B;
        _data[i + 1] = color.G;
        _data[i + 2] = color.R;
        _data[i + 3] = color.A;
    }

    public void Fill(Bgra32 color) => Fill(Bounds, color);

    /// <summary>Füllt den Schnitt des Rechtecks mit den Bildgrenzen.</summary>
    public void Fill(IntRect region, Bgra32 color)
    {
        var r = region.Intersect(Bounds);
        for (var y = r.Y; y < r.Bottom; y++)
        {
            GetPixelRow(y).Slice(r.X, r.Width).Fill(color);
        }
    }

    public PixelBuffer Clone() => new(Width, Height, (byte[])_data.Clone());

    /// <summary>Byteweiser Vergleich von Größe und Inhalt.</summary>
    public bool ContentEquals(PixelBuffer other) =>
        Width == other.Width && Height == other.Height && _data.AsSpan().SequenceEqual(other._data);

    private void EnsureInside(int x, int y)
    {
        if (!Contains(x, y))
        {
            throw new ArgumentOutOfRangeException(nameof(x), $"Pixel ({x}, {y}) liegt außerhalb von {Width}×{Height}.");
        }
    }
}
