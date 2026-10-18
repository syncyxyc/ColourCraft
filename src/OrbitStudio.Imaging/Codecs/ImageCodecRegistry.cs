namespace OrbitStudio.Imaging.Codecs;

/// <summary>Verzeichnis der verfügbaren Codecs. Plugins können ab M10 weitere registrieren.</summary>
public sealed class ImageCodecRegistry
{
    /// <summary>Größte Datei, die gelesen wird. Schützt vor unkontrolliertem Speicherverbrauch.</summary>
    public const long MaxFileBytes = 1L << 30;

    private readonly List<IImageCodec> _codecs = [];

    public IReadOnlyList<IImageCodec> All => _codecs;

    public IEnumerable<IImageCodec> Readers => _codecs.Where(c => c.Format.CanRead);

    public IEnumerable<IImageCodec> Writers => _codecs.Where(c => c.Format.CanWrite);

    public void Register(IImageCodec codec)
    {
        ArgumentNullException.ThrowIfNull(codec);
        if (_codecs.Any(c => c.Format.Id == codec.Format.Id))
        {
            throw new InvalidOperationException($"Format '{codec.Format.Id}' ist bereits registriert.");
        }

        _codecs.Add(codec);
    }

    /// <summary>Findet den Codec zu einer Dateiendung oder einem Pfad (Groß-/Kleinschreibung egal).</summary>
    public IImageCodec? FindByPath(string path)
    {
        var extension = Path.GetExtension(path);
        if (string.IsNullOrEmpty(extension))
        {
            return null;
        }

        return _codecs.FirstOrDefault(c =>
            c.Format.Extensions.Contains(extension, StringComparer.OrdinalIgnoreCase));
    }

    public IImageCodec? FindById(string id) => _codecs.FirstOrDefault(c => c.Format.Id == id);

    /// <summary>Standardausstattung: PNG, JPEG, BMP, WebP (lesen/schreiben), GIF (nur lesen, erstes Bild).</summary>
    public static ImageCodecRegistry CreateDefault()
    {
        var registry = new ImageCodecRegistry();
        registry.Register(SkiaImageCodec.Png);
        registry.Register(SkiaImageCodec.Jpeg);
        registry.Register(new BmpCodec());
        registry.Register(SkiaImageCodec.WebP);
        registry.Register(SkiaImageCodec.Gif);
        return registry;
    }
}
