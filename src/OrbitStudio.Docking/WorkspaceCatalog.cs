using System.Collections.Immutable;
using System.Text.Json;

namespace OrbitStudio.Docking;

/// <summary>
/// Benannte Arbeitsbereiche (gespeicherte Panel-Layouts) plus das aktuelle Layout. „Standard“ ist fest eingebaut:
/// er lässt sich weder überschreiben noch umbenennen oder löschen und stellt immer <see cref="PanelLayout.Default"/> her.
/// Namen sind eindeutig ohne Rücksicht auf Groß-/Kleinschreibung. Unveränderlich.
/// </summary>
public sealed class WorkspaceCatalog
{
    public const string DefaultName = "Standard";
    public const int MaxWorkspaces = 32;
    public const int MaxNameLength = 60;

    private readonly ImmutableDictionary<string, (string Name, PanelLayout Layout)> _saved;

    private WorkspaceCatalog(PanelLayout current, string? activeName,
        ImmutableDictionary<string, (string Name, PanelLayout Layout)> saved)
    {
        Current = current;
        ActiveName = activeName;
        _saved = saved;
    }

    public static WorkspaceCatalog Empty { get; } = new(PanelLayout.Default, DefaultName,
        ImmutableDictionary.Create<string, (string, PanelLayout)>(StringComparer.OrdinalIgnoreCase));

    /// <summary>Aktuell angezeigtes Layout.</summary>
    public PanelLayout Current { get; }

    /// <summary>Name des zuletzt geladenen/gespeicherten Arbeitsbereichs; null, wenn das Layout danach verändert wurde.</summary>
    public string? ActiveName { get; }

    /// <summary>Eigene Arbeitsbereiche (ohne „Standard“), nach Namen sortiert.</summary>
    public IReadOnlyList<string> Names => _saved.Values.Select(v => v.Name)
        .OrderBy(n => n, StringComparer.CurrentCultureIgnoreCase).ToList();

    /// <summary>Prüft einen Namen; liefert null oder eine deutsche Fehlermeldung.</summary>
    public static string? ValidateName(string? name)
    {
        var trimmed = name?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return "Der Name darf nicht leer sein.";
        }

        if (trimmed.Length > MaxNameLength)
        {
            return $"Der Name darf höchstens {MaxNameLength} Zeichen lang sein.";
        }

        if (trimmed.Any(char.IsControl))
        {
            return "Der Name enthält ungültige Zeichen.";
        }

