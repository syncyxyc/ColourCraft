using OrbitStudio.Domain.Documents;
using OrbitStudio.Domain.Imaging;
using SkiaSharp;

namespace OrbitStudio.Imaging.Codecs;

/// <summary>
/// Codec auf Basis von SkiaSharp. Lesen: Farbprofile werden nach sRGB umgerechnet, EXIF-Ausrichtung wird angewendet.
/// Schreiben: aus dem vormultiplizierten Puffer; Formate ohne Alpha werden vorher auf Weiß reduziert.
/// </summary>
public sealed class SkiaImageCodec : IImageCodec
{
    public static readonly SkiaImageCodec Png = new(
        new ImageFileFormat("png", [".png"], true, true, SupportsAlpha: true, SupportsQuality: false),
        SKEncodedImageFormat.Png);

    public static readonly SkiaImageCodec Jpeg = new(
        new ImageFileFormat("jpeg", [".jpg", ".jpeg", ".jpe", ".jfif"], true, true, SupportsAlpha: false, SupportsQuality: true),
        SKEncodedImageFormat.Jpeg);

    public static readonly SkiaImageCodec WebP = new(
        new ImageFileFormat("webp", [".webp"], true, true, SupportsAlpha: true, SupportsQuality: true),
        SKEncodedImageFormat.Webp);

    public static readonly SkiaImageCodec Gif = new(
        new ImageFileFormat("gif", [".gif"], true, false, SupportsAlpha: true, SupportsQuality: false),
        encodeFormat: null);

    private readonly SKEncodedImageFormat? _encodeFormat;

    private SkiaImageCodec(ImageFileFormat format, SKEncodedImageFormat? encodeFormat)
    {
        Format = format;
        _encodeFormat = encodeFormat;
    }

    public ImageFileFormat Format { get; }

    public DecodedImage Decode(Stream stream, CancellationToken cancellationToken = default) =>
        SkiaDecoder.Decode(stream, cancellationToken);

    public void Encode(PixelBuffer pixels, Stream stream, EncodeOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pixels);
        ArgumentNullException.ThrowIfNull(stream);
        if (_encodeFormat is not { } format)
        {
            throw new ImageCodecException(ImageCodecError.Unsupported, $"Format {Format.Id} kann nicht geschrieben werden.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        var source = Format.SupportsAlpha ? pixels : AlphaFlattening.OntoWhite(pixels);
        var alphaType = Format.SupportsAlpha ? SKAlphaType.Premul : SKAlphaType.Opaque;
        var info = new SKImageInfo(source.Width, source.Height, SKColorType.Bgra8888, alphaType, SKColorSpace.CreateSrgb());

        SKData? data;
        unsafe
        {
            fixed (byte* ptr = source.Data)
            {
                using var pixmap = new SKPixmap(info, (IntPtr)ptr, source.Stride);
                data = pixmap.Encode(format, Format.SupportsQuality ? options.ClampedQuality : 100);
            }
        }

        using (data)
        {
            if (data is null)
            {
                throw new ImageCodecException(ImageCodecError.EncodeFailed, $"{Format.Id}: Kodieren fehlgeschlagen.");
            }

            cancellationToken.ThrowIfCancellationRequested();
            data.SaveTo(stream);
        }
    }
}

/// <summary>Gemeinsames Lesen aller von Skia unterstützten Formate.</summary>
internal static class SkiaDecoder
{
    public static DecodedImage Decode(Stream stream, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);
        cancellationToken.ThrowIfCancellationRequested();

        using var data = SKData.Create(stream)
                         ?? throw new ImageCodecException(ImageCodecError.Corrupt, "Datei ist leer oder nicht lesbar.");
        using var codec = SKCodec.Create(data)
                          ?? throw new ImageCodecException(ImageCodecError.Corrupt, "Kein gültiges oder unterstütztes Bild.");

