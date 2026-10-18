using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using OrbitStudio.Domain.Documents;
using OrbitStudio.Domain.Imaging;
using OrbitStudio.Domain.Layers;

namespace OrbitStudio.Storage;

/// <summary>Optionen beim Laden (Migrationen, Speichergrenze).</summary>
public sealed record ProjectReadOptions(ProjectMigrator? Migrator = null, long MaxTotalBytes = ProjectFormat.DefaultMaxTotalBytes);

/// <summary>
/// Schreibt und liest Projekte über Streams (ohne Dateisystemlogik; sichere Dateiersetzung übernimmt der Aufrufer).
/// Beim Lesen wird zuerst das ganze Manifest geprüft (Format, Version, Größen, Struktur, Datenlängen gegen die
/// Speichergrenze), erst danach werden Pixelpuffer angelegt. Jeder Datenblock muss genau die erwartete Länge und die
/// im Manifest stehende SHA-256-Prüfsumme haben.
/// </summary>
public static class ProjectStore
{
    private const int CopyBufferSize = 1 << 20;

    // --- Schreiben ---------------------------------------------------------------------------------

    /// <summary>
    /// Schreibt das Dokument. Während des Schreibens darf das Dokument nicht verändert werden (der Aufrufer sperrt
    /// Bearbeitungen). Die Auswahl und die Ansicht werden nicht gespeichert.
    /// </summary>
    public static void Write(ImageDocument document, Stream output, string? savedWith, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(output);
        var ids = new Dictionary<LayerNode, string>();
        var manifest = new ProjectManifest
        {
            SavedWith = savedWith,
            SavedUtc = DateTimeOffset.UtcNow,
            Document = new ProjectDocumentInfo
            {
                Name = document.Name,
                Width = document.Width,
                Height = document.Height,
                ResolutionDpi = document.ResolutionDpi,
                Background = document.Background.ToString(),
            },
            Metadata = document.Metadata.Count == 0 ? null : new Dictionary<string, string>(document.Metadata),
        };

        using (var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            var counter = 0;
            List<ProjectNode> Nodes(LayerGroup group) => group.Children.Select(child =>
            {
                cancellation.ThrowIfCancellationRequested();
                var id = (++counter).ToString("D4", CultureInfo.InvariantCulture);
                ids[child] = id;
                var node = new ProjectNode
                {
                    Id = id,
                    Name = child.Name,
                    Visible = child.IsVisible,
                    Opacity = child.Opacity,
                    BlendMode = child.BlendMode.ToString(),
                    Locked = child.IsLocked,
                };
                switch (child)
                {
                    case PixelLayer layer:
                        node.Type = ProjectNode.PixelType;
                        node.Pixels = WriteData(zip, $"layers/{id}.bgra", layer.Pixels.Data, cancellation);
                        if (layer.Mask is { } mask)
                        {
                            node.Mask = new ProjectMask
                            {
                                Data = WriteData(zip, $"masks/{id}.mask", mask.Data, cancellation),
                                Enabled = mask.IsEnabled,
                            };
                        }

                        break;
                    case LayerGroup nested:
                        node.Type = ProjectNode.GroupType;
                        node.Children = Nodes(nested);
                        break;
                }

                return node;
            }).ToList();

            manifest.Layers = Nodes(document.Root);
            manifest.ActiveLayerId = document.ActiveLayer is { } active && ids.TryGetValue(active, out var activeId) ? activeId : null;

            // Manifest zuletzt (enthält die Prüfsummen der Daten).
            var entry = zip.CreateEntry(ProjectFormat.ManifestEntry, CompressionLevel.Optimal);
            using var stream = entry.Open();
            JsonSerializer.Serialize(stream, manifest, ProjectFormat.Json);
        }

        output.Flush();
    }

    private static ProjectData WriteData(ZipArchive zip, string path, ReadOnlySpan<byte> data, CancellationToken cancellation)
    {
        // „Fastest“: große Pixelmengen, Schreibzeit wichtiger als die letzten Prozent Dateigröße (E-044).
        var entry = zip.CreateEntry(path, CompressionLevel.Fastest);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        using (var stream = entry.Open())
        {
            for (var offset = 0; offset < data.Length; offset += CopyBufferSize)
            {
                cancellation.ThrowIfCancellationRequested();
                var chunk = data.Slice(offset, Math.Min(CopyBufferSize, data.Length - offset));
                stream.Write(chunk);
                hash.AppendData(chunk);
            }
        }

        return new ProjectData { Path = path, Length = data.Length, Sha256 = Convert.ToHexStringLower(hash.GetHashAndReset()) };
    }