        return string.Equals(trimmed, DefaultName, StringComparison.OrdinalIgnoreCase)
            ? $"„{DefaultName}“ ist reserviert."
            : null;
    }

    /// <summary>Ändert das aktuelle Layout (z. B. nach Ziehen eines Panels); der Name des aktiven Arbeitsbereichs entfällt.</summary>
    public WorkspaceCatalog WithCurrent(PanelLayout layout) =>
        layout.Equals(Current) ? this : new WorkspaceCatalog(layout, null, _saved);

    /// <summary>Layout zurücksetzen (Menü „Layout zurücksetzen“).</summary>
    public WorkspaceCatalog ResetCurrent() => new(PanelLayout.Default, DefaultName, _saved);

    /// <summary>Speichert das aktuelle Layout unter einem Namen (überschreibt einen gleichnamigen) und macht ihn aktiv.</summary>
    public WorkspaceCatalog SaveCurrentAs(string name)
    {
        CheckName(name);
        var trimmed = name.Trim();
        if (!_saved.ContainsKey(trimmed) && _saved.Count >= MaxWorkspaces)
        {
            throw new InvalidOperationException($"Es sind höchstens {MaxWorkspaces} Arbeitsbereiche möglich.");
        }

        // Beim Überschreiben bleibt die bisherige Schreibweise des Namens nicht erhalten: die neue gilt.
        var saved = _saved.Remove(trimmed).Add(trimmed, (trimmed, Current));
        return new WorkspaceCatalog(Current, trimmed, saved);
    }

    /// <summary>Lädt einen Arbeitsbereich als aktuelles Layout; „Standard“ lädt die Standardanordnung.</summary>
    public WorkspaceCatalog Load(string name)
    {
        if (string.Equals(name?.Trim(), DefaultName, StringComparison.OrdinalIgnoreCase))
        {
            return ResetCurrent();
        }

        return _saved.TryGetValue(name?.Trim() ?? "", out var entry)
            ? new WorkspaceCatalog(entry.Layout, entry.Name, _saved)
            : throw new KeyNotFoundException($"Arbeitsbereich „{name}“ gibt es nicht.");
    }

    public WorkspaceCatalog Rename(string oldName, string newName)
    {
        CheckName(newName);
        var key = oldName?.Trim() ?? "";
        if (!_saved.TryGetValue(key, out var entry))
        {
            throw new KeyNotFoundException($"Arbeitsbereich „{oldName}“ gibt es nicht.");
        }

        var target = newName.Trim();
        if (_saved.ContainsKey(target) && !string.Equals(target, entry.Name, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Den Namen „{target}“ gibt es schon.");
        }

        var saved = _saved.Remove(key).Add(target, (target, entry.Layout));
        var active = string.Equals(ActiveName, entry.Name, StringComparison.OrdinalIgnoreCase) ? target : ActiveName;
        return new WorkspaceCatalog(Current, active, saved);
    }

    public WorkspaceCatalog Delete(string name)
    {
        var key = name?.Trim() ?? "";
        if (string.Equals(key, DefaultName, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"„{DefaultName}“ lässt sich nicht löschen.");
        }

        if (!_saved.TryGetValue(key, out var entry))
        {
            throw new KeyNotFoundException($"Arbeitsbereich „{name}“ gibt es nicht.");
        }

        var active = string.Equals(ActiveName, entry.Name, StringComparison.OrdinalIgnoreCase) ? null : ActiveName;
        return new WorkspaceCatalog(Current, active, _saved.Remove(key));
    }

    // --- Speicherung ----------------------------------------------------------------------------

    public const int CurrentSchemaVersion = 1;

    private sealed record WorkspaceFile
    {
        public int SchemaVersion { get; init; } = CurrentSchemaVersion;

        public List<PanelState?>? Current { get; init; }

        public string? ActiveName { get; init; }

        public List<NamedLayout?>? Workspaces { get; init; }
    }

    private sealed record NamedLayout(string? Name, List<PanelState?>? Panels);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public string ToJson() => JsonSerializer.Serialize(new WorkspaceFile
    {
        Current = [.. Current.Panels],
        ActiveName = ActiveName,
        Workspaces = _saved.Values.OrderBy(v => v.Name, StringComparer.Ordinal)
            .Select(v => (NamedLayout?)new NamedLayout(v.Name, [.. v.Layout.Panels])).ToList(),
    }, JsonOptions);

    /// <summary>
    /// Liest gespeicherte Arbeitsbereiche. Beschädigte Datei → Standard (<see cref="WorkspaceLoadStatus.Invalid"/>), neuere
    /// Version → Standard (<see cref="WorkspaceLoadStatus.NewerVersion"/>), einzelne Fehler → repariert
    /// (<see cref="WorkspaceLoadStatus.Corrected"/>: ungültige Namen und Layouts entfallen oder werden normalisiert).
    /// </summary>
    public static (WorkspaceCatalog Catalog, WorkspaceLoadStatus Status) FromJson(string? json)
    {
        WorkspaceFile? file;
        try
        {
            file = string.IsNullOrWhiteSpace(json) ? null : JsonSerializer.Deserialize<WorkspaceFile>(json, JsonOptions);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            file = null;
        }

        if (file is null || file.SchemaVersion < 1)
        {
            return (Empty, WorkspaceLoadStatus.Invalid);
        }

        if (file.SchemaVersion > CurrentSchemaVersion)
        {
            return (Empty, WorkspaceLoadStatus.NewerVersion);
        }

        var changed = false;
        var catalog = Empty;
        var current = PanelLayout.Normalize(file.Current, out var currentChanged);
        changed |= currentChanged || file.Current is null;

        foreach (var entry in file.Workspaces ?? [])
        {
            if (entry is null || ValidateName(entry.Name) is not null)
            {
                changed = true;
                continue;
            }

            var trimmed = entry.Name!.Trim();
            if (catalog._saved.ContainsKey(trimmed) || catalog._saved.Count >= MaxWorkspaces)
            {
                changed = true;
                continue;
            }

            var layout = PanelLayout.Normalize(entry.Panels, out var layoutChanged);
            changed |= layoutChanged || trimmed != entry.Name;
            catalog = new WorkspaceCatalog(catalog.Current, catalog.ActiveName, catalog._saved.Add(trimmed, (trimmed, layout)));
        }

        var active = file.ActiveName is { } a && (a == DefaultName || catalog._saved.ContainsKey(a)) ? a : null;
        changed |= file.ActiveName is not null && active is null;
        var result = new WorkspaceCatalog(current, active, catalog._saved);
        return (result, changed ? WorkspaceLoadStatus.Corrected : WorkspaceLoadStatus.Loaded);
    }

    private static void CheckName(string? name)
    {
        var error = ValidateName(name);
        if (error is not null)
        {
            throw new ArgumentException(error, nameof(name));
        }
    }
}

public enum WorkspaceLoadStatus
{
    Loaded,
    Corrected,
    Invalid,
    NewerVersion,
}
