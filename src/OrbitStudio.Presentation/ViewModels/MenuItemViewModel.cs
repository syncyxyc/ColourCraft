using System.Windows.Input;
using OrbitStudio.Application.Commands;
using OrbitStudio.Presentation.Localization;

namespace OrbitStudio.Presentation.ViewModels;

/// <summary>Ein Eintrag der Menüleiste: Untermenü, Befehl oder Trennlinie.</summary>
public sealed class MenuItemViewModel
{
    private MenuItemViewModel(string header, ICommand? command, string? gestureText, string? toolTip,
        bool isSeparator, IReadOnlyList<MenuItemViewModel> items, string? commandId)
    {
        Header = header;
        Command = command;
        GestureText = gestureText;
        ToolTip = toolTip;
        IsSeparator = isSeparator;
        Items = items;
        CommandId = commandId;
    }

    public string Header { get; }

    public ICommand? Command { get; }

    public string? GestureText { get; }

    public string? ToolTip { get; }

    public bool IsSeparator { get; }

    public string? CommandId { get; }

    public IReadOnlyList<MenuItemViewModel> Items { get; }

    public static MenuItemViewModel Separator() => new(string.Empty, null, null, null, true, [], null);

    public static MenuItemViewModel Submenu(string header, IReadOnlyList<MenuItemViewModel> items) =>
        new(header, null, null, null, false, items, null);

    public static MenuItemViewModel ForCommand(AppCommand command, CommandRegistry registry, string? headerOverride = null)
    {
        var toolTip = command.IsImplemented ? null : Loc.Format("Common.PlannedTooltip", command.PlannedMilestone);
        return new MenuItemViewModel(
            headerOverride ?? Loc.Get(command.TextKey),
            new CommandAdapter(command, registry),
            GestureDisplay.ToDisplayText(command.Gesture),
            toolTip,
            false,
            [],
            command.Id);
    }
}
