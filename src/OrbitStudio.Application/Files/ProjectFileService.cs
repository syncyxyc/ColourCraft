using Microsoft.Extensions.Logging;
using OrbitStudio.Configuration;
using OrbitStudio.Domain.Documents;
using OrbitStudio.Storage;

namespace OrbitStudio.Application.Files;

public sealed record OpenProjectResult(ImageDocument? Document, ProjectError Error, string Path)
{
    public bool Success => Error == ProjectError.None && Document is not null;
}

public sealed record SaveProjectResult(ProjectError Error, string Path)
{
    public bool Success => Error == ProjectError.None;
}

/// <summary>
/// Speichern und Öffnen von „.orbit“-Projekten im Hintergrund (Spezifikation 11.2).
/// Sichere Dateiersetzung: Es wird in eine temporäre Datei im Zielordner geschrieben, auf den Datenträger geschrieben,
/// erneut geöffnet und geprüft und erst dann über die Zieldatei verschoben. Ein Abbruch oder Fehler – auch während des
/// Schreibens – lässt eine vorhandene Datei unverändert und entfernt die temporäre Datei.
/// </summary>
public sealed class ProjectFileService(ILogger<ProjectFileService> logger)
{
    /// <summary>Migrationen und Speichergrenze beim Laden (Tests können eigene setzen).</summary>
    public ProjectReadOptions ReadOptions { get; init; } = new();

    public static bool IsProjectPath(string path) =>
        string.Equals(Path.GetExtension(path), ProjectFormat.Extension, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Schreibt das Dokument nach <paramref name="path"/>. Ändert den Dokumentzustand nicht (Speicherpfad und
    /// Änderungsmarkierung setzt der Aufrufer, „Kopie speichern“ lässt sie unverändert).
    /// Während des Schreibens darf das Dokument nicht bearbeitet werden.
    /// </summary>
    public async Task<SaveProjectResult> SaveAsync(ImageDocument document, string path,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var full = Path.GetFullPath(path);
        var tempPath = Path.Combine(Path.GetDirectoryName(full)!, $".{Path.GetFileName(full)}.{Guid.NewGuid():N}.tmp");
        try
        {
            await Task.Run(() =>
            {
                if (!document.Metadata.ContainsKey("created"))
                {
                    document.Metadata["created"] = DateTimeOffset.UtcNow.ToString("O", System.Globalization.CultureInfo.InvariantCulture);
                }

                using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
                {
                    ProjectStore.Write(document, stream, $"{AppInfo.ProductName} {AppInfo.Version}", cancellationToken);
                    stream.Flush(flushToDisk: true);
                    stream.Position = 0;
                    ProjectStore.Inspect(stream, ReadOptions); // geschriebenes Projekt lässt sich wieder öffnen
                }

                cancellationToken.ThrowIfCancellationRequested();
                File.Move(tempPath, full, overwrite: true);
            }, cancellationToken).ConfigureAwait(true);

            logger.LogInformation("Projekt gespeichert: {Width}x{Height}, {Layers} Ebenen", document.Width, document.Height,
                document.LayerCount);
            return new SaveProjectResult(ProjectError.None, full);
        }
        catch (Exception ex) when (Classify(ex) is { } error)
        {
            logger.LogWarning(ex, "Speichern fehlgeschlagen ({Error}): {File}", error, Path.GetFileName(full));
            return new SaveProjectResult(error, full);
        }
        finally
        {
            TryDelete(tempPath);
        }
    }

    /// <summary>Öffnet ein Projekt; das Dokument heißt wie die Datei und gilt als gespeichert.</summary>
    public async Task<OpenProjectResult> OpenAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var full = Path.GetFullPath(path);
        try
        {
            var document = await Task.Run(() =>
            {
                using var stream = new FileStream(full, FileMode.Open, FileAccess.Read, FileShare.Read);
                return ProjectStore.Read(stream, ReadOptions, cancellationToken);
            }, cancellationToken).ConfigureAwait(true);

            document.Name = Path.GetFileName(full);
            document.MarkSaved(full);
            logger.LogInformation("Projekt geöffnet: {Width}x{Height}, {Layers} Ebenen", document.Width, document.Height,
                document.LayerCount);
            return new OpenProjectResult(document, ProjectError.None, full);
        }
        catch (Exception ex) when (Classify(ex) is { } error)
        {
            logger.LogWarning(ex, "Öffnen fehlgeschlagen ({Error}): {File}", error, Path.GetFileName(full));
            return new OpenProjectResult(null, error, full);
        }
    }

    private static ProjectError? Classify(Exception ex) => ex switch
    {
        ProjectException project => project.Error,
        OperationCanceledException => ProjectError.Cancelled,
        FileNotFoundException or DirectoryNotFoundException => ProjectError.NotFound,
        UnauthorizedAccessException => ProjectError.AccessDenied,
        OutOfMemoryException => ProjectError.OutOfMemory,
        InvalidDataException => ProjectError.Corrupt,
        IOException => ProjectError.IoError,
        _ => null,
    };

    private void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Temporäre Datei konnte nicht entfernt werden.");
        }
    }
}
