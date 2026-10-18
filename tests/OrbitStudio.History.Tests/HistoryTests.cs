using OrbitStudio.Domain.Documents;
using OrbitStudio.Domain.Imaging;
using OrbitStudio.Domain.Layers;
using OrbitStudio.Domain.Selection;
using OrbitStudio.History;

namespace OrbitStudio.History.Tests;

public class HistoryTests
{
    private static ImageDocument Doc() => ImageDocument.Create("T", 6, 4, "HG");

    private static PixelLayer Layer(ImageDocument d) => d.PixelLayers.First();

    /// <summary>Ändert einen Bereich und legt den passenden Eintrag an.</summary>
    private static PixelRegionEntry Paint(ImageDocument d, IntRect r, Bgra32 color, string name = "Test")
    {
        var backup = Layer(d).Pixels.Clone();
        Layer(d).Pixels.Fill(r, color);
        d.CommitPixelChange(r);
        return PixelRegionEntry.FromBackup(name, Layer(d), backup, r)!;
    }

    [Fact]
    public void PixelRegionEntry_StoresOnlyRegion_AndUndoRedoExactly()
    {
        var d = Doc();
        var original = Layer(d).Pixels.Clone();
        var entry = Paint(d, new IntRect(1, 1, 2, 2), Bgra32.Black);
        var painted = Layer(d).Pixels.Clone();

        entry.SizeBytes.ShouldBe(2 * 2 * 2 * 4); // vorher + nachher, 2×2 Pixel
        entry.Undo(d);
        Layer(d).Pixels.ContentEquals(original).ShouldBeTrue();
        entry.Redo(d);
        Layer(d).Pixels.ContentEquals(painted).ShouldBeTrue();
    }

    [Fact]
    public void PixelRegionEntry_WithoutChange_IsNull()
    {
        var d = Doc();
        var backup = Layer(d).Pixels.Clone();
        PixelRegionEntry.FromBackup("x", Layer(d), backup, new IntRect(0, 0, 3, 3)).ShouldBeNull();
        PixelRegionEntry.FromBackup("x", Layer(d), backup, new IntRect(50, 50, 3, 3)).ShouldBeNull();
    }

    [Fact]
    public void History_UndoRedo_AndNewActionDiscardsRedoBranch()
    {
        var d = Doc();
        var h = new DocumentHistory(d);
        var changes = 0;
        h.Changed += (_, _) => changes++;

        h.Push(Paint(d, new IntRect(0, 0, 1, 1), Bgra32.Black, "A"));
        h.Push(Paint(d, new IntRect(1, 0, 1, 1), Bgra32.Black, "B"));
        h.UndoNameKey.ShouldBe("B");

        h.Undo().ShouldBeTrue();
        h.CanRedo.ShouldBeTrue();
        h.RedoNameKey.ShouldBe("B");
        Layer(d).Pixels.GetPixel(1, 0).ShouldBe(Bgra32.White);

        h.Push(Paint(d, new IntRect(2, 0, 1, 1), Bgra32.Black, "C"));
        h.CanRedo.ShouldBeFalse(); // Zweig verworfen
        h.UndoCount.ShouldBe(2);
        changes.ShouldBe(4);

        h.Undo();
        h.Undo();
        h.Undo().ShouldBeFalse();
        Layer(d).Pixels.GetPixel(0, 0).ShouldBe(Bgra32.White);
        h.Redo().ShouldBeTrue();
        Layer(d).Pixels.GetPixel(0, 0).ShouldBe(Bgra32.Black);
    }

    [Fact]
    public void History_LimitsEntryCount()
    {
        var d = Doc();
        var h = new DocumentHistory(d, maxEntries: 3);
        for (var i = 0; i < 5; i++)
        {
            h.Push(Paint(d, new IntRect(i, 0, 1, 1), Bgra32.Black, "E" + i));
        }

        h.UndoCount.ShouldBe(3);
        h.UndoEntries.First().NameKey.ShouldBe("E2");
    }

    [Fact]
    public void History_LimitsMemory_ButKeepsNewestEntry()
    {
        var d = Doc();
        var h = new DocumentHistory(d, maxBytes: 40);
        h.Push(Paint(d, new IntRect(0, 0, 2, 1), Bgra32.Black, "A")); // 16 Byte
        h.Push(Paint(d, new IntRect(0, 1, 2, 1), Bgra32.Black, "B")); // 16 Byte
        h.Push(Paint(d, new IntRect(0, 2, 6, 1), Bgra32.Black, "C")); // 48 Byte > Grenze

        h.UndoCount.ShouldBe(1);
        h.UndoNameKey.ShouldBe("C");
    }

    [Fact]
    public void SelectionEntry_RestoresBeforeAndAfter()
    {
        var d = Doc();
        var after = SelectionMask.CreateFull(6, 4);
        d.SetSelection(after);
        var entry = new SelectionEntry("Auswahl", null, after);

        entry.Undo(d);
        d.Selection.ShouldBeNull();
        entry.Redo(d);
        d.Selection.ShouldBeSameAs(after);
    }

