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
public sealed class ImageDocument
{
    /// <summary>Aktuelle Version des Dokumentmodells, wird ins Projektformat übernommen.</summary>
    public const int CurrentFormatVersion = 1;

    private string _name;
    private bool _isDirty;

    private ImageDocument(string name, int width, int height, double resolutionDpi, DocumentBackground background)
    {
        Id = Guid.NewGuid();
        _name = name;
        Width = width;
        Height = height;
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

    public int Width { get; }

    public int Height { get; }

    public double ResolutionDpi { get; }

    public ColorMode ColorMode => ColorMode.Bgra8;

    public DocumentBackground Background { get; }

    /// <summary>Speicherpfad, falls das Dokument bereits gespeichert oder aus einer Datei geöffnet wurde.</summary>
    public string? FilePath { get; private set; }

    public int FormatVersion { get; } = CurrentFormatVersion;

    /// <summary>True, wenn ungespeicherte Änderungen vorliegen.</summary>
    public bool IsDirty => _isDirty;

    public event EventHandler? StateChanged;

    public static ImageDocument Create(string name, int width, int height, double resolutionDpi = 96.0,
        DocumentBackground background = DocumentBackground.White)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Der Dokumentname darf nicht leer sein.", nameof(name));
        }

        var sizeResult = DocumentLimits.ValidateSize(width, height);
        if (sizeResult != SizeValidationResult.Valid)
        {
            throw new DocumentValidationException(sizeResult);
        }

        if (!DocumentLimits.IsValidResolution(resolutionDpi))
        {
            throw new ArgumentOutOfRangeException(nameof(resolutionDpi), resolutionDpi, "Ungültige Auflösung.");
        }

        return new ImageDocument(name, width, height, resolutionDpi, background);
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
}

/// <summary>Wird ausgelöst, wenn ein Dokument mit ungültigen Abmessungen angelegt werden soll.</summary>
public sealed class DocumentValidationException(SizeValidationResult reason)
    : Exception($"Ungültige Dokumentgröße: {reason}")
{
    public SizeValidationResult Reason { get; } = reason;
}
