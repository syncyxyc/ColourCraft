using System.Buffers.Binary;
using OrbitStudio.Domain.Imaging;

namespace OrbitStudio.Imaging.Codecs;

/// <summary>
/// BMP: Lesen über SkiaSharp, Schreiben mit eigenem Encoder (Skia schreibt kein BMP).
/// Vollständig deckende Bilder → 24 Bit (BITMAPINFOHEADER, größte Kompatibilität).
/// Bilder mit Transparenz → 32 Bit BGRA mit Alpha (BITMAPV4HEADER, BI_BITFIELDS, gerades Alpha, sRGB).
/// </summary>
public sealed class BmpCodec : IImageCodec
{
    private const int FileHeaderSize = 14;
    private const int InfoHeaderSize = 40;
    private const int V4HeaderSize = 108;
    private const uint BiRgb = 0;
    private const uint BiBitfields = 3;
    private const uint LcsSrgb = 0x73524742; // 'sRGB'

    public ImageFileFormat Format { get; } =
        new("bmp", [".bmp", ".dib"], true, true, SupportsAlpha: true, SupportsQuality: false);

    public DecodedImage Decode(Stream stream, CancellationToken cancellationToken = default) =>
        SkiaDecoder.Decode(stream, cancellationToken);

    public void Encode(PixelBuffer pixels, Stream stream, EncodeOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pixels);
        ArgumentNullException.ThrowIfNull(stream);

        var hasAlpha = HasTransparency(pixels);
        var bitsPerPixel = hasAlpha ? 32 : 24;
        var headerSize = hasAlpha ? V4HeaderSize : InfoHeaderSize;
        var rowSize = (pixels.Width * bitsPerPixel / 8 + 3) & ~3; // Zeilen auf 4 Byte aufgefüllt
        var imageSize = (long)rowSize * pixels.Height;
        var pixelOffset = FileHeaderSize + headerSize;
        var fileSize = pixelOffset + imageSize;
        if (fileSize > uint.MaxValue)
        {
            throw new ImageCodecException(ImageCodecError.TooLarge, "Bild ist für BMP zu groß (über 4 GiB).");
        }

        var pelsPerMeter = (int)Math.Round(options.ResolutionDpi / 0.0254);
        var header = new byte[pixelOffset];
        var span = header.AsSpan();

        // BITMAPFILEHEADER
        span[0] = (byte)'B';
        span[1] = (byte)'M';
        BinaryPrimitives.WriteUInt32LittleEndian(span[2..], (uint)fileSize);
        BinaryPrimitives.WriteUInt32LittleEndian(span[10..], (uint)pixelOffset);

        // BITMAPINFOHEADER / BITMAPV4HEADER
        var info = span[FileHeaderSize..];
        BinaryPrimitives.WriteUInt32LittleEndian(info, (uint)headerSize);
        BinaryPrimitives.WriteInt32LittleEndian(info[4..], pixels.Width);
        BinaryPrimitives.WriteInt32LittleEndian(info[8..], pixels.Height); // positiv = Zeilen von unten nach oben
        BinaryPrimitives.WriteUInt16LittleEndian(info[12..], 1);
        BinaryPrimitives.WriteUInt16LittleEndian(info[14..], (ushort)bitsPerPixel);
        BinaryPrimitives.WriteUInt32LittleEndian(info[16..], hasAlpha ? BiBitfields : BiRgb);
        BinaryPrimitives.WriteUInt32LittleEndian(info[20..], (uint)imageSize);
        BinaryPrimitives.WriteInt32LittleEndian(info[24..], pelsPerMeter);
        BinaryPrimitives.WriteInt32LittleEndian(info[28..], pelsPerMeter);
        if (hasAlpha)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(info[40..], 0x00FF0000); // Rot
            BinaryPrimitives.WriteUInt32LittleEndian(info[44..], 0x0000FF00); // Grün
            BinaryPrimitives.WriteUInt32LittleEndian(info[48..], 0x000000FF); // Blau
            BinaryPrimitives.WriteUInt32LittleEndian(info[52..], 0xFF000000); // Alpha
            BinaryPrimitives.WriteUInt32LittleEndian(info[56..], LcsSrgb);
        }

        stream.Write(header);

        var row = new byte[rowSize];
        for (var y = pixels.Height - 1; y >= 0; y--)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var source = pixels.GetPixelRow(y);
            var o = 0;
            foreach (var p in source)
            {
                var (r, g, b, a) = p.ToStraight();
                row[o++] = b;
                row[o++] = g;
                row[o++] = r;
                if (hasAlpha)
                {
                    row[o++] = a;
                }
            }

            stream.Write(row);
        }
    }

    private static bool HasTransparency(PixelBuffer pixels)
    {
        for (var y = 0; y < pixels.Height; y++)
        {
            foreach (var p in pixels.GetPixelRow(y))
            {
                if (p.A != 255)
                {
                    return true;
                }
            }
        }

        return false;
    }
}