    // --- Prüfen und Lesen --------------------------------------------------------------------------

    /// <summary>Prüft Container und Manifest, ohne Pixeldaten zu lesen. Wirft <see cref="ProjectException"/>.</summary>
    public static ProjectSummary Inspect(Stream input, ProjectReadOptions? options = null)
    {
        using var zip = OpenZip(input);
        var (manifest, version) = ReadManifest(zip, options ?? new ProjectReadOptions());
        var bytes = Validate(manifest, zip, options ?? new ProjectReadOptions());
        return new ProjectSummary(version, manifest.Document.Name, manifest.Document.Width, manifest.Document.Height,
            CountNodes(manifest.Layers), bytes);
    }

    /// <summary>
    /// Liest ein Projekt vollständig. Wirft <see cref="ProjectException"/> (kontrollierte Ablehnung) oder
    /// <see cref="OperationCanceledException"/>. Das Dokument ist danach unverändert (nicht als geändert markiert).
    /// </summary>
    public static ImageDocument Read(Stream input, ProjectReadOptions? options = null, CancellationToken cancellation = default)
    {
        options ??= new ProjectReadOptions();
        using var zip = OpenZip(input);
        var (manifest, _) = ReadManifest(zip, options);
        Validate(manifest, zip, options);

        var info = manifest.Document;
        ImageDocument document;
        try
        {
            document = ImageDocument.CreateEmpty(info.Name, info.Width, info.Height, info.ResolutionDpi,
                Enum.Parse<DocumentBackground>(info.Background));
        }
        catch (DocumentValidationException ex)
        {
            throw new ProjectException(ProjectError.TooLarge, ex.Message);
        }

        var byId = new Dictionary<string, LayerNode>(StringComparer.Ordinal);
        void Build(LayerGroup parent, IEnumerable<ProjectNode> nodes)
        {
            foreach (var n in nodes)
            {
                cancellation.ThrowIfCancellationRequested();
                LayerNode node;
                if (n.Type == ProjectNode.PixelType)
                {
                    var pixels = PixelBuffer.Create(info.Width, info.Height);
                    ReadData(zip, n.Pixels!, pixels.Data, cancellation);
                    var layer = new PixelLayer(n.Name, pixels);
                    if (n.Mask is { } m)
                    {
                        var maskBytes = new byte[(long)info.Width * info.Height];
                        ReadData(zip, m.Data, maskBytes, cancellation);
                        layer.Mask = LayerMask.FromBytes(info.Width, info.Height, maskBytes);
                        layer.Mask.IsEnabled = m.Enabled;
                    }

                    node = layer;
                }
                else
                {
                    var group = new LayerGroup(n.Name);
                    node = group;
                    Build(group, n.Children ?? []);
                }

                node.IsVisible = n.Visible;
                node.Opacity = n.Opacity;
                node.BlendMode = Enum.Parse<BlendMode>(n.BlendMode);
                node.IsLocked = n.Locked;
                parent.Add(node);
                byId[n.Id] = node;
            }
        }

        Build(document.Root, manifest.Layers);
        foreach (var (key, value) in manifest.Metadata ?? [])
        {
            document.Metadata[key] = value;
        }

        document.ActiveLayer = manifest.ActiveLayerId is { } activeId && byId.TryGetValue(activeId, out var active)
            ? active
            : document.Root.Children.LastOrDefault();
        return document;
    }

    private static ZipArchive OpenZip(Stream input)
    {
        try
        {
            return new ZipArchive(input, ZipArchiveMode.Read, leaveOpen: true);
        }
        catch (InvalidDataException ex)
        {
            throw new ProjectException(ProjectError.NotAProject, "Kein ZIP-Container: " + ex.Message);
        }
    }

