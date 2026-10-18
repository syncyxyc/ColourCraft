using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using OrbitStudio.Application.Commands;
using OrbitStudio.Presentation.ViewModels;
using OrbitStudio.Tools;

namespace OrbitStudio.Presentation.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    /// <summary>Einfache Tastenkürzel ohne Strg/Alt (z. B. „B", „Shift+M"), Normalform → Befehl.</summary>
    private readonly Dictionary<string, AppCommand> _simpleShortcuts = new(StringComparer.Ordinal);

    public MainWindow(MainViewModel viewModel, ILogger<MainWindow> logger)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        BindShortcuts(logger);
    }

    public MainViewModel ViewModel => _viewModel;

    /// <summary>
    /// Tastenkürzel werden nur hier und nur aus der Registry gebunden – nie in einzelnen Controls.
    /// Kürzel mit Strg/Alt oder Funktionstasten laufen über WPF-KeyBindings; einfache Buchstaben-Kürzel (Werkzeuge,
    /// Farben) über <see cref="OnPreviewKeyDown"/>, damit sie beim Tippen in Textfeldern nicht auslösen.
    /// </summary>
    private void BindShortcuts(ILogger logger)
    {
        foreach (var command in _viewModel.Registry.All)
        {
            if (command.Gesture is not { } text)
            {
                continue;
            }

            if (IsSimpleShortcut(text))
            {
                _simpleShortcuts[text] = command;
                continue;
            }

            try
            {
                var gesture = GestureDisplay.ToKeyGesture(text);
                if (gesture is not null)
                {
                    InputBindings.Add(new KeyBinding(new CommandAdapter(command, _viewModel.Registry), gesture));
                }
            }
            catch (Exception ex) when (ex is NotSupportedException or ArgumentException or FormatException)
            {
                logger.LogError(ex, "Ungültiges Tastenkürzel {Gesture} für {Command}", text, command.Id);
            }
        }
    }

    /// <summary>Buchstabe/Ziffer, optional mit Umschalt, ohne Strg/Alt/Win.</summary>
    public static bool IsSimpleShortcut(string gesture)
    {
        var parts = gesture.Split('+');
        var key = parts[^1];
        var modifiersOk = parts[..^1].All(p => p == "Shift");
        return modifiersOk && key.Length == 1 && char.IsLetterOrDigit(key[0]);
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);
        if (e.Handled || IsTextInput(Keyboard.FocusedElement))
        {
            return;
        }

        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        var modifiers = Keyboard.Modifiers;
        var document = _viewModel.ActiveDocument;

        // Esc bricht eine laufende Effektberechnung ab.
        if (key == Key.Escape && _viewModel.CancelEffect())
        {
            e.Handled = true;
            return;
        }

        // Escape, Enter und Pfeiltasten zuerst an das aktive Werkzeug (Abbrechen, Übernehmen, Verschieben).
        ToolKey? toolKey = key switch
        {
            Key.Escape => ToolKey.Escape,
            Key.Enter => ToolKey.Enter,
            Key.Left => ToolKey.Left,
            Key.Right => ToolKey.Right,
            Key.Up => ToolKey.Up,
            Key.Down => ToolKey.Down,
            _ => null,
        };
        if (toolKey is { } tk && document is not null && (modifiers & (ModifierKeys.Control | ModifierKeys.Alt)) == 0
            && !(Keyboard.FocusedElement is Selector or ScrollBar && tk is not (ToolKey.Escape or ToolKey.Enter)))
        {
            var toolModifiers = modifiers.HasFlag(ModifierKeys.Shift) ? ToolModifiers.Shift : ToolModifiers.None;
            if (_viewModel.Tools.Key(document, tk, toolModifiers))
            {
                e.Handled = true;
                return;
            }
        }

        if ((modifiers & (ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Windows)) != 0)
        {
            return;
        }

        var name = key switch
        {
            >= Key.A and <= Key.Z => key.ToString(),
            >= Key.D0 and <= Key.D9 => ((int)(key - Key.D0)).ToString(System.Globalization.CultureInfo.InvariantCulture),
            _ => null,
        };
        if (name is null)
        {
            return;
        }

        var gesture = modifiers.HasFlag(ModifierKeys.Shift) ? "Shift+" + name : name;
        if (_simpleShortcuts.TryGetValue(gesture, out var command) && command.CanExecute())
        {
            command.Execute();
            e.Handled = true;
        }
    }

    private static bool IsTextInput(IInputElement? element) =>
        element is TextBoxBase or PasswordBox || (element is ComboBox { IsEditable: true });

    /// <summary>
    /// Enter in einem Optionsfeld übernimmt den Wert sofort (sonst erst beim Verlassen des Feldes). Ungültige Eingaben
    /// werden verworfen und das Feld zeigt wieder den gültigen Wert.
    /// </summary>
    private void OnOptionTextBoxKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || sender is not TextBox box
            || box.GetBindingExpression(TextBox.TextProperty) is not { } binding)
        {
            return;
        }

        binding.UpdateSource();
        if (binding.HasError)
        {
            binding.UpdateTarget();
        }

        box.SelectAll();
        e.Handled = true;
    }

    private void OnPrimaryColorClick(object sender, RoutedEventArgs e) => _viewModel.PickColor(primary: true);

    private void OnSecondaryColorClick(object sender, RoutedEventArgs e) => _viewModel.PickColor(primary: false);

    private void OnDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) && !_viewModel.IsBusy
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    /// <summary>Bilddateien in das Fenster ziehen öffnet sie als neue Dokumente.</summary>
    private async void OnDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] { Length: > 0 } files && !_viewModel.IsBusy)
        {
            e.Handled = true;
            await _viewModel.OpenFilesAsync(files);
        }
    }

    /// <summary>
    /// Beim Beenden erst alle Dokumente schließen (mit Rückfrage und ggf. Speichern im Hintergrund). Weil das Speichern
    /// asynchron ist, wird das Schließen zunächst abgebrochen und nach erfolgreichem Schließen aller Dokumente erneut
    /// ausgelöst. Während eines laufenden Vorgangs (Speichern, Effekt) bleibt das Fenster offen.
    /// </summary>
    protected override void OnClosing(CancelEventArgs e)
    {
        _viewModel.Tools.CommitActive();
        if (!_closeConfirmed && (_viewModel.IsBusy || _viewModel.Documents.Count > 0))
        {
            e.Cancel = true;
            if (!_viewModel.IsBusy)
            {
                Dispatcher.InvokeAsync(CloseAfterDocumentsAsync);
            }
        }

        base.OnClosing(e);
    }

    private bool _closeConfirmed;

    private async Task CloseAfterDocumentsAsync()
    {
        if (await _viewModel.CloseAllDocumentsAsync())
        {
            _closeConfirmed = true;
            Close();
        }
    }
}
