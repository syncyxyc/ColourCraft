using System.IO.Compression;
using System.Text;
using System.Text.Json.Nodes;
using OrbitStudio.Domain.Documents;
using OrbitStudio.Domain.Imaging;
using OrbitStudio.Domain.Layers;

namespace OrbitStudio.Storage.Tests;

public class ProjectStoreTests
{
    /// <summary>Dokument mit allem, was das Format speichern muss: Gruppen, Masken, Eigenschaften, Teiltransparenz.</summary>
    private static ImageDocument Sample()
    {
        var document = ImageDocument.Create("Probe", 7, 5, "Hintergrund", resolutionDpi: 300, background: DocumentBackground.Transparent);
        var background = document.PixelLayers.Single();
        background.Pixels.SetPixel(0, 0, new Bgra32(10, 20, 30, 40)); // vormultipliert, teiltransparent
        background.Pixels.SetPixel(6, 4, Bgra32.White);

        var group = new LayerGroup("Gruppe") { Opacity = 0.5, BlendMode = BlendMode.Multiply, IsVisible = false };
        document.Root.Add(group);
        var nested = new LayerGroup("Innen") { IsLocked = true };
        group.Add(nested);
        var top = new PixelLayer("Oben", PixelBuffer.Create(7, 5)) { BlendMode = BlendMode.Overlay, Opacity = 0.25 };
        top.Pixels.SetPixel(3, 2, new Bgra32(1, 2, 3, 4));
        top.Mask = LayerMask.CreateRevealAll(7, 5);
        top.Mask.Set(1, 1, 77);
        top.Mask.IsEnabled = false;
        nested.Add(top);
        document.ActiveLayer = top;
        document.Metadata["autor"] = "Test";
        return document;
    }

    private static byte[] Save(ImageDocument document)
    {
        using var stream = new MemoryStream();
        ProjectStore.Write(document, stream, "Test 1.0");
        return stream.ToArray();
    }

    private static ImageDocument Load(byte[] bytes, ProjectReadOptions? options = null)
    {
        using var stream = new MemoryStream(bytes);
        return ProjectStore.Read(stream, options);
    }

    private static ProjectError LoadError(byte[] bytes, ProjectReadOptions? options = null) =>
        Should.Throw<ProjectException>(() => Load(bytes, options)).Error;

    /// <summary>Container neu schreiben, dabei Einträge verändern (null = entfernen).</summary>
    private static byte[] Rewrite(byte[] bytes, Func<string, byte[], byte[]?> change)
    {
        var entries = new List<(string Name, byte[] Data)>();
        using (var zip = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read))
        {
            foreach (var entry in zip.Entries)
            {
                using var s = entry.Open();
                using var copy = new MemoryStream();
                s.CopyTo(copy);
                if (change(entry.FullName, copy.ToArray()) is { } data)
                {
                    entries.Add((entry.FullName, data));
                }
            }
        }

