namespace OrbitStudio.Application.Commands;

/// <summary>
/// Ein Befehl der zentralen Command-Registry. Menüs, Symbolleisten und Tastenkürzel verweisen nur auf die ID.
/// Sichtbare Texte werden über <see cref="TextKey"/> aus den Sprachressourcen geladen.
/// </summary>
public sealed class AppCommand
{
    private readonly Action _execute;
    private readonly Func<bool> _canExecute;

    private AppCommand(string id, string textKey, string? defaultGesture, Action execute, Func<bool> canExecute,
        string? plannedMilestone)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(textKey);
        Id = id;
        TextKey = textKey;
        DefaultGesture = KeyGestureText.Normalize(defaultGesture);
        Gesture = DefaultGesture;
        _execute = execute;
        _canExecute = canExecute;
        PlannedMilestone = plannedMilestone;
    }

    public string Id { get; }

    public string TextKey { get; }

    public string? DefaultGesture { get; }

    /// <summary>Aktuelle Tastenbelegung im Textformat, z. B. "Ctrl+Shift+S".</summary>
    public string? Gesture { get; internal set; }

    /// <summary>False, wenn der Befehl in einem späteren Meilenstein umgesetzt wird. Er ist dann deaktiviert.</summary>
    public bool IsImplemented => PlannedMilestone is null;

    /// <summary>Meilenstein, in dem der Befehl umgesetzt wird (z. B. "M2"), sonst null.</summary>
    public string? PlannedMilestone { get; }

    public static AppCommand Create(string id, string textKey, Action execute, Func<bool>? canExecute = null,
        string? gesture = null) =>
        new(id, textKey, gesture, execute, canExecute ?? (() => true), null);

    /// <summary>Befehl, der im Menü sichtbar, aber bis zum genannten Meilenstein deaktiviert ist.</summary>
    public static AppCommand Planned(string id, string textKey, string milestone, string? gesture = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(milestone);
        return new AppCommand(id, textKey, gesture, static () => { }, static () => false, milestone);
    }

    public bool CanExecute() => IsImplemented && _canExecute();

    public void Execute()
    {
        if (CanExecute())
        {
            _execute();
        }
    }
}