    [Fact]
    public void LayerTreeEntry_UndoesStructureAndProperties()
    {
        var d = Doc();
        var background = Layer(d);
        var added = new PixelLayer("Neu", PixelBuffer.Create(6, 4));

        var entry = LayerTreeEntry.Record("Neu", d, () =>
        {
            d.Root.Add(added);
            background.Opacity = 0.5;
            background.Name = "Umbenannt";
            d.ActiveLayer = added;
            d.CommitLayerChange();
        });

        entry.Undo(d);
        d.Root.Children.ShouldBe([background]);
        added.Parent.ShouldBeNull();
        background.Opacity.ShouldBe(1.0);
        background.Name.ShouldBe("HG");
        d.ActiveLayer.ShouldBeSameAs(background);

        entry.Redo(d);
        d.Root.Children.ShouldBe([background, added]);
        background.Opacity.ShouldBe(0.5);
        d.ActiveLayer.ShouldBeSameAs(added);
    }

    [Fact]
    public void LayerTreeEntry_RestoresNestedGroupsConsistently()
    {
        var d = Doc();
        var background = Layer(d);
        var group = new LayerGroup("G");

        var entry = LayerTreeEntry.Record("Gruppe", d, () =>
        {
            d.Root.Remove(background);
            group.Add(background);
            d.Root.Add(group);
            d.CommitLayerChange();
        });

        entry.Undo(d);
        background.Parent.ShouldBeSameAs(d.Root);
        d.Root.Children.ShouldBe([background]);

        entry.Redo(d);
        background.Parent.ShouldBeSameAs(group);
        group.Parent.ShouldBeSameAs(d.Root);
        group.Children.ShouldBe([background]);
    }

    [Fact]
    public void LayerTreeEntry_RestoresPixelsAndMasksOfChangedLayers()
    {
        var d = Doc();
        var layer = Layer(d);
        layer.Mask = LayerMask.CreateRevealAll(6, 4);
        var mask = layer.Mask;

        var entry = LayerTreeEntry.Record("Zusammen", d, () =>
        {
            layer.Pixels.Fill(Bgra32.Black);
            mask.Invert();
            d.CommitLayerChange();
        }, [layer], [mask]);

        entry.Undo(d);
        layer.Pixels.GetPixel(0, 0).ShouldBe(Bgra32.White);
        mask.Get(0, 0).ShouldBe((byte)255);
        entry.Redo(d);
        layer.Pixels.GetPixel(0, 0).ShouldBe(Bgra32.Black);
        mask.Get(0, 0).ShouldBe((byte)0);
        entry.SizeBytes.ShouldBe(6 * 4 * 4 * 2 + 6 * 4 * 2);
    }

    [Fact]
    public void ContentEntry_RestoresSizeAndBuffers()
    {
        var d = Doc();
        var layer = Layer(d);
        var before = d.CaptureContent();
        var bigger = PixelBuffer.Create(10, 8);
        var after = new DocumentContent(10, 8,
            new Dictionary<PixelLayer, (PixelBuffer, LayerMask?)> { [layer] = (bigger, null) }, null);
        var sizeEvents = 0;
        d.SizeChanged += (_, _) => sizeEvents++;

        d.ReplaceContent(after);
        var entry = new ContentEntry("Größe", before, after);
        d.Width.ShouldBe(10);

        entry.Undo(d);
        d.Width.ShouldBe(6);
        layer.Pixels.Width.ShouldBe(6);
        entry.Redo(d);
        layer.Pixels.ShouldBeSameAs(bigger);
        sizeEvents.ShouldBe(3);
    }

    [Fact]
    public void ReplaceContent_RejectsMissingLayersAndWrongSizes()
    {
        var d = Doc();
        var layer = Layer(d);
        Should.Throw<ArgumentException>(() => d.ReplaceContent(new DocumentContent(10, 8,
            new Dictionary<PixelLayer, (PixelBuffer, LayerMask?)>(), null)));
        Should.Throw<ArgumentException>(() => d.ReplaceContent(new DocumentContent(10, 8,
            new Dictionary<PixelLayer, (PixelBuffer, LayerMask?)> { [layer] = (PixelBuffer.Create(9, 8), null) }, null)));
    }

    [Fact]
    public void CompositeEntry_UndoesInReverseOrder()
    {
        var d = Doc();
        var log = new List<string>();
        var entry = new CompositeEntry("X",
        [
            new ActionEntry("a", _ => log.Add("undo a"), _ => log.Add("redo a"), 3),
            new ActionEntry("b", _ => log.Add("undo b"), _ => log.Add("redo b"), 4),
        ]);

        entry.Undo(d);
        entry.Redo(d);

        log.ShouldBe(["undo b", "undo a", "redo a", "redo b"]);
        entry.SizeBytes.ShouldBe(7);
    }
}
