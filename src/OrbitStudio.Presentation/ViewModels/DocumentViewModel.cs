using CommunityToolkit.Mvvm.ComponentModel;
using OrbitStudio.Domain.Documents;
using OrbitStudio.Presentation.Localization;

namespace OrbitStudio.Presentation.ViewModels;

/// <summary>
/// Ansicht eines Dokuments in einem Tab. Ansichtszustand (Zoom, Cursor) gehört zum Dokument, nicht global.
/// </summary>
public sealed partial class DocumentViewModel : ObservableObject
{
    public DocumentViewModel(ImageDocument document)
    {
        Document = document;
        document.StateChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(Title));
            OnPropertyChanged(nameof(IsDirty));
        };
    }

    public ImageDocument Document { get; }

    public string Title => Document.IsDirty ? Document.Name + Loc.Get("Common.UnsavedSuffix") : Document.Name;

    public bool IsDirty => Document.IsDirty;

    public int Width => Document.Width;

    public int Height => Document.Height;

    public bool IsTransparent => Document.Background == DocumentBackground.Transparent;

    /// <summary>Zoomfaktor (1.0 = 100 %). Zoomänderungen folgen in M2 und verändern nie Pixel.</summary>
    public double Zoom => 1.0;

    public string ZoomText => Loc.Format("Status.Zoom", Zoom * 100);

    public string SizeText => Loc.Format("Status.Size", Width, Height);

    [ObservableProperty]
    private string _cursorText = Loc.Get("Status.CursorOutside");

    /// <summary>Aktualisiert die Cursoranzeige. Koordinaten in Dokumentpixeln (Ursprung oben links).</summary>
    public void UpdateCursor(double documentX, double documentY)
    {
        var x = (int)Math.Floor(documentX);
        var y = (int)Math.Floor(documentY);
        CursorText = x >= 0 && y >= 0 && x < Width && y < Height
            ? Loc.Format("Status.Cursor", x, y)
            : Loc.Get("Status.CursorOutside");
    }

    public void ClearCursor() => CursorText = Loc.Get("Status.CursorOutside");
}
