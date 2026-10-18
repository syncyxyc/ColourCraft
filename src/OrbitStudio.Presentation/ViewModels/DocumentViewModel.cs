using System.Collections.ObjectModel;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using OrbitStudio.Domain.Documents;
using OrbitStudio.Domain.Imaging;
using OrbitStudio.Domain.Layers;
using OrbitStudio.Layers;
using OrbitStudio.Presentation.Localization;
using OrbitStudio.Presentation.Viewport;
using OrbitStudio.Rendering;

namespace OrbitStudio.Presentation.ViewModels;

/// <summary>
/// Ansicht eines Dokuments in einem Tab: Ansichtstransformation, zusammengesetztes Bild (Cache) und Ebenenliste.
/// Ansichtszustand gehört zum Dokument, nicht global – beim Tabwechsel bleibt er je Dokument erhalten.
/// </summary>
public sealed partial class DocumentViewModel : ObservableObject
{
    private PixelBuffer? _composite;
    private WriteableBitmap? _bitmap;
    private bool _syncingSelection;

    public DocumentViewModel(ImageDocument document)
    {
        Document = document;
        View = new ViewTransform(document.Width, document.Height);
        View.Changed += (_, _) =>
        {
            OnPropertyChanged(nameof(ZoomText));
            ViewChanged?.Invoke(this, EventArgs.Empty);
        };
        document.StateChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(Title));
            OnPropertyChanged(nameof(IsDirty));
        };
        document.PixelsChanged += (_, region) => Recomposite(region);
        document.LayersChanged += (_, _) =>
        {
            Recomposite(document.Bounds);
            SyncLayerItems();
        };
        document.ActiveLayerChanged += (_, _) => SyncSelection();
        document.SelectionChanged += (_, _) => UpdateSelectionOutline();
        document.SizeChanged += (_, _) => OnDocumentSizeChanged();
        SyncLayerItems();
        UpdateSelectionOutline();
    }

    /// <summary>Ausführer für Ebenenänderungen aus dem Panel (mit Historie, vom Hauptfenster gesetzt).</summary>
    public ILayerEditExecutor LayerEdits { get; set; } = DirectLayerEditExecutor.Instance;

    public ImageDocument Document { get; }

    /// <summary>Koordinatentransformation dieser Ansicht.</summary>
    public ViewTransform View { get; }

    /// <summary>True, sobald die erste Anpassung an das Fenster erfolgt ist.</summary>
    public bool IsInitialViewApplied { get; set; }

    /// <summary>Zusammengesetztes Bild aller sichtbaren Ebenen (Cache, nur bereichsweise aktualisiert).</summary>
    public PixelBuffer Composite => _composite ??= LayerCompositor.Composite(Document);

    /// <summary>Darstellung des Komposits. Wird beim ersten Zugriff erzeugt und danach bereichsweise aktualisiert.</summary>
    public WriteableBitmap Bitmap => _bitmap ??= BitmapBridge.Create(Composite);

    /// <summary>Neu zeichnen nötig (Zoom, Verschiebung oder Pixel geändert).</summary>
    public event EventHandler? ViewChanged;

    public string Title => Document.IsDirty ? Document.Name + Loc.Get("Common.UnsavedSuffix") : Document.Name;

    public bool IsDirty => Document.IsDirty;

    public int Width => Document.Width;

    public int Height => Document.Height;

    /// <summary>Kanten der Auswahl (Dokumentkoordinaten) für die Auswahlkontur; leer ohne Auswahl.</summary>
    public IReadOnlyList<(PointD A, PointD B)> SelectionOutline { get; private set; } = [];

    public string SelectionText => Document.Selection?.Bounds() is { IsEmpty: false } b
        ? Loc.Format("Status.Selection", b.Width, b.Height)
        : Loc.Get("Status.NoSelection");

    public double Zoom => View.Zoom;

    public string ZoomText => Loc.Format("Status.Zoom", View.Zoom * 100);

    public string SizeText => Loc.Format("Status.Size", Width, Height);

    [ObservableProperty]
    private string _cursorText = Loc.Get("Status.CursorOutside");

    // --- Ebenen ---------------------------------------------------------------------------------

    /// <summary>Ebenen für das Panel: oberste zuerst, jede Gruppe direkt über ihren Kindern.</summary>
    public ObservableCollection<LayerItemViewModel> Layers { get; } = [];

    public IReadOnlyList<BlendModeOption> BlendModes => BlendModeOption.All;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedLayer), nameof(SelectedOpacityPercent), nameof(SelectedBlendMode))]
    private LayerItemViewModel? _selectedLayer;

    partial void OnSelectedLayerChanged(LayerItemViewModel? value)
    {
        if (!_syncingSelection && value is not null && Document.ActiveLayer != value.Node)
        {
            Document.ActiveLayer = value.Node;
        }
    }

    public bool HasSelectedLayer => SelectedLayer is not null;

    /// <summary>Deckkraft der aktiven Ebene in Prozent (Schieberegler im Panel).</summary>
    public double SelectedOpacityPercent
    {
        get => Document.ActiveLayer is { } layer ? Math.Round(layer.Opacity * 100, 1) : 100;
        set
        {
            if (Document.ActiveLayer is { } layer && Math.Abs(layer.Opacity - Math.Clamp(value / 100.0, 0, 1)) > 1e-9)
            {
                LayerEdits.Execute(Document, "History.LayerOpacity",
                    () => LayerOperations.SetOpacity(Document, layer, value / 100.0), coalesceKey: "opacity:" + layer.Id);
            }
        }
    }

    public BlendModeOption? SelectedBlendMode
    {
        get => Document.ActiveLayer is { } layer ? BlendModes.First(o => o.Mode == layer.BlendMode) : null;
        set
        {
            if (Document.ActiveLayer is { } layer && value is not null)
            {
                if (layer.BlendMode != value.Mode)
                {
                    LayerEdits.Execute(Document, "History.LayerBlendMode",
                        () => LayerOperations.SetBlendMode(Document, layer, value.Mode));
                }
            }
        }
    }

    /// <summary>
    /// Gleicht die Ebenenliste mit dem Dokument ab. Unveränderte Struktur → nur Werte auffrischen (kein Flackern),
    /// sonst neu aufbauen.
    /// </summary>
    private void SyncLayerItems()
    {
        var nodes = TopDown(Document.Root).ToList();
        var sameStructure = nodes.Count == Layers.Count && nodes.Zip(Layers).All(p => p.First == p.Second.Node);
        if (sameStructure)
        {
            foreach (var item in Layers)
            {
                item.Refresh();
            }
        }
        else
        {
            _syncingSelection = true;
            try
            {
                Layers.Clear();
                foreach (var node in nodes)
                {
                    Layers.Add(new LayerItemViewModel(Document, node, () => LayerEdits));
                }
            }
            finally
            {
                _syncingSelection = false;
            }
        }

        SyncSelection();
    }

    private static IEnumerable<LayerNode> TopDown(LayerGroup group)
    {
        for (var i = group.Children.Count - 1; i >= 0; i--)
        {
            var child = group.Children[i];
            yield return child;
            if (child is LayerGroup nested)
            {
                foreach (var n in TopDown(nested))
                {
                    yield return n;
                }
            }
        }
    }

    private void SyncSelection()
    {
        _syncingSelection = true;
        try
        {
            SelectedLayer = Layers.FirstOrDefault(i => i.Node == Document.ActiveLayer);
        }
        finally
        {
            _syncingSelection = false;
        }

        OnPropertyChanged(nameof(SelectedOpacityPercent));
        OnPropertyChanged(nameof(SelectedBlendMode));
    }

    // --- Auswahl und Größe --------------------------------------------------------------------

    /// <summary>Höchstzahl der Konturstrecken; darüber wird nur das umschließende Rechteck gezeigt.</summary>
    public const int MaxOutlineSegments = 400_000;

    /// <summary>
    /// Berechnet die Grenzlinien zwischen ausgewählten (≥ 128) und nicht ausgewählten Pixeln.
    /// Benachbarte Kantenstücke werden zu langen Strecken zusammengefasst.
    /// </summary>
    private void UpdateSelectionOutline()
    {
        var selection = Document.Selection;
        var segments = new List<(PointD, PointD)>();
        if (selection is not null)
        {
            var w = selection.Width;
            var h = selection.Height;
            bool In(int x, int y) => x >= 0 && y >= 0 && x < w && y < h && selection.Get(x, y) >= 128;

            // Waagerechte Kanten auf Höhe y (zwischen Zeile y−1 und y)
            for (var y = 0; y <= h && segments.Count <= MaxOutlineSegments; y++)
            {
                var runStart = -1;
                for (var x = 0; x <= w; x++)
                {
                    var edge = x < w && In(x, y) != In(x, y - 1);
                    if (edge && runStart < 0)
                    {
                        runStart = x;
                    }
                    else if (!edge && runStart >= 0)
                    {
                        segments.Add((new PointD(runStart, y), new PointD(x, y)));
                        runStart = -1;
                    }
                }
            }

            // Senkrechte Kanten auf x (zwischen Spalte x−1 und x)
            for (var x = 0; x <= w && segments.Count <= MaxOutlineSegments; x++)
            {
                var runStart = -1;
                for (var y = 0; y <= h; y++)
                {
                    var edge = y < h && In(x, y) != In(x - 1, y);
                    if (edge && runStart < 0)
                    {
                        runStart = y;
                    }
                    else if (!edge && runStart >= 0)
                    {
                        segments.Add((new PointD(x, runStart), new PointD(x, y)));
                        runStart = -1;
                    }
                }
            }

            if (segments.Count > MaxOutlineSegments)
            {
                var b = selection.Bounds();
                segments =
                [
                    (new PointD(b.X, b.Y), new PointD(b.Right, b.Y)), (new PointD(b.Right, b.Y), new PointD(b.Right, b.Bottom)),
                    (new PointD(b.Right, b.Bottom), new PointD(b.X, b.Bottom)), (new PointD(b.X, b.Bottom), new PointD(b.X, b.Y)),
                ];
            }
        }

        SelectionOutline = segments;
        OnPropertyChanged(nameof(SelectionText));
        ViewChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Bildgröße geändert: Ansicht, Komposit und Bitmap neu aufbauen.</summary>
    private void OnDocumentSizeChanged()
    {
        _composite = null;
        _bitmap = null;
        View.SetDocumentSize(Document.Width, Document.Height);
        View.FitToViewport(allowUpscale: false);
        OnPropertyChanged(nameof(Width));
        OnPropertyChanged(nameof(Height));
        OnPropertyChanged(nameof(SizeText));
        UpdateSelectionOutline();
    }

    // --- Darstellung ----------------------------------------------------------------------------

    /// <summary>Setzt den Bereich neu zusammen und überträgt nur diesen in die Bitmap (Dirty-Rectangle).</summary>
    private void Recomposite(IntRect region)
    {
        if (_composite is not null && (_composite.Width != Document.Width || _composite.Height != Document.Height))
        {
            // Größe hat sich geändert, bevor SizeChanged verarbeitet wurde: Cache verwerfen.
            _composite = null;
            _bitmap = null;
        }

        if (_composite is not null)
        {
            LayerCompositor.CompositeRegion(Document.Root, _composite, region);
            if (_bitmap is not null)
            {
                BitmapBridge.Update(_bitmap, _composite, region);
            }
        }

        ViewChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Aktualisiert die Cursoranzeige aus einer Viewport-Position (Gerätepixel).</summary>
    public void UpdateCursorFromViewport(PointD viewport)
    {
        CursorText = View.PixelAtViewport(viewport) is { } p
            ? Loc.Format("Status.Cursor", p.X, p.Y)
            : Loc.Get("Status.CursorOutside");
    }

    public void ClearCursor() => CursorText = Loc.Get("Status.CursorOutside");

    public void ZoomIn() => View.ZoomIn();

    public void ZoomOut() => View.ZoomOut();

    public void ActualSize() => View.ActualSize();

    public void FitToWindow() => View.FitToViewport();

    /// <summary>Mausrad: eine Raststufe (±120) entspricht einer Zoomstufe, Anker ist der Mauszeiger.</summary>
    public void ZoomWithWheel(int delta, PointD anchorViewport)
    {
        if (delta > 0)
        {
            View.ZoomIn(anchorViewport);
        }
        else if (delta < 0)
        {
            View.ZoomOut(anchorViewport);
        }
    }
}
