using System.Text.Json.Nodes;

namespace OrbitStudio.Storage;

/// <summary>Hebt ein Manifest von <see cref="FromVersion"/> auf <see cref="FromVersion"/> + 1 an.</summary>
public interface IProjectMigration
{
    int FromVersion { get; }

    /// <summary>Verändert das Manifest an Ort und Stelle. Darf <see cref="ProjectException"/> werfen.</summary>
    void Apply(JsonObject manifest);
}

/// <summary>
/// Migrationskette: Ein Manifest der Version v wird schrittweise bis zur aktuellen Version angehoben
/// (v → v + 1 → …). Fehlt ein Schritt, wird die Datei als nicht unterstützt abgewiesen. Neuere Versionen als die
/// aktuelle werden nie „herabgestuft“, sondern abgewiesen. Formatversion 1 ist die erste veröffentlichte Version;
/// die Standardkette ist daher leer und wird durch Tests mit eigenen Migrationen geprüft.
/// </summary>
public sealed class ProjectMigrator
{
    private readonly Dictionary<int, IProjectMigration> _steps;

    public ProjectMigrator(IEnumerable<IProjectMigration> migrations, int currentVersion = ProjectFormat.CurrentVersion)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(currentVersion);
        CurrentVersion = currentVersion;
        _steps = migrations.ToDictionary(m => m.FromVersion);
    }

    public static ProjectMigrator Default { get; } = new([]);

    public int CurrentVersion { get; }

    /// <summary>Liefert die Version vor der Migration; das Manifest hat danach <see cref="CurrentVersion"/>.</summary>
    public int Migrate(JsonObject manifest)
    {
        var version = manifest["formatVersion"] is JsonValue v && v.TryGetValue<int>(out var n)
            ? n
            : throw new ProjectException(ProjectError.Corrupt, "formatVersion fehlt oder ist keine Zahl.");
        if (version < 1)
        {
            throw new ProjectException(ProjectError.Corrupt, $"Ungültige formatVersion {version}.");
        }

        if (version > CurrentVersion)
        {
            throw new ProjectException(ProjectError.UnsupportedVersion,
                $"formatVersion {version} ist neuer als {CurrentVersion}.");
        }

        var original = version;
        while (version < CurrentVersion)
        {
            if (!_steps.TryGetValue(version, out var step))
            {
                throw new ProjectException(ProjectError.UnsupportedVersion, $"Keine Migration von Version {version}.");
            }

            step.Apply(manifest);
            version++;
            manifest["formatVersion"] = version;
        }

        return original;
    }
}
