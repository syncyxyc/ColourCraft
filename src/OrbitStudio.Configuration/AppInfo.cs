using System.Reflection;

namespace OrbitStudio.Configuration;

/// <summary>
/// Zentrale Produktinformationen. Der Produktname ist nur hier definiert und an dieser Stelle austauschbar.
/// </summary>
public static class AppInfo
{
    public const string ProductName = "ORBIT Studio";

    /// <summary>Ordnername für Einstellungen, Logs und Plugins im Benutzerprofil.</summary>
    public const string DataFolderName = "OrbitStudio";

    /// <summary>Dateiendung des nativen Projektformats.</summary>
    public const string ProjectFileExtension = ".orbit";

    public static string Version { get; } =
        typeof(AppInfo).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            .Split('+')[0]
        ?? "0.0.0";
}
