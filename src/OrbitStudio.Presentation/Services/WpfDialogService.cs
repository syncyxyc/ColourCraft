using System.Windows;
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

    public bool ConfirmDiscardChanges(string documentName) =>
        Show(Loc.Get("Msg.Unsaved.Title"), Loc.Format("Msg.Unsaved.Body", documentName),
            MessageBoxButton.OKCancel, MessageBoxImage.Warning, MessageBoxResult.Cancel) == MessageBoxResult.OK;

    public void ShowInformation(string title, string message) =>
        Show(title, message, MessageBoxButton.OK, MessageBoxImage.Information, MessageBoxResult.OK);

    public void ShowWarning(string title, string message) =>
        Show(title, message, MessageBoxButton.OK, MessageBoxImage.Warning, MessageBoxResult.OK);

    public void ShowError(string title, string message) =>
        Show(title, message, MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK);

    private static MessageBoxResult Show(string title, string message, MessageBoxButton buttons, MessageBoxImage image,
        MessageBoxResult defaultResult) =>
        Owner is { } owner
            ? MessageBox.Show(owner, message, title, buttons, image, defaultResult)
            : MessageBox.Show(message, title, buttons, image, defaultResult);
}
