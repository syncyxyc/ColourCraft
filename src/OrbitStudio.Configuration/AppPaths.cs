namespace OrbitStudio.Configuration;

/// <summary>
/// Speicherorte für Einstellungen und Logs. Der Basisordner ist injizierbar, damit Tests isoliert laufen.
/// </summary>
public sealed class AppPaths
{
    public AppPaths(string settingsRoot, string localDataRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(settingsRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(localDataRoot);
        SettingsDirectory = settingsRoot;
        LocalDataDirectory = localDataRoot;
    }

    /// <summary>Standardorte: %APPDATA%\OrbitStudio und %LOCALAPPDATA%\OrbitStudio.</summary>
    public static AppPaths CreateDefault() => new(
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppInfo.DataFolderName),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppInfo.DataFolderName));

    public string SettingsDirectory { get; }

    public string LocalDataDirectory { get; }

    public string GlobalSettingsFile => Path.Combine(SettingsDirectory, "settings.json");

    public string WorkspaceSettingsFile => Path.Combine(SettingsDirectory, "workspace.json");

    public string LogDirectory => Path.Combine(LocalDataDirectory, "logs");
}
