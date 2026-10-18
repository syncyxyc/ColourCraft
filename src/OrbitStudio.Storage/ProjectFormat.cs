using System.Text.Json;
using System.Text.Json.Serialization;

namespace OrbitStudio.Storage;

/// <summary>
/// Natives Projektformat „.orbit“ (Formatversion 1), vollständig beschrieben in docs/architecture.md:
/// ZIP-Container mit <c>manifest.json</c> (Dokument, Ebenenbaum, Eigenschaften, Metadaten, Prüfsummen) und je Pixelebene
/// <c>layers/NNNN.bgra</c> (rohes BGRA, vormultipliert, Zeilenabstand = Breite · 4) bzw. je Maske <c>masks/NNNN.mask</c>
/// (8 Bit je Pixel). Pixeldaten werden verlustfrei gespeichert (kein Umweg über gerades Alpha).
/// </summary>
public static class ProjectFormat
{
    public const string Extension = ".orbit";

    /// <summary>Kennung im Manifest; andere ZIP-Dateien werden als „kein Projekt“ abgewiesen.</summary>
    public const string FormatId = "orbit-studio-project";

    /// <summary>Aktuelle Formatversion. Ältere Versionen werden beim Laden migriert, neuere abgewiesen.</summary>
    public const int CurrentVersion = 1;

    public const string ManifestEntry = "manifest.json";

    /// <summary>Obergrenze für das Manifest (unkomprimiert).</summary>
    public const long MaxManifestBytes = 8L * 1024 * 1024;

    /// <summary>Höchstzahl an Ebenen und Gruppen.</summary>
    public const int MaxNodes = 10_000;

    /// <summary>Größte Verschachtelungstiefe von Gruppen.</summary>
    public const int MaxDepth = 64;

    public const int MaxNameLength = 1_000;

    public const int MaxMetadataEntries = 256;

    /// <summary>Standardgrenze für die Summe aller Pixel- und Maskendaten eines Projekts.</summary>
    public const long DefaultMaxTotalBytes = 8L * 1024 * 1024 * 1024;

    internal static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
}

/// <summary>Fehlerarten beim Laden und Speichern. Die Oberfläche übersetzt sie in deutsche Meldungen.</summary>
public enum ProjectError
{
    None,
    NotFound,
    AccessDenied,
    IoError,

    /// <summary>Keine ZIP-Datei oder kein ORBIT-Manifest.</summary>
    NotAProject,

    /// <summary>Beschädigt oder manipuliert (Manifest ungültig, Daten fehlen, falsche Länge, Prüfsumme falsch).</summary>
    Corrupt,

    /// <summary>Mit einer neueren Programmversion gespeichert.</summary>
    UnsupportedVersion,

    /// <summary>Bild zu groß oder Projekt braucht mehr Speicher als erlaubt.</summary>
    TooLarge,
    OutOfMemory,
    Cancelled,
}

/// <summary>Kontrollierte Ablehnung eines Projekts. <see cref="Detail"/> ist nur für das Protokoll (ohne Bildinhalte).</summary>
public sealed class ProjectException(ProjectError error, string detail) : Exception($"{error}: {detail}")
{
    public ProjectError Error { get; } = error;

    public string Detail { get; } = detail;
}

// --- Manifest (Formatversion 1) -----------------------------------------------------------------

public sealed class ProjectManifest
{
    public string Format { get; set; } = ProjectFormat.FormatId;

    public int FormatVersion { get; set; } = ProjectFormat.CurrentVersion;

    /// <summary>Programm und Version, die zuletzt gespeichert haben (nur Information).</summary>
    public string? SavedWith { get; set; }

    public DateTimeOffset? SavedUtc { get; set; }

    public ProjectDocumentInfo Document { get; set; } = new();

    /// <summary>Kinder der Wurzelgruppe von unten nach oben.</summary>
    public List<ProjectNode> Layers { get; set; } = [];

    /// <summary>Manifest-ID der aktiven Ebene (null = keine).</summary>
    public string? ActiveLayerId { get; set; }

    public Dictionary<string, string>? Metadata { get; set; }
}

public sealed class ProjectDocumentInfo
{
    public string Name { get; set; } = string.Empty;

    public int Width { get; set; }

    public int Height { get; set; }

    public double ResolutionDpi { get; set; }

    /// <summary>Einziger Wert in Version 1: „Bgra8Premultiplied“.</summary>
    public string ColorMode { get; set; } = ProjectNode.ColorModeBgra8Premultiplied;

    public string Background { get; set; } = "White";
}

public sealed class ProjectNode
{
    public const string ColorModeBgra8Premultiplied = "Bgra8Premultiplied";
    public const string PixelType = "pixel";
    public const string GroupType = "group";

    /// <summary>„pixel“ oder „group“.</summary>
    public string Type { get; set; } = PixelType;

    /// <summary>Eindeutige ID innerhalb des Manifests (nur für Verweise wie die aktive Ebene).</summary>
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public bool Visible { get; set; } = true;

    public double Opacity { get; set; } = 1;

    public string BlendMode { get; set; } = "Normal";

    public bool Locked { get; set; }

    /// <summary>Nur Pixelebenen.</summary>
    public ProjectData? Pixels { get; set; }

    /// <summary>Nur Pixelebenen, optional.</summary>
    public ProjectMask? Mask { get; set; }

    /// <summary>Nur Gruppen: Kinder von unten nach oben.</summary>
    public List<ProjectNode>? Children { get; set; }
}

public sealed class ProjectMask
{
    public ProjectData Data { get; set; } = new();

    public bool Enabled { get; set; } = true;
}

/// <summary>Verweis auf einen Datenblock im Container mit erwarteter Länge und SHA-256 (Hex, klein).</summary>
public sealed class ProjectData
{
    public string Path { get; set; } = string.Empty;

    public long Length { get; set; }

    public string Sha256 { get; set; } = string.Empty;
}

/// <summary>Kurzinfo nach der Prüfung eines Projekts (ohne Pixeldaten zu laden).</summary>
public sealed record ProjectSummary(int FormatVersion, string Name, int Width, int Height, int LayerCount, long DataBytes);
