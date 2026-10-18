using OrbitStudio.Domain.Documents;

namespace OrbitStudio.Presentation.Services;

/// <summary>Angaben aus dem Dialog „Neues Bild".</summary>
public sealed record NewDocumentRequest(string Name, int Width, int Height, DocumentBackground Background);

/// <summary>Kapselt alle Dialoge, damit ViewModels ohne Fenster testbar bleiben.</summary>
public interface IDialogService
{
    NewDocumentRequest? ShowNewDocument(string defaultName, int defaultWidth, int defaultHeight);

    /// <summary>Fragt, ob ungespeicherte Änderungen verworfen werden dürfen. True = verwerfen.</summary>
    bool ConfirmDiscardChanges(string documentName);

    void ShowInformation(string title, string message);

    void ShowWarning(string title, string message);

    void ShowError(string title, string message);
}

/// <summary>Fensterbezogene Aktionen, die nicht in ein ViewModel gehören.</summary>
public interface IShellService
{
    void ToggleFullScreen();

    void RequestExit();
}
