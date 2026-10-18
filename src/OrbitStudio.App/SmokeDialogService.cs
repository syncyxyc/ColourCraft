using OrbitStudio.Domain.Imaging;
using OrbitStudio.Presentation.Services;
using OrbitStudio.Tools.Transform;

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

    public Queue<ColorRgba?> ColorAnswers { get; } = new();

    public Queue<double[]?> NumberAnswers { get; } = new();

    public Queue<(int Width, int Height, CanvasAnchor Anchor)?> CanvasAnswers { get; } = new();

    public ColorRgba? ShowColorPicker(ColorRgba initial) => ColorAnswers.TryDequeue(out var answer) ? answer : null;

    /// <summary>Antworten des Effektdialogs: aus den Startwerten die bestätigten Werte (null = Abbrechen).</summary>
    public Queue<Func<Effects.EffectSettings, Effects.EffectSettings?>> EffectAnswers { get; } = new();

    /// <summary>Letzte Anfrage an den Effektdialog und die dafür berechnete Vorschau (wie der echte Dialog).</summary>
    public EffectDialogRequest? LastEffectRequest { get; private set; }

    public PixelBuffer? LastEffectPreview { get; private set; }

    public Effects.EffectSettings? ShowEffect(EffectDialogRequest request)
    {
        LastEffectRequest = request;
        if (!EffectAnswers.TryDequeue(out var answer) || answer(request.Initial) is not { } chosen)
        {
            return null;
        }

        LastEffectPreview = request.Preview.Render(request.Effect, chosen, CancellationToken.None);
        return chosen;
    }

    public double[]? ShowNumbers(string titleKey, IReadOnlyList<NumberField> fields, bool linkFirstTwo = false)
    {
        if (!NumberAnswers.TryDequeue(out var answer) || answer is null)
        {
            return null;
        }

        // Wie der echte Dialog: Werte gegen die Feldgrenzen prüfen.
        var (values, _) = Presentation.Views.NumberFieldsDialog.Validate(fields,
            answer.Select(v => v.ToString(Presentation.Localization.Loc.Culture)).ToList());
        return values;
    }

    public (int Width, int Height, CanvasAnchor Anchor)? ShowCanvasSize(int width, int height) =>
        CanvasAnswers.TryDequeue(out var answer) ? answer : null;

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