        // Abmessungen vor dem Dekodieren prüfen: übergroße Bilder werden nie in den Speicher geladen.
        var size = codec.Info;
        var (width, height) = IsRotated(codec.EncodedOrigin) ? (size.Height, size.Width) : (size.Width, size.Height);
        var sizeCheck = DocumentLimits.ValidateSize(width, height);
        if (sizeCheck == SizeValidationResult.NonPositive)
        {
            throw new ImageCodecException(ImageCodecError.Corrupt, "Bild hat keine gültigen Abmessungen.");
        }

        if (sizeCheck != SizeValidationResult.Valid)
        {
            throw new ImageCodecException(ImageCodecError.TooLarge, $"Bild ist zu groß ({size.Width}×{size.Height}).");
        }

        PixelBuffer decoded;
        try
        {
            decoded = PixelBuffer.Create(size.Width, size.Height);
        }
        catch (OutOfMemoryException ex)
        {
            throw new ImageCodecException(ImageCodecError.OutOfMemory, "Nicht genug Arbeitsspeicher.", ex);
        }

        var target = new SKImageInfo(size.Width, size.Height, SKColorType.Bgra8888, SKAlphaType.Premul,
            SKColorSpace.CreateSrgb());
        SKCodecResult result;
        unsafe
        {
            fixed (byte* ptr = decoded.Data)
            {
                result = codec.GetPixels(target, (IntPtr)ptr);
            }
        }

        // Unvollständige Daten gelten als beschädigt: keine stillschweigend halbfertigen Bilder.
        if (result != SKCodecResult.Success)
        {
            var error = result is SKCodecResult.Unimplemented or SKCodecResult.InvalidConversion
                ? ImageCodecError.Unsupported
                : ImageCodecError.Corrupt;
            throw new ImageCodecException(error, $"Bild konnte nicht dekodiert werden ({result}).");
        }

        cancellationToken.ThrowIfCancellationRequested();
        return new DecodedImage(Orientation.Apply(decoded, codec.EncodedOrigin), null);
    }

    private static bool IsRotated(SKEncodedOrigin origin) =>
        origin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop or SKEncodedOrigin.RightBottom
            or SKEncodedOrigin.LeftBottom;
}

/// <summary>Wendet die EXIF-Ausrichtung (1–8) an, damit das Bild so erscheint, wie die Kamera es gemeint hat.</summary>
public static class Orientation
{
    public static PixelBuffer Apply(PixelBuffer pixels, SKEncodedOrigin origin) => origin switch
    {
        SKEncodedOrigin.TopRight => PixelTransforms.FlipHorizontal(pixels),
        SKEncodedOrigin.BottomRight => PixelTransforms.Rotate180(pixels),
        SKEncodedOrigin.BottomLeft => PixelTransforms.FlipVertical(pixels),
        SKEncodedOrigin.LeftTop => PixelTransforms.Transpose(pixels),
        SKEncodedOrigin.RightTop => PixelTransforms.Rotate90Clockwise(pixels),
        SKEncodedOrigin.RightBottom => PixelTransforms.Transverse(pixels),
        SKEncodedOrigin.LeftBottom => PixelTransforms.Rotate90CounterClockwise(pixels),
        _ => pixels,
    };
}

/// <summary>Reduziert Transparenz auf einen weißen Hintergrund (für Formate ohne Alpha).</summary>
public static class AlphaFlattening
{
    /// <summary>
    /// Vormultipliert gilt: Ergebnis = Quelle + Weiß · (1 − α) = Kanal + (255 − A). Exakt, ohne Rundungsfehler.
    /// </summary>
    public static PixelBuffer OntoWhite(PixelBuffer source)
    {
        var result = source.Clone();
        for (var y = 0; y < result.Height; y++)
        {
            var row = result.GetPixelRow(y);
            for (var x = 0; x < row.Length; x++)
            {
                var p = row[x];
                var add = 255 - p.A;
                row[x] = new Bgra32((byte)(p.B + add), (byte)(p.G + add), (byte)(p.R + add), 255);
            }
        }

        return result;
    }
}
