using Microsoft.Extensions.Logging;
using OrbitStudio.Domain.Documents;
using OrbitStudio.Domain.Imaging;
using OrbitStudio.Imaging.Codecs;

namespace OrbitStudio.Application.Files;

/// <summary>Fehlerarten bei Dateioperationen. Die Oberfläche übersetzt sie in deutsche Meldungen.</summary>
public enum FileError
{
    None,
    NotFound,
    AccessDenied,
    IoError,
    UnknownFormat,
    FormatNotWritable,
    Corrupt,
    Unsupported,
    TooLarge,
    OutOfMemory,
    EncodeFailed,
    Cancelled,
}

public sealed record OpenImageResult(ImageDocument? Document, FileError Error, string Path)
{
    public bool Success => Error == FileError.None && Document is not null;
}

public sealed record LoadPixelsResult(PixelBuffer? Pixels, double? ResolutionDpi, FileError Error)
{
    public bool Success => Error == FileError.None && Pixels is not null;
}

public sealed record ExportResult(FileError Error, string Path)
{
    public bool Success => Error == FileError.None;
}

/// <summary>
/// Öffnen und Exportieren von Bilddateien. Dekodieren und Kodieren laufen im Hintergrund und sind abbrechbar.
/// Exportieren schreibt zuerst in eine temporäre Datei im Zielordner und ersetzt dann die Zieldatei,
/// damit ein Abbruch nie eine vorhandene Datei zerstört.
/// </summary>
public sealed class ImageFileService(ImageCodecRegistry codecs, ILogger<ImageFileService> logger)
{
    public ImageCodecRegistry Codecs => codecs;

    /// <summary>Öffnet eine Bilddatei als neues Dokument mit einer Ebene.</summary>
    /// <param name="layerName">Name der Ebene (sprachabhängig, kommt aus der Oberfläche).</param>
    public async Task<OpenImageResult> OpenAsync(string path, string layerName,
        CancellationToken cancellationToken = default)
    {
        var loaded = await LoadPixelsAsync(path, cancellationToken).ConfigureAwait(true);
        if (!loaded.Success)
        {
            return new OpenImageResult(null, loaded.Error, path);
        }

        var dpi = loaded.ResolutionDpi is { } d && DocumentLimits.IsValidResolution(d) ? d : ImageDocument.DefaultResolutionDpi;
        var document = ImageDocument.FromPixels(Path.GetFileName(path), loaded.Pixels!, layerName, path, dpi);
        return new OpenImageResult(document, FileError.None, path);
    }

    /// <summary>Liest nur die Pixel einer Bilddatei (z. B. für „Importieren als Ebene").</summary>
    public async Task<LoadPixelsResult> LoadPixelsAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var codec = codecs.FindByPath(path);
        if (codec is null || !codec.Format.CanRead)
        {
            return new LoadPixelsResult(null, null, FileError.UnknownFormat);
        }

        try
        {
            var decoded = await Task.Run(() =>
            {
                var info = new FileInfo(path);
                if (!info.Exists)
                {
                    throw new FileNotFoundException(null, path);
                }

                if (info.Length > ImageCodecRegistry.MaxFileBytes)
                {
                    throw new ImageCodecException(ImageCodecError.TooLarge, "Datei ist zu groß.");
                }

                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                return codec.Decode(stream, cancellationToken);
            }, cancellationToken).ConfigureAwait(true);

            logger.LogInformation("Bild gelesen: Format {Format}, {Width}x{Height}", codec.Format.Id,
                decoded.Pixels.Width, decoded.Pixels.Height);
            return new LoadPixelsResult(decoded.Pixels, decoded.ResolutionDpi, FileError.None);
        }
        catch (Exception ex) when (Classify(ex) is { } error)
        {
            logger.LogWarning(ex, "Lesen fehlgeschlagen ({Error}): {File}", error, Path.GetFileName(path));
            return new LoadPixelsResult(null, null, error);
        }
    }

    /// <summary>
    /// Exportiert die Pixel in das Format, das zur Dateiendung passt. Ohne <paramref name="isSnapshot"/> wird der
    /// Puffer vorab kopiert, damit spätere Bearbeitungen den laufenden Export nicht beeinflussen.
    /// </summary>
    public async Task<ExportResult> ExportAsync(PixelBuffer pixels, string path, EncodeOptions options,
        CancellationToken cancellationToken = default, bool isSnapshot = false)
    {
        ArgumentNullException.ThrowIfNull(pixels);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var codec = codecs.FindByPath(path);
        if (codec is null)
        {
            return new ExportResult(FileError.UnknownFormat, path);
        }

        if (!codec.Format.CanWrite)
        {
            return new ExportResult(FileError.FormatNotWritable, path);
        }

        var snapshot = isSnapshot ? pixels : pixels.Clone();
        var directory = Path.GetDirectoryName(Path.GetFullPath(path))!;
        var tempPath = Path.Combine(directory, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        try
        {
            await Task.Run(() =>
            {
                using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    codec.Encode(snapshot, stream, options, cancellationToken);
                    stream.Flush(flushToDisk: true);
                }

                cancellationToken.ThrowIfCancellationRequested();
                File.Move(tempPath, path, overwrite: true);
            }, cancellationToken).ConfigureAwait(true);

            logger.LogInformation("Exportiert: Format {Format}, {Width}x{Height}", codec.Format.Id, snapshot.Width,
                snapshot.Height);
            return new ExportResult(FileError.None, path);
        }
        catch (Exception ex) when (Classify(ex) is { } error)
        {
            logger.LogWarning(ex, "Export fehlgeschlagen ({Error}): {File}", error, Path.GetFileName(path));
            return new ExportResult(error, path);
        }
        finally
        {
            TryDelete(tempPath);
        }
    }

    /// <summary>True, wenn mindestens ein Pixel nicht vollständig deckend ist.</summary>
    public static bool HasTransparency(PixelBuffer pixels)
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

    private static FileError? Classify(Exception ex) => ex switch
    {
        OperationCanceledException => FileError.Cancelled,
        FileNotFoundException or DirectoryNotFoundException => FileError.NotFound,
        UnauthorizedAccessException => FileError.AccessDenied,
        ImageCodecException codec => codec.Error switch
        {
            ImageCodecError.Corrupt => FileError.Corrupt,
            ImageCodecError.Unsupported => FileError.Unsupported,
            ImageCodecError.TooLarge => FileError.TooLarge,
            ImageCodecError.OutOfMemory => FileError.OutOfMemory,
            _ => FileError.EncodeFailed,
        },
        DocumentValidationException => FileError.TooLarge,
        OutOfMemoryException => FileError.OutOfMemory,
        IOException => FileError.IoError,
        _ => null,
    };

    private void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Temporäre Datei konnte nicht entfernt werden.");
        }
    }
}
