using OrbitStudio.Domain.Imaging;

namespace OrbitStudio.Imaging.Codecs;

/// <summary>Beschreibung eines Dateiformats und seiner Fähigkeiten.</summary>
/// <param name="Id">Stabile Kennung, z. B. "png". Anzeigename kommt aus den Sprachressourcen (Format.&lt;Id&gt;).</param>
/// <param name="Extensions">Dateiendungen inkl. Punkt, kleingeschrieben; die erste ist die Standardendung.</param>
/// <param name="SupportsAlpha">False, wenn das Format keine Transparenz speichern kann (Export füllt dann Weiß auf).</param>
/// <param name="SupportsQuality">True, wenn ein Qualitätsparameter (1–100) wirkt.</param>
public sealed record ImageFileFormat(
    string Id,
    IReadOnlyList<string> Extensions,
    bool CanRead,
    bool CanWrite,
    bool SupportsAlpha,
    bool SupportsQuality)
{
    public string DefaultExtension => Extensions[0];
}

/// <summary>Parameter für das Schreiben einer Bilddatei.</summary>
/// <param name="Quality">1–100, nur für verlustbehaftete Formate (JPEG, WebP).</param>
/// <param name="ResolutionDpi">Auflösung, sofern das Format sie speichern kann.</param>
public sealed record EncodeOptions(int Quality = 92, double ResolutionDpi = 96.0)
{
    public const int MinQuality = 1;
    public const int MaxQuality = 100;

    public int ClampedQuality => Math.Clamp(Quality, MinQuality, MaxQuality);
}

/// <summary>Ergebnis eines Lesevorgangs.</summary>
/// <param name="ResolutionDpi">Aus der Datei gelesene Auflösung oder null, wenn nicht verfügbar.</param>
public sealed record DecodedImage(PixelBuffer Pixels, double? ResolutionDpi);

/// <summary>Fehlerklassen beim Lesen/Schreiben. Die Oberfläche übersetzt sie in deutsche Meldungen.</summary>
public enum ImageCodecError
{
    /// <summary>Dateiinhalt beschädigt, abgeschnitten oder kein gültiges Bild.</summary>
    Corrupt,

    /// <summary>Format oder Variante wird nicht unterstützt.</summary>
    Unsupported,

    /// <summary>Bildabmessungen oder Dateigröße über den erlaubten Grenzen.</summary>
    TooLarge,

    /// <summary>Nicht genug Arbeitsspeicher.</summary>
    OutOfMemory,

    /// <summary>Kodieren fehlgeschlagen.</summary>
    EncodeFailed,
}

public sealed class ImageCodecException(ImageCodecError error, string message, Exception? inner = null)
    : Exception(message, inner)
{
    public ImageCodecError Error { get; } = error;
}

/// <summary>
/// Vertrag für Importer/Exporter. Implementierungen sind zustandslos und threadsicher,
/// damit Lese- und Schreibvorgänge im Hintergrund laufen können.
/// </summary>
public interface IImageCodec
{
    ImageFileFormat Format { get; }

    /// <exception cref="ImageCodecException">Bei beschädigten, nicht unterstützten oder zu großen Bildern.</exception>
    DecodedImage Decode(Stream stream, CancellationToken cancellationToken = default);

    /// <exception cref="ImageCodecException">Wenn das Kodieren fehlschlägt.</exception>
    void Encode(PixelBuffer pixels, Stream stream, EncodeOptions options, CancellationToken cancellationToken = default);
}
