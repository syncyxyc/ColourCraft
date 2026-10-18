namespace OrbitStudio.Configuration;

/// <summary>Gemeinsamer Vertrag für versionierte Einstellungsdateien.</summary>
public interface IVersionedSettings
{
    int SchemaVersion { get; }
}

/// <summary>Globale Einstellungen: Sprache, zuletzt verwendete Dateien, Vorgaben für neue Dokumente.</summary>
public sealed record GlobalSettings : IVersionedSettings
{
    public const int CurrentSchemaVersion = 1;
    public const int MaxRecentFiles = 10;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    public string Language { get; init; } = "de-DE";

    public IReadOnlyList<string> RecentFiles { get; init; } = [];

    public int NewDocumentWidth { get; init; } = 1920;

    public int NewDocumentHeight { get; init; } = 1080;

    /// <summary>Bringt geladene Werte in gültige Bereiche. Liefert true, wenn etwas korrigiert wurde.</summary>
    public static GlobalSettings Normalize(GlobalSettings s, out bool changed)
    {
        var defaults = new GlobalSettings();
        var language = s.Language is "de-DE" ? s.Language : defaults.Language;
        var recent = (s.RecentFiles ?? [])
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MaxRecentFiles)
            .ToList();
        var width = s.NewDocumentWidth is > 0 and <= 65_535 ? s.NewDocumentWidth : defaults.NewDocumentWidth;
        var height = s.NewDocumentHeight is > 0 and <= 65_535 ? s.NewDocumentHeight : defaults.NewDocumentHeight;

        var result = s with
        {
            SchemaVersion = CurrentSchemaVersion,
            Language = language,
            RecentFiles = recent,
            NewDocumentWidth = width,
            NewDocumentHeight = height,
        };
        changed = language != s.Language
                  || width != s.NewDocumentWidth
                  || height != s.NewDocumentHeight
                  || s.RecentFiles is null
                  || !recent.SequenceEqual(s.RecentFiles);
        return result;
    }
}

/// <summary>Arbeitsbereich: Fensterposition und -zustand. Panels und Layouts folgen in M8/M9.</summary>
public sealed record WorkspaceSettings : IVersionedSettings
{
    public const int CurrentSchemaVersion = 1;
    public const double MinWindowWidth = 640;
    public const double MinWindowHeight = 480;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    /// <summary>Linke obere Ecke in geräteunabhängigen Einheiten; null = vom System platzieren lassen.</summary>
    public double? WindowLeft { get; init; }

    public double? WindowTop { get; init; }

    public double WindowWidth { get; init; } = 1280;

    public double WindowHeight { get; init; } = 800;

    public bool IsMaximized { get; init; }

    public static WorkspaceSettings Normalize(WorkspaceSettings s, out bool changed)
    {
        var defaults = new WorkspaceSettings();
        var width = IsValidSize(s.WindowWidth, MinWindowWidth) ? s.WindowWidth : defaults.WindowWidth;
        var height = IsValidSize(s.WindowHeight, MinWindowHeight) ? s.WindowHeight : defaults.WindowHeight;
        var left = s.WindowLeft is { } l && double.IsFinite(l) ? s.WindowLeft : null;
        var top = s.WindowTop is { } t && double.IsFinite(t) ? s.WindowTop : null;

        var result = s with
        {
            SchemaVersion = CurrentSchemaVersion,
            WindowWidth = width,
            WindowHeight = height,
            WindowLeft = left,
            WindowTop = top,
        };
        changed = width != s.WindowWidth || height != s.WindowHeight || left != s.WindowLeft || top != s.WindowTop;
        return result;
    }

    private static bool IsValidSize(double value, double min) => double.IsFinite(value) && value >= min && value <= 32_000;
}