    private static (ProjectManifest Manifest, int OriginalVersion) ReadManifest(ZipArchive zip, ProjectReadOptions options)
    {
        var entry = zip.GetEntry(ProjectFormat.ManifestEntry)
                    ?? throw new ProjectException(ProjectError.NotAProject, "manifest.json fehlt.");
        if (entry.Length > ProjectFormat.MaxManifestBytes)
        {
            throw new ProjectException(ProjectError.Corrupt, "manifest.json ist zu groß.");
        }

        JsonObject json;
        try
        {
            using var stream = entry.Open();
            var bytes = ReadExactly(stream, entry.Length);
            json = JsonNode.Parse(bytes) as JsonObject ?? throw new ProjectException(ProjectError.Corrupt, "Manifest ist kein Objekt.");
        }
        catch (Exception ex) when (ex is JsonException or InvalidDataException)
        {
            throw new ProjectException(ProjectError.Corrupt, "Manifest unlesbar: " + ex.Message);
        }

        if (json["format"]?.GetValueKind() != JsonValueKind.String || (string?)json["format"] != ProjectFormat.FormatId)
        {
            throw new ProjectException(ProjectError.NotAProject, "Falsche Formatkennung.");
        }

        var original = (options.Migrator ?? ProjectMigrator.Default).Migrate(json);
        try
        {
            var manifest = json.Deserialize<ProjectManifest>(ProjectFormat.Json)
                           ?? throw new ProjectException(ProjectError.Corrupt, "Manifest leer.");
            return (manifest, original);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException)
        {
            throw new ProjectException(ProjectError.Corrupt, "Manifest passt nicht zum Schema: " + ex.Message);
        }
    }

    /// <summary>Prüft das Manifest vollständig gegen Regeln und Container; liefert die Summe der Datenbytes.</summary>
    private static long Validate(ProjectManifest manifest, ZipArchive zip, ProjectReadOptions options)
    {
        static ProjectException Corrupt(string detail) => new(ProjectError.Corrupt, detail);

        var info = manifest.Document ?? throw Corrupt("document fehlt.");
        if (string.IsNullOrWhiteSpace(info.Name) || info.Name.Length > ProjectFormat.MaxNameLength)
        {
            throw Corrupt("Ungültiger Dokumentname.");
        }

        var size = DocumentLimits.ValidateSize(info.Width, info.Height);
        if (size == SizeValidationResult.NonPositive)
        {
            throw Corrupt("Ungültige Bildgröße.");
        }

        if (size != SizeValidationResult.Valid)
        {
            throw new ProjectException(ProjectError.TooLarge, $"Bildgröße {info.Width}x{info.Height}.");
        }

        if (!DocumentLimits.IsValidResolution(info.ResolutionDpi))
        {
            throw Corrupt("Ungültige Auflösung.");
        }

        if (info.ColorMode != ProjectNode.ColorModeBgra8Premultiplied)
        {
            throw Corrupt("Unbekannter Farbmodus.");
        }

        if (!Enum.TryParse<DocumentBackground>(info.Background, out var background) || !Enum.IsDefined(background)
            || int.TryParse(info.Background, out _))
        {
            throw Corrupt("Unbekannter Hintergrund.");
        }

        if (manifest.Metadata is { Count: > ProjectFormat.MaxMetadataEntries })
        {
            throw Corrupt("Zu viele Metadaten.");
        }

        var pixelBytes = (long)info.Width * info.Height * 4;
        var maskBytes = (long)info.Width * info.Height;
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var paths = new HashSet<string>(StringComparer.Ordinal);
        long total = 0;
        var count = 0;

        void CheckData(ProjectData? data, long expected)
        {
            if (data is null || string.IsNullOrEmpty(data.Path) || data.Length != expected
                || data.Sha256 is not { Length: 64 } || !data.Sha256.All(Uri.IsHexDigit))
            {
                throw Corrupt("Datenverweis ungültig.");
            }

            if (!paths.Add(data.Path))
            {
                throw Corrupt("Datenblock mehrfach verwendet.");
            }

            var entry = zip.GetEntry(data.Path) ?? throw Corrupt("Datenblock fehlt.");
            if (entry.Length != expected)
            {
                throw Corrupt("Datenblock hat die falsche Länge.");
            }

            total += expected;
            if (total > options.MaxTotalBytes)
            {
                throw new ProjectException(ProjectError.TooLarge, $"Mehr als {options.MaxTotalBytes} Byte Bilddaten.");
            }
        }

        void Walk(List<ProjectNode>? nodes, int depth)
        {
            if (nodes is null)
            {
                throw Corrupt("Ebenenliste fehlt.");
            }

            if (depth > ProjectFormat.MaxDepth)
            {
                throw Corrupt("Gruppen zu tief verschachtelt.");
            }

            foreach (var n in nodes)
            {
                if (n is null || ++count > ProjectFormat.MaxNodes)
                {
                    throw Corrupt("Zu viele oder leere Ebenen.");
                }

                if (string.IsNullOrEmpty(n.Id) || !ids.Add(n.Id))
                {
                    throw Corrupt("Ebenen-ID fehlt oder ist doppelt.");
                }

                if (string.IsNullOrWhiteSpace(n.Name) || n.Name.Length > ProjectFormat.MaxNameLength)
                {
                    throw Corrupt("Ungültiger Ebenenname.");
                }

                if (!double.IsFinite(n.Opacity) || n.Opacity < 0 || n.Opacity > 1)
                {
                    throw Corrupt("Deckkraft außerhalb 0–1.");
                }

                if (!Enum.TryParse<BlendMode>(n.BlendMode, out var mode) || !Enum.IsDefined(mode)
                    || int.TryParse(n.BlendMode, out _))
                {
                    throw Corrupt("Unbekannter Mischmodus.");
                }

                switch (n.Type)
                {
                    case ProjectNode.PixelType when n.Children is null:
                        CheckData(n.Pixels, pixelBytes);
                        if (n.Mask is not null)
                        {
                            CheckData(n.Mask.Data, maskBytes);
                        }

                        break;
                    case ProjectNode.GroupType when n.Pixels is null && n.Mask is null:
                        Walk(n.Children, depth + 1);
                        break;
                    default:
                        throw Corrupt("Unbekannter oder widersprüchlicher Ebenentyp.");
                }
            }
        }

        Walk(manifest.Layers, 0);
        if (count == 0)
        {
            throw Corrupt("Projekt ohne Ebenen.");
        }

        if (manifest.ActiveLayerId is { } active && !ids.Contains(active))
        {
            throw Corrupt("Aktive Ebene unbekannt.");
        }

        // Grobe Speicherprüfung vor dem Anlegen der Puffer.
        var available = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
        if (available > 0 && total > available)
        {
            throw new ProjectException(ProjectError.OutOfMemory, $"{total} Byte benötigt, {available} verfügbar.");
        }

        return total;
    }

