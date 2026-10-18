using System.ComponentModel;
using System.Windows.Input;
using OrbitStudio.Application.Commands;
using OrbitStudio.Presentation.Localization;

namespace OrbitStudio.Presentation.ViewModels;

/// <summary>Ein Eintrag der Menüleiste: Untermenü, Befehl oder Trennlinie.</summary>
public sealed class MenuItemViewModel : INotifyPropertyChanged
{
    private readonly AppCommand? _appCommand;

    private MenuItemViewModel(string header, ICommand? command, object? commandParameter, string? gestureText,
        string? toolTip, bool isSeparator, IReadOnlyList<MenuItemViewModel> items, string? commandId,
        AppCommand? appCommand, bool isEnabledOverride = true)
    {
        Header = header;
        Command = command;
        CommandParameter = commandParameter;
        GestureText = gestureText;
        ToolTip = toolTip;
        IsSeparator = isSeparator;
        Items = items;
        CommandId = commandId;
        _appCommand = appCommand;
        IsEnabled = isEnabledOverride;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Header { get; }

    public ICommand? Command { get; }

    public object? CommandParameter { get; }

    public string? GestureText { get; }

    public string? ToolTip { get; }

    public bool IsSeparator { get; }

    public string? CommandId { get; }

    /// <summary>Nur für Einträge ohne Befehl relevant (z. B. Platzhalter „(keine)").</summary>
    public bool IsEnabled { get; }

    /// <summary>Häkchen bei Umschaltbefehlen.</summary>
    public bool IsChecked => _appCommand?.IsChecked == true;

    /// <summary>Untereinträge. Kann eine beobachtbare Sammlung sein (z. B. „Zuletzt verwendet").</summary>
    public IReadOnlyList<MenuItemViewModel> Items { get; }

    public static MenuItemViewModel Separator() => new(string.Empty, null, null, null, null, true, [], null, null);

    public static MenuItemViewModel Submenu(string header, IReadOnlyList<MenuItemViewModel> items) =>
        new(header, null, null, null, null, false, items, null, null);

    /// <summary>Deaktivierter Hinweiseintrag ohne Befehl.</summary>
    public static MenuItemViewModel Placeholder(string header) =>
        new(header, null, null, null, null, false, [], null, null, isEnabledOverride: false);

    /// <summary>Eintrag mit eigenem Befehl (z. B. eine zuletzt verwendete Datei).</summary>
    public static MenuItemViewModel ForAction(string header, ICommand command, object? parameter, string? toolTip) =>
        new(header, command, parameter, null, toolTip, false, [], null, null);

    public static MenuItemViewModel ForCommand(AppCommand command, CommandRegistry registry, string? headerOverride = null)
    {
        var toolTip = command.IsImplemented ? null : Loc.Format("Common.PlannedTooltip", command.PlannedMilestone);
        var item = new MenuItemViewModel(
            headerOverride ?? Loc.Get(command.TextKey),
            new CommandAdapter(command, registry),
            null,
            GestureDisplay.ToDisplayText(command.Gesture),
            toolTip,
            false,
            [],
            command.Id,
            command);
        if (command.IsToggle)
        {
            registry.RequerySuggested += (_, _) =>
                item.PropertyChanged?.Invoke(item, new PropertyChangedEventArgs(nameof(IsChecked)));
        }

        return item;
    }
}
