using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using OrbitStudio.Domain.Documents;
using OrbitStudio.Domain.Imaging;
using OrbitStudio.Imaging.Codecs;

namespace OrbitStudio.Presentation.Services;

/// <summary>Ergebnis eines Zugriffs auf die Zwischenablage.</summary>
public enum ClipboardStatus
{
    Ok,
    Empty,
    Failed,
    TooLarge,
}

/// <summary>Zugriff auf die Zwischenablage für Bilder (austauschbar für Tests).</summary>
public interface IClipboardService
{
    ClipboardStatus SetImage(PixelBuffer pixels);

    (PixelBuffer? Pixels, ClipboardStatus Status) GetImage();

    bool ContainsImage { get; }
}

/// <summary>
/// Windows-Zwischenablage. Schreibt das Bild zweifach: als „PNG" (mit Transparenz, für Bildprogramme)
/// und als Standard-Bitmap (für alle anderen Programme). Liest bevorzugt „PNG".
/// </summary>
public sealed class WpfClipboardService(ImageCodecRegistry codecs) : IClipboardService
{
    private const string PngFormat = "PNG";

    public bool ContainsImage
    {
        get
        {
            try
            {
                return Clipboard.ContainsData(PngFormat) || Clipboard.ContainsImage();
            }
            catch (COMException)
            {
                return false;
            }
        }
    }

    public ClipboardStatus SetImage(PixelBuffer pixels)
    {
        try
        {
            var png = new MemoryStream();
            codecs.FindById("png")!.Encode(pixels, png, new EncodeOptions());
            png.Position = 0;

            var data = new DataObject();
            data.SetData(PngFormat, png, autoConvert: false);
            data.SetImage(ToBitmapSource(pixels));
            Clipboard.SetDataObject(data, copy: true);
            return ClipboardStatus.Ok;
        }
        catch (Exception ex) when (ex is COMException or ExternalException)
        {
            return ClipboardStatus.Failed;
        }
    }

    public (PixelBuffer? Pixels, ClipboardStatus Status) GetImage()
    {
        try
        {
            if (Clipboard.GetData(PngFormat) is MemoryStream png)
            {
                return (codecs.FindById("png")!.Decode(png).Pixels, ClipboardStatus.Ok);
            }

            if (Clipboard.GetImage() is { } image)
            {
                return FromBitmapSource(image);
            }

            return (null, ClipboardStatus.Empty);
        }
        catch (ImageCodecException ex) when (ex.Error == ImageCodecError.TooLarge)
        {
            return (null, ClipboardStatus.TooLarge);
        }
        catch (Exception ex) when (ex is COMException or ExternalException or ImageCodecException)
        {
            return (null, ClipboardStatus.Failed);
        }
    }

    private static BitmapSource ToBitmapSource(PixelBuffer pixels)
    {
        var bitmap = BitmapSource.Create(pixels.Width, pixels.Height, 96, 96, PixelFormats.Pbgra32, null,
            pixels.Data.ToArray(), pixels.Stride);
        bitmap.Freeze();
        return bitmap;
    }

    private static (PixelBuffer?, ClipboardStatus) FromBitmapSource(BitmapSource source)
    {
        if (DocumentLimits.ValidateSize(source.PixelWidth, source.PixelHeight) != SizeValidationResult.Valid)
        {
            return (null, ClipboardStatus.TooLarge);
        }

        var converted = new FormatConvertedBitmap(source, PixelFormats.Pbgra32, null, 0);
        var buffer = PixelBuffer.Create(converted.PixelWidth, converted.PixelHeight);
        var data = new byte[buffer.Stride * buffer.Height];
        converted.CopyPixels(data, buffer.Stride, 0);
        data.CopyTo(buffer.Data);
        return (buffer, ClipboardStatus.Ok);
    }
}
