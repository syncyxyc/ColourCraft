using System.Windows;
using Microsoft.Win32;
using OrbitStudio.Presentation.Localization;
using OrbitStudio.Presentation.Views;

namespace OrbitStudio.Presentation.Services;

/// <summary>WPF-Umsetzung der Dialoge. Besitzer ist jeweils das aktive Hauptfenster.</summary>
public sealed class WpfDialogService : IDialogService
{
    private static Window? Owner => System.Windows.Application.Current?.MainWindow is { IsLoaded: true } w ? w : null;

    public NewDocumentRequest? ShowNewDocument(string defaultName, int defaultWidth, int defaultHeight)
    {
        var dialog = new NewDocumentDialog(defaultName, defaultWidth, defaultHeight) { Owner = Owner };
        return dialog.ShowDialog() == true ? dialog.Result : null;
    }

    public IReadOnlyList<string>? ShowOpenFiles(string title, IReadOnlyList<FileFilter> filters)
    {
        var dialog = new OpenFileDialog
        {
            Title = title,
            Filter = BuildFilter(filters),
            Multiselect = true,
            CheckFileExists = true,
        };
        return ShowCommon(dialog) ? dialog.FileNames : null;
    }

    public (string Path, int FilterIndex)? ShowSaveFile(string title, IReadOnlyList<FileFilter> filters,
        string defaultFileName, int defaultFilterIndex)
    {
        var dialog = new SaveFileDialog
        {
            Title = title,
            Filter = BuildFilter(filters),
            FilterIndex = defaultFilterIndex + 1, // WPF zählt ab 1
            FileName = defaultFileName,
            AddExtension = true,
            OverwritePrompt = true,
        };
        return ShowCommon(dialog) ? (dialog.FileName, dialog.FilterIndex - 1) : null;
    }

    public Domain.Imaging.ColorRgba? ShowColorPicker(Domain.Imaging.ColorRgba initial)
    {
        var dialog = new ColorDialog(initial) { Owner = Owner };
        return dialog.ShowDialog() == true ? dialog.Result : null;
    }

    public Effects.EffectSettings? ShowEffect(EffectDialogRequest request)
    {
        var dialog = new EffectDialog(request) { Owner = Owner };
        return dialog.ShowDialog() == true ? dialog.Result : null;
    }

    public double[]? ShowNumbers(string titleKey, IReadOnlyList<NumberField> fields, bool linkFirstTwo = false)
    {
        var dialog = new NumberFieldsDialog(titleKey, fields, linkFirstTwo) { Owner = Owner };
        return dialog.ShowDialog() == true ? dialog.Result : null;
    }

    public (int Width, int Height, Tools.Transform.CanvasAnchor Anchor)? ShowCanvasSize(int width, int height)
    {
        var dialog = new CanvasSizeDialog(width, height) { Owner = Owner };
        return dialog.ShowDialog() == true ? dialog.Result : null;
    }

    public LayerProperties? ShowLayerProperties(LayerProperties initial, LayerPropertyFocus focus)
    {
        var dialog = new LayerPropertiesDialog(initial, focus) { Owner = Owner };
        return dialog.ShowDialog() == true ? dialog.Result : null;
    }

    public UnsavedChangesChoice AskSaveChanges(string documentName)
    {
        var dialog = new UnsavedChangesDialog(documentName) { Owner = Owner };
        dialog.ShowDialog();
        return dialog.Choice;
    }

    public bool Confirm(string title, string message) =>
        Show(title, message, MessageBoxButton.OKCancel, MessageBoxImage.Warning, MessageBoxResult.Cancel) == MessageBoxResult.OK;

    public void ShowInformation(string title, string message) =>
        Show(title, message, MessageBoxButton.OK, MessageBoxImage.Information, MessageBoxResult.OK);

    public void ShowWarning(string title, string message) =>
        Show(title, message, MessageBoxButton.OK, MessageBoxImage.Warning, MessageBoxResult.OK);

    public void ShowError(string title, string message) =>
        Show(title, message, MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK);

    /// <summary>"Beschreibung (*.a;*.b)|*.a;*.b|…"</summary>
    public static string BuildFilter(IReadOnlyList<FileFilter> filters) =>
        string.Join('|', filters.Select(f =>
        {
            var patterns = string.Join(';', f.Patterns);
            return $"{f.Description} ({patterns})|{patterns}";
        }));

    private static bool ShowCommon(CommonDialog dialog) =>
        (Owner is { } owner ? dialog.ShowDialog(owner) : dialog.ShowDialog()) == true;

    private static MessageBoxResult Show(string title, string message, MessageBoxButton buttons, MessageBoxImage image,
        MessageBoxResult defaultResult) =>
        Owner is { } owner
            ? MessageBox.Show(owner, message, title, buttons, image, defaultResult)
            : MessageBox.Show(message, title, buttons, image, defaultResult);
}