        using var output = new MemoryStream();
        using (var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, data) in entries)
            {
                using var s = zip.CreateEntry(name).Open();
                s.Write(data);
            }
        }

        return output.ToArray();
    }

    private static byte[] EditManifest(byte[] bytes, Action<JsonObject> edit) => Rewrite(bytes, (name, data) =>
    {
        if (name != ProjectFormat.ManifestEntry)
        {
            return data;
        }

        var json = JsonNode.Parse(data)!.AsObject();
        edit(json);
        return Encoding.UTF8.GetBytes(json.ToJsonString());
    });

    private static JsonObject FirstLayer(JsonObject manifest) => manifest["layers"]![0]!.AsObject();

    [Fact]
    public void RoundTrip_PreservesStructurePropertiesPixelsMasksAndMetadata()
    {
        var original = Sample();
        var loaded = Load(Save(original));

        (loaded.Width, loaded.Height, loaded.ResolutionDpi, loaded.Background).ShouldBe((7, 5, 300.0, DocumentBackground.Transparent));
        loaded.Name.ShouldBe("Probe");
        loaded.IsDirty.ShouldBeFalse();
        loaded.Metadata["autor"].ShouldBe("Test");

        var background = (PixelLayer)loaded.Root.Children[0];
        background.Name.ShouldBe("Hintergrund");
        background.Pixels.ContentEquals(original.PixelLayers.First().Pixels).ShouldBeTrue();
        background.Pixels.GetPixel(0, 0).ShouldBe(new Bgra32(10, 20, 30, 40)); // vormultipliert exakt

        var group = (LayerGroup)loaded.Root.Children[1];
        (group.Name, group.Opacity, group.BlendMode, group.IsVisible).ShouldBe(("Gruppe", 0.5, BlendMode.Multiply, false));
        var nested = (LayerGroup)group.Children.Single();
        nested.IsLocked.ShouldBeTrue();
        var top = (PixelLayer)nested.Children.Single();
        (top.Name, top.Opacity, top.BlendMode).ShouldBe(("Oben", 0.25, BlendMode.Overlay));
        top.Pixels.GetPixel(3, 2).ShouldBe(new Bgra32(1, 2, 3, 4));
        top.Mask!.Get(1, 1).ShouldBe((byte)77);
        top.Mask.Get(0, 0).ShouldBe((byte)255);
        top.Mask.IsEnabled.ShouldBeFalse();
        loaded.ActiveLayer.ShouldBe(top);
    }

    [Fact]
    public void Container_HasManifestAndRawDataWithChecksums()
    {
        using var zip = new ZipArchive(new MemoryStream(Save(Sample())), ZipArchiveMode.Read);
        zip.Entries.Select(e => e.FullName).Order().ShouldBe(["layers/0001.bgra", "layers/0004.bgra", "manifest.json", "masks/0004.mask"]);
        zip.GetEntry("layers/0001.bgra")!.Length.ShouldBe(7 * 5 * 4);
        zip.GetEntry("masks/0004.mask")!.Length.ShouldBe(7 * 5);

        using var manifest = zip.GetEntry("manifest.json")!.Open();
        var json = JsonNode.Parse(manifest)!.AsObject();
        ((string)json["format"]!).ShouldBe("orbit-studio-project");
        ((int)json["formatVersion"]!).ShouldBe(1);
        ((string)json["savedWith"]!).ShouldBe("Test 1.0");
        ((string)FirstLayer(json)["pixels"]!["sha256"]!).Length.ShouldBe(64);
    }

    [Fact]
    public void Inspect_ValidatesWithoutLoadingPixels()
    {
        using var stream = new MemoryStream(Save(Sample()));
        var summary = ProjectStore.Inspect(stream);
        summary.ShouldBe(new ProjectSummary(1, "Probe", 7, 5, 4, 7 * 5 * 4 * 2 + 7 * 5));
    }

    [Fact]
    public void NotAZip_IsNotAProject() =>
        LoadError(Encoding.UTF8.GetBytes("Das ist kein Projekt.")).ShouldBe(ProjectError.NotAProject);

    [Fact]
    public void ZipWithoutManifest_IsNotAProject() =>
        LoadError(Rewrite(Save(Sample()), (name, data) => name == ProjectFormat.ManifestEntry ? null : data))
            .ShouldBe(ProjectError.NotAProject);

    [Fact]
    public void WrongFormatId_IsNotAProject() =>
        LoadError(EditManifest(Save(Sample()), m => m["format"] = "anderes-programm")).ShouldBe(ProjectError.NotAProject);

    [Fact]
    public void NewerVersion_IsRejectedAsUnsupported() =>
        LoadError(EditManifest(Save(Sample()), m => m["formatVersion"] = 2)).ShouldBe(ProjectError.UnsupportedVersion);

    [Theory]
    [InlineData("{ kaputt")]
    [InlineData("[1, 2, 3]")]
    [InlineData("")]
    public void UnreadableManifest_IsCorrupt(string content) =>
        LoadError(Rewrite(Save(Sample()), (name, data) => name == ProjectFormat.ManifestEntry ? Encoding.UTF8.GetBytes(content) : data))
            .ShouldBeOneOf(ProjectError.Corrupt, ProjectError.NotAProject);

    [Fact]
    public void MissingDataBlock_IsCorrupt() =>
        LoadError(Rewrite(Save(Sample()), (name, data) => name == "layers/0001.bgra" ? null : data)).ShouldBe(ProjectError.Corrupt);

    [Fact]
    public void WrongDataLength_IsCorrupt() =>
        LoadError(Rewrite(Save(Sample()), (name, data) => name == "layers/0001.bgra" ? data[..^4] : data)).ShouldBe(ProjectError.Corrupt);

    [Fact]
    public void ManipulatedPixels_FailChecksum()
    {
        var tampered = Rewrite(Save(Sample()), (name, data) =>
        {
            if (name == "layers/0001.bgra")
            {
                data[100] ^= 0xFF; // gleiche Länge, anderer Inhalt
            }

            return data;
        });

        var ex = Should.Throw<ProjectException>(() => Load(tampered));
        ex.Error.ShouldBe(ProjectError.Corrupt);
        ex.Detail.ShouldContain("Prüfsumme");
    }

    [Fact]
    public void TruncatedFile_IsRejected()
    {
        var bytes = Save(Sample());
        LoadError(bytes[..(bytes.Length / 2)]).ShouldBeOneOf(ProjectError.NotAProject, ProjectError.Corrupt);
    }

    [Theory]
    [InlineData(100_000, 10)]
    [InlineData(20_000, 20_000)]
    public void OversizedDimensions_AreTooLarge_BeforeAllocating(int width, int height) =>
        LoadError(EditManifest(Save(Sample()), m =>
        {
            m["document"]!["width"] = width;
            m["document"]!["height"] = height;
        })).ShouldBe(ProjectError.TooLarge);

    [Fact]
    public void DataExceedingBudget_IsTooLarge() =>
        LoadError(Save(Sample()), new ProjectReadOptions(MaxTotalBytes: 100)).ShouldBe(ProjectError.TooLarge);

    [Theory]
    [InlineData("width", 0)]
    [InlineData("resolutionDpi", -5)]
    public void InvalidDocumentValues_AreCorrupt(string property, int value) =>
        LoadError(EditManifest(Save(Sample()), m => m["document"]![property] = value)).ShouldBe(ProjectError.Corrupt);

    [Fact]
    public void ManifestSizeChangedButDataNot_IsCorrupt() =>
        // Größe 8 × 5 passt nicht zu den gespeicherten 7 × 5-Daten
        LoadError(EditManifest(Save(Sample()), m => m["document"]!["width"] = 8)).ShouldBe(ProjectError.Corrupt);

    [Theory]
    [InlineData("opacity", 1.5)]
    [InlineData("opacity", -0.1)]
    public void InvalidOpacity_IsCorrupt(string property, double value) =>
        LoadError(EditManifest(Save(Sample()), m => FirstLayer(m)[property] = value)).ShouldBe(ProjectError.Corrupt);

    [Theory]
    [InlineData("blendMode", "Zauberei")]
    [InlineData("blendMode", "3")]
    [InlineData("type", "text")]
    [InlineData("name", "  ")]
    public void InvalidLayerValues_AreCorrupt(string property, string value) =>
        LoadError(EditManifest(Save(Sample()), m => FirstLayer(m)[property] = value)).ShouldBe(ProjectError.Corrupt);

    [Fact]
    public void DuplicateIds_AreCorrupt() =>
        LoadError(EditManifest(Save(Sample()), m => m["layers"]![1]!["id"] = (string)FirstLayer(m)["id"]!)).ShouldBe(ProjectError.Corrupt);

    [Fact]
    public void UnknownActiveLayer_IsCorrupt() =>
        LoadError(EditManifest(Save(Sample()), m => m["activeLayerId"] = "9999")).ShouldBe(ProjectError.Corrupt);

    [Fact]
    public void SameDataBlockForTwoLayers_IsCorrupt() =>
        LoadError(EditManifest(Save(Sample()), m =>
        {
            var copy = FirstLayer(m).DeepClone().AsObject();
            copy["id"] = "x";
            m["layers"]!.AsArray().Add(copy);
        })).ShouldBe(ProjectError.Corrupt);

    [Fact]
    public void TooDeepNesting_IsCorrupt() =>
        LoadError(EditManifest(Save(Sample()), m =>
        {
            var leaf = FirstLayer(m).DeepClone();
            JsonNode node = leaf;
            for (var i = 0; i <= ProjectFormat.MaxDepth + 1; i++)
            {
                node = new JsonObject { ["type"] = "group", ["id"] = "g" + i, ["name"] = "G", ["children"] = new JsonArray(node) };
            }

            m["layers"] = new JsonArray(node);
            m.Remove("activeLayerId");
        })).ShouldBe(ProjectError.Corrupt);

    [Fact]
    public void EmptyProject_IsCorrupt() =>
        LoadError(EditManifest(Save(Sample()), m =>
        {
            m["layers"] = new JsonArray();
            m.Remove("activeLayerId");
        })).ShouldBe(ProjectError.Corrupt);

    [Fact]
    public void UnknownExtraFields_AreIgnored_ForForwardCompatibleReading()
    {
        var loaded = Load(EditManifest(Save(Sample()), m => m["zukunft"] = "egal"));
        loaded.LayerCount.ShouldBe(4);
    }

    [Fact]
    public void CancelledWrite_Throws()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        Should.Throw<OperationCanceledException>(() => ProjectStore.Write(Sample(), new MemoryStream(), null, cts.Token));
    }

    // --- Migration -----------------------------------------------------------------------------

    /// <summary>Beispiel-Migration für die Tests: Version 1 → 2 benennt „name“ der Ebenen in „title“ um und zurück.</summary>
    private sealed class RenameMigration(int from, Action<JsonObject> apply) : IProjectMigration
    {
        public int FromVersion { get; } = from;

        public void Apply(JsonObject manifest) => apply(manifest);
    }

    [Fact]
    public void Migration_RunsStepByStep_ToCurrentVersion()
    {
        // Simulierte alte Datei (Version 1) für ein Programm, dessen aktuelle Version 3 ist.
        var steps = new List<int>();
        var migrator = new ProjectMigrator(
        [
            new RenameMigration(1, m => { steps.Add(1); m["document"]!["name"] = "migriert"; }),
            new RenameMigration(2, m => steps.Add(2)),
        ], currentVersion: 3);

        var loaded = Load(Save(Sample()), new ProjectReadOptions(migrator));

        steps.ShouldBe([1, 2]);
        loaded.Name.ShouldBe("migriert");
        loaded.LayerCount.ShouldBe(4);
    }

    [Fact]
    public void Migration_MissingStep_IsUnsupported()
    {
        var migrator = new ProjectMigrator([new RenameMigration(2, _ => { })], currentVersion: 3);
        LoadError(Save(Sample()), new ProjectReadOptions(migrator)).ShouldBe(ProjectError.UnsupportedVersion);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void InvalidVersionNumber_IsCorrupt(int version) =>
        LoadError(EditManifest(Save(Sample()), m => m["formatVersion"] = version)).ShouldBe(ProjectError.Corrupt);

    [Fact]
    public void MissingVersion_IsCorrupt() =>
        LoadError(EditManifest(Save(Sample()), m => m.Remove("formatVersion"))).ShouldBe(ProjectError.Corrupt);
}
