using OrbitStudio.Domain.Imaging;
using OrbitStudio.Domain.Layers;
using OrbitStudio.Domain.Selection;

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
/// Ein geöffnetes Bilddokument mit Ebenenbaum. Jedes Dokument besitzt seinen eigenen Zustand;
/// zwischen Dokumenten wird nichts geteilt. Alle Ebenen haben die Größe des Dokuments.
/// </summary>
/// <remarks>
/// Änderungen an Ebenen werden über <see cref="CommitLayerChange"/> bzw. <see cref="CommitPixelChange"/> gemeldet,
/// damit Ansicht und (ab M4) Historie reagieren können.
/// </remarks>
public sealed class ImageDocument
{
    /// <summary>Aktuelle Version des Dokumentmodells, wird ins Projektformat übernommen.</summary>
    public const int CurrentFormatVersion = 1;

    public const double DefaultResolutionDpi = 96.0;

    /// <summary>Interner Name der Wurzelgruppe (nie sichtbar).</summary>
    public const string RootGroupName = "root";

    private string _name;
    private bool _isDirty;
    private LayerNode? _activeLayer;

    private ImageDocument(string name, int width, int height, double resolutionDpi, DocumentBackground background)
    {
        Id = Guid.NewGuid();
        _name = name;
        Width = width;
        Height = height;
        ResolutionDpi = resolutionDpi;
        Background = background;
        Root = new LayerGroup(RootGroupName);
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

    public int Width { get; private set; }

    public int Height { get; private set; }

    public IntRect Bounds => new(0, 0, Width, Height);

    /// <summary>Aktuelle Auswahl oder null (keine Auswahl = das ganze Bild ist bearbeitbar).</summary>
    public SelectionMask? Selection { get; private set; }

    public bool HasSelection => Selection is not null;

    /// <summary>Auswahl hat sich geändert.</summary>
    public event EventHandler? SelectionChanged;

    /// <summary>Bildgröße hat sich geändert (Bildgröße, Leinwand, Drehen, Zuschneiden).</summary>
    public event EventHandler? SizeChanged;

    /// <summary>
    /// Setzt die Auswahl. Eine leere Maske wird als „keine Auswahl" gespeichert. Die Maske wird übernommen
    /// (nicht kopiert) und darf danach nicht mehr verändert werden.
    /// </summary>
    public void SetSelection(SelectionMask? selection)
    {
        if (selection is not null && (selection.Width != Width || selection.Height != Height))
        {
            throw new ArgumentException("Die Auswahl muss die Größe des Dokuments haben.", nameof(selection));
        }

        Selection = selection is null || selection.IsEmpty ? null : selection;
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Bearbeitungsfaktor eines Pixels (0–255): Auswahlwert oder 255 ohne Auswahl.</summary>
    public byte EditableAmount(int x, int y) => Selection?.Get(x, y) ?? 255;

    /// <summary>Schnappschuss von Größe, Pixelpuffern, Masken und Auswahl (Referenzen, keine Kopien).</summary>
    public DocumentContent CaptureContent() => new(Width, Height,
        PixelLayers.ToDictionary(l => l, l => (l.Pixels, l.Mask)), Selection);

    /// <summary>
    /// Ersetzt Größe und Inhalt aller Pixelebenen auf einmal (Bildgröße, Leinwand, Drehen, Zuschneiden, Rückgängig).
    /// Jede Pixelebene des Dokuments muss enthalten sein; alle Puffer und Masken müssen die neue Größe haben.
    /// </summary>
    public void ReplaceContent(DocumentContent content)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (DocumentLimits.ValidateSize(content.Width, content.Height) is var check && check != SizeValidationResult.Valid)
        {
            throw new DocumentValidationException(check);
        }

        foreach (var layer in PixelLayers)
        {
            if (!content.Layers.TryGetValue(layer, out var entry))
            {
                throw new ArgumentException($"Inhalt für Ebene „{layer.Name}“ fehlt.", nameof(content));
            }

            if (entry.Pixels.Width != content.Width || entry.Pixels.Height != content.Height
                || (entry.Mask is { } m && (m.Width != content.Width || m.Height != content.Height)))
            {
                throw new ArgumentException("Puffergröße passt nicht zur neuen Dokumentgröße.", nameof(content));
            }
        }

        var sizeChanged = Width != content.Width || Height != content.Height;
        Width = content.Width;
        Height = content.Height;
        foreach (var (layer, entry) in content.Layers)
        {
            layer.Pixels = entry.Pixels;
            layer.Mask = entry.Mask;
        }

        Selection = content.Selection;
        if (sizeChanged)
        {
            SizeChanged?.Invoke(this, EventArgs.Empty);
        }

        SelectionChanged?.Invoke(this, EventArgs.Empty);
        CommitLayerChange();
    }

    /// <summary>Wurzel des Ebenenbaums. Kinder von unten (Index 0) nach oben.</summary>
    public LayerGroup Root { get; }

    /// <summary>Ausgewählte Ebene oder Gruppe; Ziel der Ebenenbefehle.</summary>
    public LayerNode? ActiveLayer
    {
        get => _activeLayer;
        set
        {
            if (value is not null && !Contains(value))
            {
                throw new InvalidOperationException("Die Ebene gehört nicht zu diesem Dokument.");
            }

            if (_activeLayer != value)
            {
                _activeLayer = value;
                ActiveLayerChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    /// <summary>Aktive Ebene, falls es eine Pixelebene ist.</summary>
    public PixelLayer? ActivePixelLayer => _activeLayer as PixelLayer;

    public IEnumerable<PixelLayer> PixelLayers => Root.PixelLayers();

    /// <summary>Anzahl aller Ebenen und Gruppen.</summary>
    public int LayerCount => Root.Descendants().Count();

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

    /// <summary>Name, Speicherzustand oder Änderungsmarkierung haben sich geändert.</summary>
    public event EventHandler? StateChanged;

    /// <summary>Pixel einer Ebene haben sich im angegebenen Bereich geändert.</summary>
    public event EventHandler<IntRect>? PixelsChanged;

    /// <summary>Ebenenstruktur oder Ebeneneigenschaften (Sichtbarkeit, Deckkraft, …) haben sich geändert.</summary>
    public event EventHandler? LayersChanged;

    public event EventHandler? ActiveLayerChanged;

    /// <param name="backgroundLayerName">Name der ersten Ebene (sprachabhängig, kommt aus der Oberfläche).</param>
    public static ImageDocument Create(string name, int width, int height, string backgroundLayerName,
        double resolutionDpi = DefaultResolutionDpi, DocumentBackground background = DocumentBackground.White)
    {
        ValidateName(name);
        ValidateResolution(resolutionDpi);
        var pixels = PixelBuffer.Create(width, height);
        if (background == DocumentBackground.White)
        {
            pixels.Fill(Bgra32.White);
        }

        var document = new ImageDocument(name, width, height, resolutionDpi, background);
        var layer = new PixelLayer(backgroundLayerName, pixels);
        document.Root.Add(layer);
        document._activeLayer = layer;
        return document;
    }

    /// <summary>Dokument aus vorhandenen Pixeln, z. B. aus einer geöffneten Bilddatei. Der Puffer wird übernommen.</summary>
    public static ImageDocument FromPixels(string name, PixelBuffer pixels, string layerName, string? sourcePath = null,
        double resolutionDpi = DefaultResolutionDpi)
    {
        ValidateName(name);
        ValidateResolution(resolutionDpi);
        ArgumentNullException.ThrowIfNull(pixels);
        var document = new ImageDocument(name, pixels.Width, pixels.Height, resolutionDpi, DocumentBackground.Transparent)
        {
            SourcePath = sourcePath,
        };
        var layer = new PixelLayer(layerName, pixels);
        document.Root.Add(layer);
        document._activeLayer = layer;
        return document;
    }

    /// <summary>True, wenn der Knoten im Ebenenbaum dieses Dokuments liegt.</summary>
    public bool Contains(LayerNode node)
    {
        for (LayerNode? n = node; n is not null; n = n.Parent)
        {
            if (n == Root)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Meldet eine Pixeländerung: markiert das Dokument als geändert und informiert die Ansicht.</summary>
    public void CommitPixelChange(IntRect region)
    {
        var clipped = region.Intersect(Bounds);
        if (clipped.IsEmpty)
        {
            return;
        }

        PixelsChanged?.Invoke(this, clipped);
        MarkDirty();
    }

    /// <summary>Meldet eine Änderung am Ebenenbaum oder an Ebeneneigenschaften.</summary>
    public void CommitLayerChange()
    {
        if (_activeLayer is not null && !Contains(_activeLayer))
        {
            _activeLayer = null;
            ActiveLayerChanged?.Invoke(this, EventArgs.Empty);
        }

        LayersChanged?.Invoke(this, EventArgs.Empty);
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

/// <summary>Größe und Inhalt eines Dokuments zu einem Zeitpunkt (Referenzen auf Puffer, keine Kopien).</summary>
public sealed record DocumentContent(
    int Width,
    int Height,
    IReadOnlyDictionary<PixelLayer, (PixelBuffer Pixels, LayerMask? Mask)> Layers,
    SelectionMask? Selection)
{
    /// <summary>Ungefährer Speicherbedarf in Byte (für die Begrenzung der Historie).</summary>
    public long SizeBytes => Layers.Values.Sum(v => (long)v.Pixels.Data.Length + (v.Mask is null ? 0 : v.Mask.Data.Length))
                             + (Selection?.Data.Length ?? 0);
}

/// <summary>Wird ausgelöst, wenn ein Dokument mit ungültigen Abmessungen angelegt werden soll.</summary>
public sealed class DocumentValidationException(SizeValidationResult reason)
    : Exception($"Ungültige Dokumentgröße: {reason}")
{
    public SizeValidationResult Reason { get; } = reason;
}
