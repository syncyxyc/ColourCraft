using System.Text.Json;

namespace OrbitStudio.Configuration;

/// <summary>Ergebnis eines Ladevorgangs. Alles außer <see cref="Loaded"/> wird dem Nutzer gemeldet.</summary>
public enum SettingsLoadStatus
{
    /// <summary>Datei gelesen und gültig.</summary>
    Loaded,

    /// <summary>Keine Datei vorhanden, Standardwerte verwendet (erster Start).</summary>
    Missing,

    /// <summary>Datei gelesen, einzelne ungültige Werte wurden auf Standardwerte gesetzt.</summary>
    Corrected,

    /// <summary>Datei unlesbar oder beschädigt. Sicherungskopie angelegt, Standardwerte verwendet.</summary>
    Invalid,

    /// <summary>Datei stammt aus einer neueren Programmversion. Unverändert gelassen, Standardwerte verwendet.</summary>
    NewerVersion,
}

public sealed record SettingsLoadResult<T>(T Value, SettingsLoadStatus Status, string? BackupPath = null);

/// <summary>
/// Lädt und speichert eine versionierte JSON-Einstellungsdatei.
/// Fehlerhafte Dateien führen nie zum Absturz, sondern zu Standardwerten plus Statusmeldung.
/// Gespeichert wird atomar: erst in eine temporäre Datei, dann ersetzen.
/// </summary>
public sealed class SettingsStore<T> where T : class, IVersionedSettings, new()
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private readonly string _filePath;
    private readonly int _currentVersion;
    private readonly Func<T, (T Value, bool Changed)> _normalize;

    public SettingsStore(string filePath, int currentVersion, Func<T, (T Value, bool Changed)> normalize)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        _filePath = filePath;
        _currentVersion = currentVersion;
        _normalize = normalize;
    }

    public string FilePath => _filePath;

    public SettingsLoadResult<T> Load()
    {
        if (!File.Exists(_filePath))
        {
            return new SettingsLoadResult<T>(new T(), SettingsLoadStatus.Missing);
        }

        T? loaded;
        try
        {
            var json = File.ReadAllText(_filePath);
            loaded = JsonSerializer.Deserialize<T>(json, JsonOptions);
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return new SettingsLoadResult<T>(new T(), SettingsLoadStatus.Invalid, TryBackup());
        }

        if (loaded is null)
        {
            return new SettingsLoadResult<T>(new T(), SettingsLoadStatus.Invalid, TryBackup());
        }

        if (loaded.SchemaVersion > _currentVersion)
        {
            return new SettingsLoadResult<T>(new T(), SettingsLoadStatus.NewerVersion);
        }

        if (loaded.SchemaVersion < 1)
        {
            return new SettingsLoadResult<T>(new T(), SettingsLoadStatus.Invalid, TryBackup());
        }

        // Ältere Schemaversionen: Migrationsschritte werden hier ergänzt, sobald es Version 2 gibt.
        var (value, changed) = _normalize(loaded);
        return new SettingsLoadResult<T>(value, changed ? SettingsLoadStatus.Corrected : SettingsLoadStatus.Loaded);
    }

    public void Save(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = _filePath + ".tmp";
        File.WriteAllText(tempPath, JsonSerializer.Serialize(value, JsonOptions));
        File.Move(tempPath, _filePath, overwrite: true);
    }

    private string? TryBackup()
    {
        try
        {
            var backup = $"{_filePath}.defekt-{DateTime.Now:yyyyMMdd-HHmmss}";
            File.Copy(_filePath, backup, overwrite: true);
            return backup;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}

/// <summary>Fabrik für die Stores der konkreten Einstellungsdateien.</summary>
public static class SettingsStores
{
    public static SettingsStore<GlobalSettings> Global(AppPaths paths) =>
        new(paths.GlobalSettingsFile, GlobalSettings.CurrentSchemaVersion,
            s => (GlobalSettings.Normalize(s, out var c), c));

    public static SettingsStore<WorkspaceSettings> Workspace(AppPaths paths) =>
        new(paths.WorkspaceSettingsFile, WorkspaceSettings.CurrentSchemaVersion,
            s => (WorkspaceSettings.Normalize(s, out var c), c));
}
