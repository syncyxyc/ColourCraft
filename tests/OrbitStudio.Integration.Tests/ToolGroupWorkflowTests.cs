using Microsoft.Extensions.Logging.Abstractions;
using OrbitStudio.Application.Files;
using OrbitStudio.Domain.Documents;
using OrbitStudio.Domain.Imaging;
using OrbitStudio.Domain.Layers;
using OrbitStudio.History;
using OrbitStudio.Imaging.Codecs;
using OrbitStudio.Layers;
using OrbitStudio.Rendering;
using OrbitStudio.Tools;
using OrbitStudio.Tools.Drawing;
using OrbitStudio.Tools.Editing;
using OrbitStudio.Tools.Fill;
using OrbitStudio.Tools.Selection;
using OrbitStudio.Tools.Transform;

namespace OrbitStudio.Integration.Tests;

/// <summary>
/// Je Werkzeuggruppe ein durchgehender Ablauf über mehrere Module (Dokument, Ebenen, Werkzeuge, Historie, Dateien).
/// </summary>
public sealed class ToolGroupWorkflowTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "orbit-workflow-" + Guid.NewGuid().ToString("N"));
    private readonly ImageFileService _files = new(ImageCodecRegistry.CreateDefault(), NullLogger<ImageFileService>.Instance);
    private readonly ImageDocument _doc = ImageDocument.Create("Ablauf", 64, 48, "Hintergrund");
    private readonly DocumentHistory _history;
    private readonly ToolColors _colors = new();
    private readonly ViewTransform _view = new(64, 48);
    private readonly ToolContext _context;

    public ToolGroupWorkflowTests()
    {
        Directory.CreateDirectory(_dir);
        _history = new DocumentHistory(_doc);
        _view.SetViewport(640, 480, 1);
        _context = new ToolContext(_doc, _history, _colors, _view);
    }

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private static ToolPointerEvent At(double x, double y, PointerButton b = PointerButton.Primary,
        ToolModifiers m = ToolModifiers.None, int clicks = 1) => new(new PointD(x, y), new PointD(x, y), b, m, clicks);

    private void Drag(ITool tool, double x0, double y0, double x1, double y1, ToolModifiers m = ToolModifiers.None)
    {
        tool.PointerDown(_context, At(x0, y0, m: m));
        for (var i = 1; i <= 6; i++)
        {
            tool.PointerMove(_context, At(x0 + (x1 - x0) * i / 6, y0 + (y1 - y0) * i / 6, m: m));
        }

        tool.PointerUp(_context, At(x1, y1, m: m));
    }

    private void Click(ITool tool, double x, double y, PointerButton b = PointerButton.Primary)
    {
        tool.PointerDown(_context, At(x, y, b));
        tool.PointerUp(_context, At(x, y, b));
    }

    private void RecordLayerOp(string name, Action op) => _history.Push(LayerTreeEntry.Record(name, _doc, op));

    [Fact]
    public async Task Drawing_OnNewLayer_ExportReopen_AndUndoEverything()
    {
        var originalComposite = LayerCompositor.Composite(_doc);
        RecordLayerOp("Neue Ebene", () => LayerOperations.AddLayer(_doc, "Zeichnung"));
        _colors.Primary = new ColorRgba(0, 0, 255);
        var rectangle = new RectangleTool(filled: true);
        Drag(rectangle, 10, 10, 30, 20);
        var line = new LineTool();
        line.LineWidth.Value = 3;
        Drag(line, 5, 40, 60, 40);

        var composite = LayerCompositor.Composite(_doc);
        composite.GetPixel(15, 15).ShouldBe(new Bgra32(255, 0, 0, 255));
        composite.GetPixel(40, 15).ShouldBe(Bgra32.White);

        var path = Path.Combine(_dir, "zeichnung.png");
        (await _files.ExportAsync(composite, path, new EncodeOptions())).Success.ShouldBeTrue();
        var reopened = await _files.OpenAsync(path, "HG");
        reopened.Document!.PixelLayers.Single().Pixels.ContentEquals(composite).ShouldBeTrue();

        _history.UndoCount.ShouldBe(3);
        while (_history.Undo())
        {
        }

        _doc.LayerCount.ShouldBe(1);
        LayerCompositor.Composite(_doc).ContentEquals(originalComposite).ShouldBeTrue();
        while (_history.Redo())
        {
        }

        LayerCompositor.Composite(_doc).ContentEquals(composite).ShouldBeTrue();
    }

    [Fact]
    public void Selection_MagicWandOnImportedLayer_DeleteAndUndo()
    {
        var image = PixelBuffer.Create(64, 48);
        image.Fill(new IntRect(0, 0, 32, 48), Bgra32.FromStraight(0, 200, 0, 255));
        RecordLayerOp("Import", () => LayerOperations.ImportAsLayer(_doc, image, "Bild"));
        var layer = _doc.ActivePixelLayer!;

        Click(new MagicWandTool(contiguous: true), 5, 5);
        _doc.Selection!.Bounds().ShouldBe(new IntRect(0, 0, 32, 48));

        SelectionContent.Delete(_context).ShouldBeTrue();
        layer.Pixels.GetPixel(5, 5).ShouldBe(Bgra32.Transparent);
        LayerCompositor.Composite(_doc).GetPixel(5, 5).ShouldBe(Bgra32.White); // Hintergrund scheint durch

        SelectionEditing.Invert(_doc, _history);
        _doc.Selection!.Get(5, 5).ShouldBe((byte)0);
        _doc.Selection.Get(40, 5).ShouldBe((byte)255);

        _history.Undo(); // Umkehren
        _history.Undo(); // Löschen
        layer.Pixels.GetPixel(5, 5).ShouldBe(Bgra32.FromStraight(0, 200, 0, 255));
        _history.Undo(); // Zauberstab
        _doc.Selection.ShouldBeNull();
    }

    [Fact]
    public void FillAndColor_PipetteFromCompositeThenBucketOnOtherLayer()
    {
        var background = _doc.ActivePixelLayer!;
        background.Pixels.Fill(new IntRect(0, 0, 10, 10), Bgra32.FromStraight(10, 20, 30, 255));
        RecordLayerOp("Neue Ebene", () => LayerOperations.AddLayer(_doc, "Farbe"));
        var top = _doc.ActivePixelLayer!;

        var pipette = new PipetteTool(); // Quelle: Bild (Komposit)
        Click(pipette, 2, 2);
        _colors.Primary.ShouldBe(new ColorRgba(10, 20, 30));

        var bucket = new BucketTool();
        bucket.SampleFrom.Index = 1; // Bild als Quelle: Fläche gleicher Farbe im Komposit
        Click(bucket, 30, 30);

        top.Pixels.GetPixel(30, 30).ShouldBe(Bgra32.FromStraight(10, 20, 30, 255));
        top.Pixels.GetPixel(2, 2).ShouldBe(Bgra32.Transparent); // andere Farbe im Bild → nicht gefüllt
        background.Pixels.GetPixel(30, 30).ShouldBe(Bgra32.White); // Hintergrund unverändert

        var gradient = new GradientTool();
        Drag(gradient, 0, 0, 64, 0);
        top.Pixels.GetPixel(0, 30).ShouldNotBe(top.Pixels.GetPixel(63, 30));
        _history.Undo();
        top.Pixels.GetPixel(30, 30).ShouldBe(Bgra32.FromStraight(10, 20, 30, 255));
    }

    [Fact]
    public void Transform_MoveSelectionCropResizeRotate_AndUndoChain()
    {
        var layer = _doc.ActivePixelLayer!;
        layer.Pixels.Fill(new IntRect(4, 4, 8, 8), Bgra32.Black);
        Drag(new RectangleSelectTool(), 4, 4, 12, 12);
        Drag(new MoveTool(), 6, 6, 26, 16);
        layer.Pixels.GetPixel(24, 14).ShouldBe(Bgra32.Black);
        layer.Pixels.GetPixel(4, 4).ShouldBe(Bgra32.Transparent);

        DocumentTransforms.Crop(_doc, _history, _doc.Selection!.Bounds());
        _doc.Width.ShouldBe(8);
        layer.Pixels.GetPixel(0, 0).ShouldBe(Bgra32.Black);

        DocumentTransforms.ResizeImage(_doc, _history, 16, 16);
        DocumentTransforms.Rotate(_doc, _history, 90);
        _doc.Width.ShouldBe(16);
        layer.Pixels.GetPixel(8, 8).ShouldBe(Bgra32.Black);

        for (var i = 0; i < 3; i++)
        {
            _history.Undo();
        }

        _doc.Width.ShouldBe(64);
        layer.Pixels.GetPixel(24, 14).ShouldBe(Bgra32.Black);
        _history.Undo(); // Verschieben
        layer.Pixels.GetPixel(4, 4).ShouldBe(Bgra32.Black);
        _history.TotalBytes.ShouldBeGreaterThan(0);
    }

    /// <summary>
    /// Strichmuster (Spezifikation 1.1, Abnahmeablauf ohne Oberfläche): neue Ebene, Linie aufziehen, beide Kontrollpunkte
    /// verschieben, Parameter ändern, Vorschau prüfen, anwenden, Rückgängig/Wiederholen, PNG exportieren und wieder einlesen.
    /// </summary>
    [Fact]
    public async Task DashPattern_MoveBothControlPoints_Apply_UndoRedo_ExportReopen()
    {
        _view.SetZoom(2);
        RecordLayerOp("Neue Ebene", () => LayerOperations.AddLayer(_doc, "Muster"));
        var layer = _doc.ActivePixelLayer!;
        var tool = new DashPatternTool();

        ToolPointerEvent Ev(double x, double y)
        {
            var p = new PointD(x, y);
            return new ToolPointerEvent(p, _view.DocumentToViewport(p), PointerButton.Primary, ToolModifiers.None);
        }

        void DragPoint(double x0, double y0, double x1, double y1)
        {
            tool.PointerDown(_context, Ev(x0, y0));
            tool.PointerMove(_context, Ev((x0 + x1) / 2, (y0 + y1) / 2));
            tool.PointerMove(_context, Ev(x1, y1));
            tool.PointerUp(_context, Ev(x1, y1));
        }

        DragPoint(10, 10, 40, 10);         // aufziehen
        DragPoint(10, 10, 2, 20);          // Startpunkt verschieben
        DragPoint(40, 10, 62, 20);         // Endpunkt verschieben
        tool.StartPoint.ShouldBe(new PointD(2, 20));
        tool.EndPoint.ShouldBe(new PointD(62, 20));
        tool.Thickness.Value = 6;
        tool.DashLength.Value = 8;
        tool.GapLength.Value = 4;
        tool.Color.Value = new ColorRgba(0, 128, 255);
        tool.Overlay.OfType<OverlayRaster>().ShouldHaveSingleItem();
        layer.Pixels.GetPixel(5, 20).ShouldBe(Bgra32.Transparent); // Vorschau verändert die Ebene nicht

        tool.ApplyAction.Execute(_context);

        // Striche ab x = 2 mit Periode 12: [2, 10), [14, 22), …; Zeilen 17–22.
        var blue = Bgra32.FromStraight(0, 128, 255, 255);
        layer.Pixels.GetPixel(5, 18).ShouldBe(blue);
        layer.Pixels.GetPixel(12, 18).ShouldBe(Bgra32.Transparent);
        layer.Pixels.GetPixel(15, 22).ShouldBe(blue);
        layer.Pixels.GetPixel(15, 23).ShouldBe(Bgra32.Transparent);
        var composite = LayerCompositor.Composite(_doc);

        _history.UndoNameKey.ShouldBe("Tool.dashPattern");
        _history.Undo();
        layer.Pixels.GetPixel(5, 18).ShouldBe(Bgra32.Transparent);
        _history.Redo();
        LayerCompositor.Composite(_doc).ContentEquals(composite).ShouldBeTrue();

        var path = Path.Combine(_dir, "muster.png");
        (await _files.ExportAsync(composite, path, new EncodeOptions())).Success.ShouldBeTrue();
        var reopened = await _files.OpenAsync(path, "HG");
        reopened.Document!.PixelLayers.Single().Pixels.ContentEquals(composite).ShouldBeTrue();
    }

    /// <summary>
    /// Effekt-Teil des Pflicht-Workflows (Spezifikation 13, Schritte 1–7, 13–14): Dokument, Ebene, zeichnen, Auswahl,
    /// Effekt anwenden, rückgängig, wiederholen, PNG exportieren und Pixel nach dem Einlesen prüfen.
    /// </summary>
    [Fact]
    public async Task Effect_OnSelection_UndoRedo_ExportReopen()
    {
        RecordLayerOp("Neue Ebene", () => LayerOperations.AddLayer(_doc, "Effekt"));
        var layer = _doc.ActivePixelLayer!;
        _colors.Primary = new ColorRgba(200, 40, 40);
        Drag(new RectangleTool(filled: true), 4, 4, 40, 30);
        Drag(new RectangleSelectTool(), 0, 0, 20, 48);
        var drawn = layer.Pixels.Clone();

        var effects = Effects.EffectRegistry.CreateDefault();
        var invert = effects.Get("invert");
        Effects.EffectEngine.Apply(_doc, _history, invert, Effects.EffectSettings.Defaults(invert)).ShouldNotBeNull();

        layer.Pixels.GetPixel(10, 10).ShouldBe(Bgra32.FromStraight(55, 215, 215, 255));   // in der Auswahl invertiert
        layer.Pixels.GetPixel(30, 10).ShouldBe(Bgra32.FromStraight(200, 40, 40, 255));    // außerhalb unverändert
        layer.Pixels.GetPixel(10, 40).ShouldBe(Bgra32.Transparent);                       // transparent bleibt transparent
        var applied = layer.Pixels.Clone();
        _history.UndoNameKey.ShouldBe("Effect.invert");
        _history.Undo();
        layer.Pixels.ContentEquals(drawn).ShouldBeTrue();
        _history.Redo();
        layer.Pixels.ContentEquals(applied).ShouldBeTrue();

        var composite = LayerCompositor.Composite(_doc);
        var path = Path.Combine(_dir, "effekt.png");
        (await _files.ExportAsync(composite, path, new EncodeOptions())).Success.ShouldBeTrue();
        var reopened = await _files.OpenAsync(path, "HG");
        reopened.Document!.PixelLayers.Single().Pixels.ContentEquals(composite).ShouldBeTrue();
        composite.GetPixel(10, 10).ShouldBe(Bgra32.FromStraight(55, 215, 215, 255));
    }

    [Fact]
    public void View_ZoomAndHand_DoNotChangePixelsOrHistory()
    {
        var before = LayerCompositor.Composite(_doc);
        _view.ActualSize();
        Click(new ZoomTool(), 32, 24);
        Click(new ZoomTool(), 32, 24);
        _view.Zoom.ShouldBe(2.0);
        var hand = new HandTool();
        Drag(hand, 100, 100, 50, 60);

        LayerCompositor.Composite(_doc).ContentEquals(before).ShouldBeTrue();
        _history.CanUndo.ShouldBeFalse();
    }
}
