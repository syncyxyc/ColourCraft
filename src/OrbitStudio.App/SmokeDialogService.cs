using OrbitStudio.Presentation.Services;

namespace OrbitStudio.App;

/// <summary>
/// Dialogdienst für den Smoke-Test: Rückfragen werden automatisch beantwortet, Dateidialoge und Eingabedialoge
/// liefern vorbereitete Antworten, alle Meldungen werden protokolliert. So laufen Befehle über denselben Weg
/// wie bei einer Bedienung durch den Nutzer, ohne dass ein modaler Dialog den Test blockiert.
/// </summary>
internal sealed class SmokeDialogService : IDialogService
{
    public List<(string Kind, string Title, string Message)> Messages { get; } = [];

    public bool ConfirmAnswer { get; set; } = true;

    public Queue<NewDocumentRequest?> NewDocumentAnswers { get; } = new();

    public Queue<IReadOnlyList<string>?> OpenFilesAnswers { get; } = new();

    public Queue<(string Path, int FilterIndex)?> SaveFileAnswers { get; } = new();

    public Queue<Func<LayerProperties, LayerProperties?>> LayerPropertiesAnswers { get; } = new();

    public NewDocumentRequest? ShowNewDocument(string defaultName, int defaultWidth, int defaultHeight) =>
        NewDocumentAnswers.TryDequeue(out var answer) ? answer : null;

    public IReadOnlyList<string>? ShowOpenFiles(string title, IReadOnlyList<FileFilter> filters) =>
        OpenFilesAnswers.TryDequeue(out var answer) ? answer : null;

    public (string Path, int FilterIndex)? ShowSaveFile(string title, IReadOnlyList<FileFilter> filters,
        string defaultFileName, int defaultFilterIndex) =>
        SaveFileAnswers.TryDequeue(out var answer) ? answer : null;

    public LayerProperties? ShowLayerProperties(LayerProperties initial, LayerPropertyFocus focus) =>
        LayerPropertiesAnswers.TryDequeue(out var answer) ? answer(initial) : null;

    public bool ConfirmDiscardChanges(string documentName)
    {
        Messages.Add(("confirm", "discard", documentName));
        return ConfirmAnswer;
    }

    public bool Confirm(string title, string message)
    {
        Messages.Add(("confirm", title, message));
        return ConfirmAnswer;
    }

    public void ShowInformation(string title, string message) => Messages.Add(("info", title, message));

    public void ShowWarning(string title, string message) => Messages.Add(("warning", title, message));

    public void ShowError(string title, string message) => Messages.Add(("error", title, message));
}
