using Microsoft.Extensions.Logging;
using OrbitStudio.Configuration;

namespace OrbitStudio.Application.Settings;

/// <summary>Meldung über ein Problem beim Laden einer Einstellungsdatei.</summary>
public sealed record SettingsIssue(string FilePath, SettingsLoadStatus Status, string? BackupPath);

/// <summary>
/// Hält die aktuell gültigen globalen und Arbeitsbereichs-Einstellungen und speichert sie getrennt.
/// </summary>
public sealed class SettingsService(AppPaths paths, ILogger<SettingsService> logger)
{
    private readonly SettingsStore<GlobalSettings> _globalStore = SettingsStores.Global(paths);
    private readonly SettingsStore<WorkspaceSettings> _workspaceStore = SettingsStores.Workspace(paths);

    public GlobalSettings Global { get; private set; } = new();

    public WorkspaceSettings Workspace { get; private set; } = new();

    /// <summary>Die Liste „Zuletzt verwendet" hat sich geändert.</summary>
    public event EventHandler? RecentFilesChanged;

    /// <summary>Arbeitsbereichs-Einstellungen (z. B. Pixelraster) haben sich geändert.</summary>
    public event EventHandler? WorkspaceChanged;

    /// <summary>Lädt beide Dateien. Liefert alle Probleme, die dem Nutzer gemeldet werden sollen.</summary>
    public IReadOnlyList<SettingsIssue> Load()
    {
        var issues = new List<SettingsIssue>();

        var global = _globalStore.Load();
        Global = global.Value;
        Report(_globalStore.FilePath, global.Status, global.BackupPath, issues);

        var workspace = _workspaceStore.Load();
        Workspace = workspace.Value;
        Report(_workspaceStore.FilePath, workspace.Status, workspace.BackupPath, issues);

        RecentFilesChanged?.Invoke(this, EventArgs.Empty);
        WorkspaceChanged?.Invoke(this, EventArgs.Empty);
        return issues;
    }

    public void UpdateGlobal(GlobalSettings settings)
    {
        var previous = Global.RecentFiles;
        Global = GlobalSettings.Normalize(settings, out _);
        if (!previous.SequenceEqual(Global.RecentFiles))
        {
            RecentFilesChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void UpdateWorkspace(WorkspaceSettings settings)
    {
        Workspace = WorkspaceSettings.Normalize(settings, out _);
        WorkspaceChanged?.Invoke(this, EventArgs.Empty);
    }

    public void AddRecentFile(string path)
    {
        UpdateGlobal(Global.WithRecentFile(path));
        TrySaveGlobal();
    }

    public void RemoveRecentFile(string path)
    {
        UpdateGlobal(Global.WithoutRecentFile(path));
        TrySaveGlobal();
    }

    /// <summary>Speichert beide Dateien. Wirft bei Ein-/Ausgabefehlern, damit der Aufrufer sie melden kann.</summary>
    public void Save()
    {
        _globalStore.Save(Global);
        _workspaceStore.Save(Workspace);
        logger.LogInformation("Einstellungen gespeichert.");
    }

    /// <summary>Sofortiges Speichern nach kleinen Änderungen; Fehler werden nur protokolliert (beim Beenden erneuter Versuch).</summary>
    private void TrySaveGlobal()
    {
        try
        {
            _globalStore.Save(Global);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Globale Einstellungen konnten nicht sofort gespeichert werden.");
        }
    }

    private void Report(string path, SettingsLoadStatus status, string? backup, List<SettingsIssue> issues)
    {
        switch (status)
        {
            case SettingsLoadStatus.Loaded:
            case SettingsLoadStatus.Missing:
                logger.LogInformation("Einstellungen {File}: {Status}", Path.GetFileName(path), status);
                break;
            default:
                logger.LogWarning("Einstellungen {File}: {Status}, Sicherung: {Backup}", Path.GetFileName(path), status,
                    backup is null ? "-" : Path.GetFileName(backup));
                issues.Add(new SettingsIssue(path, status, backup));
                break;
        }
    }
}
