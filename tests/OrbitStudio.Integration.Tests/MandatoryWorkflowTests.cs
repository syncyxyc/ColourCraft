using Microsoft.Extensions.Logging.Abstractions;
using OrbitStudio.Application.Documents;
using OrbitStudio.Application.Files;
using OrbitStudio.Domain.Documents;
using OrbitStudio.Domain.Imaging;
using OrbitStudio.Domain.Layers;
using OrbitStudio.Effects;
using OrbitStudio.History;
using OrbitStudio.Imaging.Codecs;
using OrbitStudio.Layers;
using OrbitStudio.Rendering;
using OrbitStudio.Storage;
using OrbitStudio.Tools;
using OrbitStudio.Tools.Drawing;
using OrbitStudio.Tools.Selection;

namespace OrbitStudio.Integration.Tests;

/// <summary>
/// Pflicht-Integrationsworkflow (Spezifikation Abschnitt 13), Schritt für Schritt über die echten Module
/// (Arbeitsbereich, Ebenen, Werkzeuge, Effekte, Historie, Projektformat, Codecs, Dateien).
/// </summary>
public sealed class MandatoryWorkflowTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "orbit-pflicht-" + Guid.NewGuid().ToString("N"));
    private readonly ImageFileService _images = new(ImageCodecRegistry.CreateDefault(), NullLogger<ImageFileService>.Instance);
    private readonly ProjectFileService _projects = new(NullLogger<ProjectFileService>.Instance);
    private readonly DocumentWorkspace _workspace = new(NullLogger<DocumentWorkspace>.Instance);

    public MandatoryWorkflowTests() => Directory.CreateDirectory(_dir);

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private static ToolPointerEvent At(double x, double y) =>
        new(new PointD(x, y), new PointD(x, y), PointerButton.Primary, ToolModifiers.None);

    private static void Drag(ITool tool, ToolContext context, double x0, double y0, double x1, double y1)
    {
        tool.PointerDown(context, At(x0, y0));
        tool.PointerMove(context, At((x0 + x1) / 2, (y0 + y1) / 2));
        tool.PointerUp(context, At(x1, y1));
    }

    [Fact]
    public async Task Section13_CompleteWorkflow()
    {
        // 1. Dokument erstellen
        var document = ImageDocument.Create("Pflicht", 80, 60, "Hintergrund");
        _workspace.Add(document);
        var history = new DocumentHistory(document);
        var context = new ToolContext(document, history, new ToolColors { Primary = new ColorRgba(40, 120, 220) }, null);

        // 2. Ebene hinzufügen
        history.Push(LayerTreeEntry.Record("Neue Ebene", document, () => LayerOperations.AddLayer(document, "Ebene 1")));
        var layer = document.ActivePixelLayer!;
        document.LayerCount.ShouldBe(2);

        // 3. Auf der Ebene zeichnen
        Drag(new RectangleTool(filled: true), context, 10, 10, 50, 40);
        layer.Pixels.GetPixel(20, 20).ShouldBe(Bgra32.FromStraight(40, 120, 220, 255));

        // 4. Auswahl erstellen
        Drag(new RectangleSelectTool(), context, 0, 0, 30, 60);
        document.Selection!.Bounds().ShouldBe(new IntRect(0, 0, 30, 60));

        // 5. Effekt anwenden (Invertieren, nur in der Auswahl)
        var invert = EffectRegistry.CreateDefault().Get("invert");
        EffectEngine.Apply(document, history, invert, EffectSettings.Defaults(invert)).ShouldNotBeNull();
        var inverted = Bgra32.FromStraight(215, 135, 35, 255);
        layer.Pixels.GetPixel(20, 20).ShouldBe(inverted);
        layer.Pixels.GetPixel(40, 20).ShouldBe(Bgra32.FromStraight(40, 120, 220, 255));
        var afterEffect = layer.Pixels.Clone();

        // 6. Effekt rückgängig machen
        history.Undo();
        layer.Pixels.GetPixel(20, 20).ShouldBe(Bgra32.FromStraight(40, 120, 220, 255));

        // 7. Effekt wiederholen
        history.Redo();
        layer.Pixels.ContentEquals(afterEffect).ShouldBeTrue();

        // 8. Ebene umbenennen
        history.Push(LayerTreeEntry.Record("Umbenennen", document, () => LayerOperations.Rename(document, layer, "Blaues Rechteck")));
        layer.Name.ShouldBe("Blaues Rechteck");

        // 9. Projekt als .orbit speichern
        var projectPath = Path.Combine(_dir, "pflicht.orbit");
        var saved = await _projects.SaveAsync(document, projectPath);
        saved.Success.ShouldBeTrue();
        document.MarkSaved(projectPath);
        history.MarkSavePoint();
        document.IsDirty.ShouldBeFalse();
        var expectedComposite = LayerCompositor.Composite(document);
        var expectedBackground = document.PixelLayers.First().Pixels.Clone();

        // 10. Projekt schließen
        _workspace.Close(document);
        _workspace.Documents.ShouldBeEmpty();

        // 11. Projekt erneut öffnen
        var opened = await _projects.OpenAsync(projectPath);
        opened.Success.ShouldBeTrue();
        var reopened = opened.Document!;
        _workspace.Add(reopened);

        // 12. Pixel, Ebenen und Dokumentzustand prüfen
        reopened.ShouldNotBeSameAs(document);
        (reopened.Width, reopened.Height, reopened.IsDirty, reopened.FilePath).ShouldBe((80, 60, false, projectPath));
        reopened.Name.ShouldBe("pflicht.orbit");
        reopened.Root.Children.Select(c => c.Name).ShouldBe(["Hintergrund", "Blaues Rechteck"]);
        var reopenedLayer = (PixelLayer)reopened.Root.Children[1];
        reopened.ActiveLayer.ShouldBe(reopenedLayer);
        reopenedLayer.Pixels.ContentEquals(afterEffect).ShouldBeTrue();
        ((PixelLayer)reopened.Root.Children[0]).Pixels.ContentEquals(expectedBackground).ShouldBeTrue();
        LayerCompositor.Composite(reopened).ContentEquals(expectedComposite).ShouldBeTrue();
        reopened.Selection.ShouldBeNull(); // die Auswahl ist kein Teil des Projekts (bekannte Einschränkung)

        // 13. PNG exportieren
        var pngPath = Path.Combine(_dir, "pflicht.png");
        (await _images.ExportAsync(LayerCompositor.Composite(reopened), pngPath, new EncodeOptions())).Success.ShouldBeTrue();

        // 14. Exportiertes PNG wieder einlesen und Pixel verifizieren
        var png = await _images.OpenAsync(pngPath, "PNG");
        png.Success.ShouldBeTrue();
        png.Document!.PixelLayers.Single().Pixels.ContentEquals(expectedComposite).ShouldBeTrue();
        png.Document.PixelLayers.Single().Pixels.GetPixel(20, 20).ShouldBe(inverted);
    }
}

