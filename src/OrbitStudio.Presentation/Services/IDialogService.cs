using OrbitStudio.Domain.Documents;

namespace OrbitStudio.Presentation.Services;

/// <summary>Angaben aus dem Dialog „Neues Bild".</summary>
public sealed record NewDocumentRequest(string Name, int Width, int Height, DocumentBackground Background);

/// <summary>Ein Dateityp-Filter für Datei-Dialoge, z. B. ("PNG-Bild", ["*.png"]).</summary>
public sealed record FileFilter(string Description, IReadOnlyList<string> Patterns);

/// <summary>Kapselt alle Dialoge, damit ViewModels ohne Fenster testbar bleiben.</summary>
public interface IDialogService
{
    NewDocumentRequest? ShowNewDocument(string defaultName, int defaultWidth, int defaultHeight);

    /// <summary>Datei-öffnen-Dialog mit Mehrfachauswahl. Null = abgebrochen.</summary>
    IReadOnlyList<string>? ShowOpenFiles(string title, IReadOnlyList<FileFilter> filters);

    /// <summary>Speichern-Dialog. Liefert Pfad und den gewählten Filter (0-basiert) oder null.</summary>
    (string Path, int FilterIndex)? ShowSaveFile(string title, IReadOnlyList<FileFilter> filters, string defaultFileName,
        int defaultFilterIndex);

    /// <summary>Dialog „Ebeneneigenschaften". Null = abgebrochen.</summary>
    LayerProperties? ShowLayerProperties(LayerProperties initial, LayerPropertyFocus focus);

    /// <summary>Fragt, ob ungespeicherte Änderungen verworfen werden dürfen. True = verwerfen.</summary>
    bool ConfirmDiscardChanges(string documentName);

    /// <summary>OK/Abbrechen-Rückfrage. True = OK.</summary>
    bool Confirm(string title, string message);

    void ShowInformation(string title, string message);

    void ShowWarning(string title, string message);

    void ShowError(string title, string message);
}

/// <summary>Fensterbezogene Aktionen, die nicht in ein ViewModel gehören.</summary>
public interface IShellService
{
    void ToggleFullScreen();

    void RequestExit();

    /// <summary>Zeigt während langer Vorgänge den Wartecursor.</summary>
    void SetBusy(bool busy);
}
