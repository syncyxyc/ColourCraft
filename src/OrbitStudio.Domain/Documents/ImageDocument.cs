using OrbitStudio.Domain.Imaging;

namespace OrbitStudio.Domain.Documents;

/// <summary>Farbmodus eines Dokuments. Intern wird ausschließlich BGRA mit 8 Bit je Kanal verwendet.</summary>
public enum ColorMode
{
    Bgra8,
}

/// <summary>Hintergrund eines neu angelegten Dokuments.</summary>
public enum DocumentBackground
{
    White,
    Transparent,
}

/// <summary>
/// Ein geöffnetes Bilddokument. Jedes Dokument besitzt seinen eigenen Zustand;
/// zwischen Dokumenten wird nichts geteilt.
/// </summary>
/// <remarks>
/// Bis M3 besitzt ein Dokument genau einen Pixelpuffer. Mit dem Ebenenmodell (M3) wird daraus die unterste Ebene.
/// </remarks>
public sealed class ImageDocument
{
    /// <summary>Aktuelle Version des Dokumentmodells, wird ins Projektformat übernommen.</summary>
    public const int CurrentFormatVersion = 1;

    public const double DefaultResolutionDpi = 96.0;

    private string _name;
    private bool _isDirty;

    private ImageDocument(string name, PixelBuffer pixels, double resolutionDpi, DocumentBackground background)
    {
        Id = Guid.NewGuid();
        _name = name;
        Pixels = pixels;
        ResolutionDpi = resolutionDpi;
        Background = background;
    }

    public Guid Id { get; }

    public string Name
    {
        get => _name;
        set
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Der Dokumentname darf nicht leer sein.", nameof(value));
            }

            if (_name != value)
            {
                _name = value;
                StateChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public int Width => Pixels.Width;

    public int Height => Pixels.Height;

    /// <summary>Bildinhalt. Änderungen müssen über <see cref="CommitPixelChange"/> gemeldet werden.</summary>
    public PixelBuffer Pixels { get; }

    public double ResolutionDpi { get; }

    public ColorMode ColorMode => ColorMode.Bgra8;

    public DocumentBackground Background { get; }

    /// <summary>Speicherpfad des Projekts (.orbit), falls bereits gespeichert.</summary>
    public string? FilePath { get; private set; }

    /// <summary>Bilddatei, aus der das Dokument geöffnet wurde (PNG, JPEG, …), sonst null.</summary>
    public string? SourcePath { get; private set; }

    public int FormatVersion { get; } = CurrentFormatVersion;

    /// <summary>True, wenn ungespeicherte Änderungen vorliegen.</summary>
    public bool IsDirty => _isDirty;

    public event EventHandler? StateChanged;

    /// <summary>Pixel im angegebenen Bereich haben sich geändert (für inkrementelles Neuzeichnen).</summary>
    public event EventHandler<IntRect>? PixelsChanged;

    public static ImageDocument Create(string name, int width, int height, double resolutionDpi = DefaultResolutionDpi,
        DocumentBackground background = DocumentBackground.White)
    {
        ValidateName(name);
        ValidateResolution(resolutionDpi);
        var pixels = PixelBuffer.Create(width, height);
        if (background == DocumentBackground.White)
        {
            pixels.Fill(Bgra32.White);
        }

        return new ImageDocument(name, pixels, resolutionDpi, background);
    }

    /// <summary>Dokument aus vorhandenen Pixeln, z. B. aus einer geöffneten Bilddatei. Der Puffer wird übernommen.</summary>
    public static ImageDocument FromPixels(string name, PixelBuffer pixels, string? sourcePath = null,
        double resolutionDpi = DefaultResolutionDpi)
    {
        ValidateName(name);
        ValidateResolution(resolutionDpi);
        ArgumentNullException.ThrowIfNull(pixels);
        return new ImageDocument(name, pixels, resolutionDpi, DocumentBackground.Transparent) { SourcePath = sourcePath };
    }

    /// <summary>Meldet eine Pixeländerung: markiert das Dokument als geändert und informiert die Ansicht.</summary>
    public void CommitPixelChange(IntRect region)
    {
        var clipped = region.Intersect(Pixels.Bounds);
        if (clipped.IsEmpty)
        {
            return;
        }

        PixelsChanged?.Invoke(this, clipped);
        MarkDirty();
    }

    public void MarkDirty()
    {
        if (!_isDirty)
        {
            _isDirty = true;
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void MarkSaved(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        FilePath = filePath;
        _isDirty = false;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Der Dokumentname darf nicht leer sein.", nameof(name));
        }
    }

    private static void ValidateResolution(double dpi)
    {
        if (!DocumentLimits.IsValidResolution(dpi))
        {
            throw new ArgumentOutOfRangeException(nameof(dpi), dpi, "Ungültige Auflösung.");
        }
    }
}

/// <summary>Wird ausgelöst, wenn ein Dokument mit ungültigen Abmessungen angelegt werden soll.</summary>
public sealed class DocumentValidationException(SizeValidationResult reason)
    : Exception($"Ungültige Dokumentgröße: {reason}")
{
    public SizeValidationResult Reason { get; } = reason;
}