    private static void ReadData(ZipArchive zip, ProjectData data, Span<byte> target, CancellationToken cancellation)
    {
        var entry = zip.GetEntry(data.Path)!;
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        using (var stream = entry.Open())
        {
            var offset = 0;
            try
            {
                while (offset < target.Length)
                {
                    cancellation.ThrowIfCancellationRequested();
                    var read = stream.Read(target.Slice(offset, Math.Min(CopyBufferSize, target.Length - offset)));
                    if (read == 0)
                    {
                        throw new ProjectException(ProjectError.Corrupt, "Datenblock zu kurz.");
                    }

                    hash.AppendData(target.Slice(offset, read));
                    offset += read;
                }

                Span<byte> extra = stackalloc byte[1];
                if (stream.Read(extra) != 0)
                {
                    throw new ProjectException(ProjectError.Corrupt, "Datenblock zu lang.");
                }
            }
            catch (InvalidDataException ex)
            {
                throw new ProjectException(ProjectError.Corrupt, "Datenblock nicht entpackbar: " + ex.Message);
            }
        }

        if (!string.Equals(Convert.ToHexStringLower(hash.GetHashAndReset()), data.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new ProjectException(ProjectError.Corrupt, "Prüfsumme stimmt nicht.");
        }
    }

    private static byte[] ReadExactly(Stream stream, long length)
    {
        var buffer = new byte[length];
        var offset = 0;
        while (offset < length)
        {
            var read = stream.Read(buffer, offset, (int)(length - offset));
            if (read == 0)
            {
                throw new InvalidDataException("Manifest zu kurz.");
            }

            offset += read;
        }

        return buffer;
    }

    private static int CountNodes(IEnumerable<ProjectNode> nodes) =>
        nodes.Sum(n => 1 + (n.Children is null ? 0 : CountNodes(n.Children)));
}
