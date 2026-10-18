using System.Windows;
using System.Windows.Controls;
using OrbitStudio.Presentation.Localization;
using OrbitStudio.Presentation.Services;

namespace OrbitStudio.Presentation.Views;

/// <summary>
/// Rückfrage vor Datenverlust mit eindeutig beschrifteten Schaltflächen „Speichern“, „Nicht speichern“, „Abbrechen“
/// (statt Ja/Nein). Enter = Speichern, Esc oder Fenster schließen = Abbrechen.
/// </summary>
public sealed class UnsavedChangesDialog : Window
{
    public UnsavedChangesDialog(string documentName)
    {
        Title = Loc.Get("Msg.Unsaved.Title");
        SizeToContent = SizeToContent.WidthAndHeight;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;
        SetResourceReference(BackgroundProperty, "Brush.Window.Background");
        SetResourceReference(ForegroundProperty, "Brush.Text");

        var panel = new StackPanel { Margin = new Thickness(20), MaxWidth = 460 };
        panel.Children.Add(new TextBlock
        {
            Text = Loc.Format("Msg.Unsaved.Body", documentName),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 16),
        });

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        Button Add(string key, UnsavedChangesChoice choice, bool isDefault = false, bool isCancel = false)
        {
            var button = new Button
            {
                Content = Loc.Get(key),
                MinWidth = 110,
                Margin = new Thickness(8, 0, 0, 0),
                IsDefault = isDefault,
                IsCancel = isCancel,
            };
            button.Click += (_, _) =>
            {
                Choice = choice;
                DialogResult = choice != UnsavedChangesChoice.Cancel;
            };
            buttons.Children.Add(button);
            return button;
        }

        Add("Msg.Unsaved.Save", UnsavedChangesChoice.Save, isDefault: true);
        Add("Msg.Unsaved.Discard", UnsavedChangesChoice.Discard);
        Add("Common.Cancel", UnsavedChangesChoice.Cancel, isCancel: true);
        panel.Children.Add(buttons);
        Content = panel;
    }

    /// <summary>Gewählte Antwort; ohne Auswahl (Fenster geschlossen) „Abbrechen“.</summary>
    public UnsavedChangesChoice Choice { get; private set; } = UnsavedChangesChoice.Cancel;
}
