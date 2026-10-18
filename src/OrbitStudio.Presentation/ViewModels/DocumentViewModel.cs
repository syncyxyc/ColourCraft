using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using OrbitStudio.Domain.Documents;
using OrbitStudio.Domain.Imaging;
using OrbitStudio.Presentation.Localization;
using OrbitStudio.Presentation.Viewport;
using OrbitStudio.Rendering;

namespace OrbitStudio.Presentation.ViewModels;

/// <summary>
/// Ansicht eines Dokuments in einem Tab. Ansichtszustand (Zoom, Verschiebung, Cursor) gehört zum Dokument,
/// nicht global – beim Tabwechsel bleibt er je Dokument erhalten.
/// </summary>
public sealed partial class DocumentViewModel : ObservableObject
{
    private WriteableBitmap? _bitmap;

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
        document.PixelsChanged += (_, region) =>
        {
            if (_bitmap is not null)
            {
                BitmapBridge.Update(_bitmap, Document.Pixels, region);
            }

            ViewChanged?.Invoke(this, EventArgs.Empty);
        };
    }

    public ImageDocument Document { get; }

    /// <summary>Koordinatentransformation dieser Ansicht.</summary>
    public ViewTransform View { get; }

    /// <summary>True, sobald die erste Anpassung an das Fenster erfolgt ist.</summary>
    public bool IsInitialViewApplied { get; set; }

    /// <summary>Darstellung der Pixel. Wird beim ersten Zugriff erzeugt und danach nur bereichsweise aktualisiert.</summary>
    public WriteableBitmap Bitmap => _bitmap ??= BitmapBridge.Create(Document.Pixels);

    /// <summary>Neu zeichnen nötig (Zoom, Verschiebung oder Pixel geändert).</summary>
    public event EventHandler? ViewChanged;

    public string Title => Document.IsDirty ? Document.Name + Loc.Get("Common.UnsavedSuffix") : Document.Name;

    public bool IsDirty => Document.IsDirty;

    public int Width => Document.Width;

    public int Height => Document.Height;

    public bool IsTransparent => Document.Background == DocumentBackground.Transparent;

    public double Zoom => View.Zoom;

    public string ZoomText => Loc.Format("Status.Zoom", View.Zoom * 100);

    public string SizeText => Loc.Format("Status.Size", Width, Height);

    [ObservableProperty]
    private string _cursorText = Loc.Get("Status.CursorOutside");

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
