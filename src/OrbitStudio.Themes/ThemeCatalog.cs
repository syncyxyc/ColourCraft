using System.Text.Json;
using System.Text.Json.Serialization;

namespace OrbitStudio.Themes;

public sealed record ThemeImportResult(Theme? Theme, IReadOnlyList<string> Errors)
{
    public bool Success => Theme is not null;
}

/// <summary>Import und Export von Theme-Dateien (versioniertes JSON). Import wirft nie, sondern liefert Fehlermeldungen.</summary>
public static class ThemeFile
{
    public const int CurrentSchemaVersion = 1;
    public const int MaxFileBytes = 64 * 1024;

    private sealed record Dto
    {
        public int SchemaVersion { get; init; }

        public string? Id { get; init; }

        public string? Name { get; init; }

        public ThemeKind? Kind { get; init; }

        public Dictionary<string, string>? Colors { get; init; }

        public double? Spacing { get; init; }

        public double? CornerRadius { get; init; }

        public double? UiScale { get; init; }
    }

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public static string Export(Theme theme) => JsonSerializer.Serialize(new Dto
    {
        SchemaVersion = CurrentSchemaVersion,
        Id = theme.Id,
        Name = theme.Name,
        Kind = theme.Kind,
        Colors = theme.Colors.OrderBy(kv => kv.Key, StringComparer.Ordinal).ToDictionary(kv => kv.Key, kv => kv.Value),
        Spacing = theme.Spacing,
        CornerRadius = theme.CornerRadius,
        UiScale = theme.UiScale,
    }, Options);

    /// <summary>
    /// Liest und prüft ein Theme. Nicht aktiviert wird, was unlesbar, zu groß, von einer neueren Version oder ungültig ist
    /// (<see cref="ThemeValidator"/>); die Fehlermeldungen sind deutsch. Fehlende Maßangaben bekommen Standardwerte.
    /// </summary>
    public static ThemeImportResult Import(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Fail("Die Datei ist leer.");
        }

        if (json.Length > MaxFileBytes)
        {
            return Fail("Die Datei ist zu groß für ein Theme.");
        }

        Dto? dto;
        try
        {
            dto = JsonSerializer.Deserialize<Dto>(json, Options);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            return Fail("Die Datei ist kein gültiges Theme (JSON nicht lesbar).");
        }

        if (dto is null)
        {
            return Fail("Die Datei ist kein gültiges Theme.");
        }

        if (dto.SchemaVersion > CurrentSchemaVersion)
        {
            return Fail("Das Theme stammt aus einer neueren Programmversion.");
        }

        if (dto.SchemaVersion < 1)
        {
            return Fail("Die Formatversion des Themes fehlt oder ist ungültig.");
        }

        var theme = new Theme(
            dto.Id?.Trim() ?? "",
            dto.Name?.Trim() ?? "",
            dto.Kind ?? (ThemeKind)(-1),
            (dto.Colors ?? []).ToDictionary(kv => kv.Key, kv => kv.Value?.ToUpperInvariant() ?? "", StringComparer.Ordinal),
            dto.Spacing ?? 8,
            dto.CornerRadius ?? 3,
            dto.UiScale ?? 1);
        var issues = ThemeValidator.Validate(theme);
        return issues.Count == 0 ? new ThemeImportResult(theme, []) : new ThemeImportResult(null, issues.Select(i => i.Message).ToList());
    }

    private static ThemeImportResult Fail(string message) => new(null, [message]);
}

/// <summary>
/// Verfügbare Themes (eingebaut + importiert) und das aktive. Ein ungültiges oder fehlendes Theme macht die Anwendung
/// nie unbenutzbar: <see cref="Resolve"/> fällt auf „Hell“ zurück.
/// </summary>
public sealed class ThemeCatalog
{
    private readonly List<Theme> _custom = [];

    public IReadOnlyList<Theme> All => [.. Theme.BuiltIn, .. _custom];

    /// <summary>Fügt ein importiertes Theme hinzu; gleiche ID ersetzt ein früheres eigenes Theme. Eingebaute IDs sind geschützt.</summary>
    public ThemeImportResult Add(Theme theme)
    {
        var issues = ThemeValidator.Validate(theme);
        if (issues.Count > 0)
        {
            return new ThemeImportResult(null, issues.Select(i => i.Message).ToList());
        }

        if (Theme.BuiltIn.Any(t => t.Id == theme.Id))
        {
            return new ThemeImportResult(null, [$"Die ID „{theme.Id}“ ist für eingebaute Themes reserviert."]);
        }

        _custom.RemoveAll(t => t.Id == theme.Id);
        _custom.Add(theme);
        return new ThemeImportResult(theme, []);
    }

    public bool Remove(string id) => _custom.RemoveAll(t => t.Id == id) > 0;

    /// <summary>Theme zur ID; unbekannt oder ungültig → „Hell“. <paramref name="fellBack"/> meldet den Rückfall.</summary>
    public Theme Resolve(string? id, out bool fellBack)
    {
        var theme = All.FirstOrDefault(t => t.Id == id);
        if (theme is null || ThemeValidator.Validate(theme).Count > 0)
        {
            fellBack = true;
            return Theme.Light;
        }

        fellBack = false;
        return theme;
    }
}
