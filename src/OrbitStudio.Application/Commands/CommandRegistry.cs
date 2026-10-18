namespace OrbitStudio.Application.Commands;

/// <summary>
/// Zentrale Registry aller Befehle. Menüs, Symbolleisten und Tastenkürzel werden ausschließlich hieraus erzeugt.
/// </summary>
public sealed class CommandRegistry
{
    private readonly Dictionary<string, AppCommand> _commands = new(StringComparer.Ordinal);
    private readonly List<AppCommand> _ordered = [];

    public IReadOnlyList<AppCommand> All => _ordered;

    /// <summary>Wird ausgelöst, wenn sich der Anwendungszustand geändert hat und CanExecute neu ausgewertet werden muss.</summary>
    public event EventHandler? RequerySuggested;

    public void Register(AppCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (!_commands.TryAdd(command.Id, command))
        {
            throw new InvalidOperationException($"Befehl '{command.Id}' ist bereits registriert.");
        }

        _ordered.Add(command);
    }

    public AppCommand Get(string id) =>
        _commands.TryGetValue(id, out var command)
            ? command
            : throw new KeyNotFoundException($"Befehl '{id}' ist nicht registriert.");

    public bool TryGet(string id, out AppCommand? command) => _commands.TryGetValue(id, out command);

    /// <summary>Setzt die Tastenbelegung eines Befehls. null entfernt die Belegung.</summary>
    public void SetGesture(string id, string? gesture) => Get(id).Gesture = KeyGestureText.Normalize(gesture);

    public void ResetGestures()
    {
        foreach (var command in _ordered)
        {
            command.Gesture = command.DefaultGesture;
        }
    }

    /// <summary>Liefert alle Tastenkürzel, die mehr als einem Befehl zugeordnet sind.</summary>
    public IReadOnlyList<GestureConflict> FindGestureConflicts() =>
        _ordered
            .Where(c => c.Gesture is not null)
            .GroupBy(c => c.Gesture!, StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .Select(g => new GestureConflict(g.Key, g.Select(c => c.Id).ToList()))
            .ToList();

    public void NotifyStateChanged() => RequerySuggested?.Invoke(this, EventArgs.Empty);
}

public sealed record GestureConflict(string Gesture, IReadOnlyList<string> CommandIds);