/// <summary>Sichere Dateiersetzung und Fehlerfälle des Projekt-Dateidienstes mit echten Dateien.</summary>
public sealed class ProjectFileServiceTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "orbit-projekt-" + Guid.NewGuid().ToString("N"));
    private readonly ProjectFileService _service = new(NullLogger<ProjectFileService>.Instance);

    public ProjectFileServiceTests() => Directory.CreateDirectory(_dir);

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private static ImageDocument Doc(byte gray)
    {
        var document = ImageDocument.Create("D", 64, 48, "HG");
        document.PixelLayers.First().Pixels.Fill(Bgra32.FromStraight(gray, gray, gray, 255));
        return document;
    }

    [Fact]
    public async Task Save_ReplacesExistingFile_AndLeavesNoTemporaryFiles()
    {
        var path = Path.Combine(_dir, "a.orbit");
        (await _service.SaveAsync(Doc(10), path)).Success.ShouldBeTrue();
        (await _service.SaveAsync(Doc(200), path)).Success.ShouldBeTrue();

        var reopened = await _service.OpenAsync(path);
        reopened.Document!.PixelLayers.First().Pixels.GetPixel(0, 0).R.ShouldBe((byte)200);
        Directory.GetFiles(_dir).Select(Path.GetFileName).ShouldBe(["a.orbit"]);
    }

    [Fact]
    public async Task CancelledSave_KeepsExistingFileUnchanged()
    {
        var path = Path.Combine(_dir, "b.orbit");
        (await _service.SaveAsync(Doc(10), path)).Success.ShouldBeTrue();
        var before = await File.ReadAllBytesAsync(path);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        (await _service.SaveAsync(Doc(99), path, cts.Token)).Error.ShouldBe(ProjectError.Cancelled);

        (await File.ReadAllBytesAsync(path)).ShouldBe(before);
        Directory.GetFiles(_dir).Length.ShouldBe(1);
    }

    [Fact]
    public async Task SaveIntoMissingFolder_ReportsError_WithoutException()
    {
        var result = await _service.SaveAsync(Doc(1), Path.Combine(_dir, "fehlt", "c.orbit"));
        result.Error.ShouldBe(ProjectError.NotFound);
    }

    [Fact]
    public async Task SaveOverReadOnlyFile_ReportsAccessDenied_AndKeepsFile()
    {
        var path = Path.Combine(_dir, "d.orbit");
        (await _service.SaveAsync(Doc(1), path)).Success.ShouldBeTrue();
        var before = await File.ReadAllBytesAsync(path);
        File.SetAttributes(path, FileAttributes.ReadOnly);
        try
        {
            (await _service.SaveAsync(Doc(2), path)).Error.ShouldBe(ProjectError.AccessDenied);
            (await File.ReadAllBytesAsync(path)).ShouldBe(before);
        }
        finally
        {
            File.SetAttributes(path, FileAttributes.Normal);
        }
    }

    [Fact]
    public async Task OpenErrors_AreClassified()
    {
        (await _service.OpenAsync(Path.Combine(_dir, "gibtsnicht.orbit"))).Error.ShouldBe(ProjectError.NotFound);
        var fake = Path.Combine(_dir, "bild.orbit");
        await File.WriteAllTextAsync(fake, "kein Projekt");
        (await _service.OpenAsync(fake)).Error.ShouldBe(ProjectError.NotAProject);
    }

    [Fact]
    public void ProjectPathDetection_IsCaseInsensitive()
    {
        ProjectFileService.IsProjectPath(@"C:\x\Bild.ORBIT").ShouldBeTrue();
        ProjectFileService.IsProjectPath(@"C:\x\Bild.png").ShouldBeFalse();
    }
}
