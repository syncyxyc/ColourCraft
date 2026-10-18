using OrbitStudio.Domain.Documents;
using OrbitStudio.Domain.Imaging;
using OrbitStudio.Domain.Layers;
using OrbitStudio.Layers;
using OrbitStudio.Rendering;

namespace OrbitStudio.Layers.Tests;

public class LayerOperationsTests
{
    private static ImageDocument Doc(DocumentBackground background = DocumentBackground.White) =>
        ImageDocument.Create("Test", 8, 6, "Hintergrund", background: background);

    private static PixelLayer Background(ImageDocument doc) => (PixelLayer)doc.Root.Children[0];

    private static PixelLayer AddGray(ImageDocument doc, byte value = 128, string name = "Grau")
    {
        var layer = LayerOperations.AddLayer(doc, name);
        layer.Pixels.Fill(new Bgra32(value, value, value, 255));
        return layer;
    }

    [Fact]
    public void AddLayer_InsertsAboveActive_Transparent_Active_AndMarksDirty()
    {
        var doc = Doc();
        var changes = 0;
        doc.LayersChanged += (_, _) => changes++;

        var layer = LayerOperations.AddLayer(doc, "Ebene 2");

        doc.Root.Children.ShouldBe([Background(doc), layer]);
        doc.ActiveLayer.ShouldBeSameAs(layer);
        layer.Pixels.Width.ShouldBe(8);
        layer.Pixels.GetPixel(0, 0).ShouldBe(Bgra32.Transparent);
        changes.ShouldBe(1);
        doc.IsDirty.ShouldBeTrue();
    }

    [Fact]
    public void AddLayer_AboveActiveInsideGroup()
    {
        var doc = Doc();
        var group = LayerOperations.CreateGroup(doc, Background(doc), "Gruppe");
        doc.ActiveLayer = group.Children[0];

        var layer = LayerOperations.AddLayer(doc, "Neu");

        layer.Parent.ShouldBeSameAs(group);
        group.Children[1].ShouldBeSameAs(layer);
    }

    [Fact]
    public void NextName_SkipsExistingNames()
    {
        var doc = Doc();
        LayerOperations.AddLayer(doc, "Ebene 2");

        LayerOperations.NextName(doc, "Ebene {0}").ShouldBe("Ebene 3");
    }

    [Fact]
    public void Duplicate_PlacesCopyAboveAndActivates()
    {
        var doc = Doc();
        var gray = AddGray(doc);
        gray.Opacity = 0.3;

        var copy = (PixelLayer)LayerOperations.Duplicate(doc, gray, "Grau Kopie");

        doc.Root.Children.ShouldBe([Background(doc), gray, copy]);
        copy.Opacity.ShouldBe(0.3);
        copy.Pixels.ContentEquals(gray.Pixels).ShouldBeTrue();
        copy.Pixels.ShouldNotBeSameAs(gray.Pixels);
        doc.ActiveLayer.ShouldBeSameAs(copy);
    }

    [Fact]
    public void Delete_SelectsLayerBelow_AndKeepsLastPixelLayer()
    {
        var doc = Doc();
        var gray = AddGray(doc);

        LayerOperations.Delete(doc, gray);

        doc.ActiveLayer.ShouldBeSameAs(Background(doc));
        LayerOperations.CanDelete(doc, Background(doc)).ShouldBeFalse();
        Should.Throw<LayerOperationException>(() => LayerOperations.Delete(doc, Background(doc))).Error
            .ShouldBe(LayerOperationError.LastPixelLayer);
    }

    [Fact]
    public void Delete_GroupContainingAllPixelLayers_IsRejected()
    {
        var doc = Doc();
        var group = LayerOperations.CreateGroup(doc, Background(doc), "G");

        Should.Throw<LayerOperationException>(() => LayerOperations.Delete(doc, group)).Error
            .ShouldBe(LayerOperationError.LastPixelLayer);
    }

    [Fact]
    public void Delete_LockedLayer_IsRejected()
    {
        var doc = Doc();
        var gray = AddGray(doc);
        LayerOperations.SetLocked(doc, gray, true);

        LayerOperations.CanDelete(doc, gray).ShouldBeFalse();
        Should.Throw<LayerOperationException>(() => LayerOperations.Delete(doc, gray)).Error
            .ShouldBe(LayerOperationError.Locked);
    }

    [Fact]
    public void MoveUpAndDown_WithinParent_WithBounds()
    {
        var doc = Doc();
        var a = AddGray(doc, name: "A");
        var b = AddGray(doc, name: "B");

        LayerOperations.CanMoveUp(b).ShouldBeFalse();
        Should.Throw<LayerOperationException>(() => LayerOperations.MoveUp(doc, b)).Error.ShouldBe(LayerOperationError.AtTop);

        LayerOperations.MoveDown(doc, b);
        doc.Root.Children.ShouldBe([Background(doc), b, a]);
        LayerOperations.MoveDown(doc, b);
        doc.Root.Children[0].ShouldBeSameAs(b);
        Should.Throw<LayerOperationException>(() => LayerOperations.MoveDown(doc, b)).Error
            .ShouldBe(LayerOperationError.AtBottom);
    }

    [Fact]
    public void Properties_ChangeOnlyWhenDifferent()
    {
        var doc = Doc();
        var gray = AddGray(doc);
        var changes = 0;
        doc.LayersChanged += (_, _) => changes++;

        LayerOperations.SetOpacity(doc, gray, 1.0); // unverändert
        LayerOperations.SetBlendMode(doc, gray, BlendMode.Normal); // unverändert
        LayerOperations.SetVisibility(doc, gray, true); // unverändert
        changes.ShouldBe(0);

        LayerOperations.SetOpacity(doc, gray, 2.0); // wird auf 1 begrenzt → unverändert
        LayerOperations.SetOpacity(doc, gray, 0.25);
        LayerOperations.SetBlendMode(doc, gray, BlendMode.Screen);
        LayerOperations.Rename(doc, gray, "  Neu  ");
        changes.ShouldBe(3);
        gray.Name.ShouldBe("Neu");
        Should.Throw<ArgumentOutOfRangeException>(() => LayerOperations.SetBlendMode(doc, gray, (BlendMode)99));
    }

    [Fact]
    public void CreateAndDissolveGroup_PreservesOrderAndPosition()
    {
        var doc = Doc();
        var a = AddGray(doc, name: "A");
        var b = AddGray(doc, name: "B");

        var group = LayerOperations.CreateGroup(doc, a, "G");
        doc.Root.Children.ShouldBe([Background(doc), group, b]);
        group.Children.ShouldBe([a]);
        doc.ActiveLayer.ShouldBeSameAs(group);

        LayerOperations.DissolveGroup(doc, group);
        doc.Root.Children.ShouldBe([Background(doc), a, b]);
        doc.ActiveLayer.ShouldBeSameAs(a);
        Should.Throw<LayerOperationException>(() => LayerOperations.DissolveGroup(doc, a)).Error
            .ShouldBe(LayerOperationError.NotGroup);
    }

    [Fact]
    public void Masks_AddInvertDisableApplyRemove()
    {
        var doc = Doc(DocumentBackground.Transparent);
        var layer = Background(doc);
        layer.Pixels.Fill(Bgra32.White);

        var mask = LayerOperations.AddMask(doc, layer);
        LayerOperations.AddMask(doc, layer).ShouldBeSameAs(mask); // keine zweite Maske
        mask.Get(0, 0).ShouldBe((byte)255);

        mask.Fill(new IntRect(0, 0, 8, 6), 128);
        LayerOperations.InvertMask(doc, layer);
        mask.Get(0, 0).ShouldBe((byte)127);

        LayerOperations.SetMaskEnabled(doc, layer, false);
        LayerCompositor.Composite(doc).GetPixel(0, 0).ShouldBe(Bgra32.White);
        LayerOperations.SetMaskEnabled(doc, layer, true);
        var before = LayerCompositor.Composite(doc).GetPixel(0, 0);

        LayerOperations.ApplyMask(doc, layer);

        layer.Mask.ShouldBeNull();
        layer.Pixels.GetPixel(0, 0).ShouldBe(new Bgra32(127, 127, 127, 127)); // 255·127/255
        LayerCompositor.Composite(doc).GetPixel(0, 0).ShouldBe(before); // Aussehen unverändert
        Should.Throw<LayerOperationException>(() => LayerOperations.RemoveMask(doc, layer)).Error
            .ShouldBe(LayerOperationError.NoMask);
    }

    [Fact]
    public void Masks_OnGroup_AreRejected()
    {
        var doc = Doc();
        var group = LayerOperations.CreateGroup(doc, Background(doc), "G");
        Should.Throw<LayerOperationException>(() => LayerOperations.AddMask(doc, group)).Error
            .ShouldBe(LayerOperationError.NotPixelLayer);
    }

    [Theory]
    [InlineData(BlendMode.Normal, 0.4)]
    [InlineData(BlendMode.Multiply, 1.0)]
    [InlineData(BlendMode.Screen, 0.7)]
    [InlineData(BlendMode.Difference, 0.5)]
    public void MergeDown_OntoOpaqueNormalLayer_PreservesAppearance(BlendMode mode, double opacity)
    {
        var doc = Doc();
        var lower = Background(doc);
        for (var x = 0; x < 8; x++)
        {
            lower.Pixels.Fill(new IntRect(x, 0, 1, 6), Bgra32.FromStraight((byte)(x * 30), 100, 200, 255));
        }

        var upper = AddGray(doc, 90);
        upper.Pixels.Fill(new IntRect(0, 0, 3, 6), new Bgra32(20, 40, 60, 100));
        upper.BlendMode = mode;
        upper.Opacity = opacity;
        var before = LayerCompositor.Composite(doc);

        var merged = LayerOperations.MergeDown(doc, upper);

        merged.ShouldBeSameAs(lower);
        doc.LayerCount.ShouldBe(1);
        doc.ActiveLayer.ShouldBeSameAs(lower);
        LayerCompositor.Composite(doc).ContentEquals(before).ShouldBeTrue();
    }

    [Fact]
    public void MergeDown_HiddenUpperLayer_ContributesNothing()
    {
        var doc = Doc();
        var upper = AddGray(doc);
        upper.IsVisible = false;

        LayerOperations.MergeDown(doc, upper);

        Background(doc).Pixels.GetPixel(0, 0).ShouldBe(Bgra32.White);
    }

    [Fact]
    public void MergeDown_RulesForBottomGroupAndLock()
    {
        var doc = Doc();
        LayerOperations.CanMergeDown(doc, Background(doc)).ShouldBeFalse();
        Should.Throw<LayerOperationException>(() => LayerOperations.MergeDown(doc, Background(doc))).Error
            .ShouldBe(LayerOperationError.NoLayerBelow);

        LayerOperations.CreateGroup(doc, Background(doc), "G");
        var top = LayerOperations.AddLayer(doc, "Oben");
        // AddLayer über der aktiven Gruppe → liegt in der Wurzel über der Gruppe
        LayerOperations.CanMergeDown(doc, top).ShouldBeFalse();
        Should.Throw<LayerOperationException>(() => LayerOperations.MergeDown(doc, top)).Error
            .ShouldBe(LayerOperationError.LayerBelowNotPixelLayer);

        var doc2 = Doc();
        var upper = AddGray(doc2);
        LayerOperations.SetLocked(doc2, Background(doc2), true);
        LayerOperations.CanMergeDown(doc2, upper).ShouldBeFalse();
    }

    [Fact]
    public void MergeDown_LowerMaskIsBakedFirst()
    {
        var doc = Doc();
        var lower = Background(doc);
        LayerOperations.AddMask(doc, lower).Fill(new IntRect(0, 0, 4, 6), 0);
        var upper = AddGray(doc);
        upper.Pixels.Fill(Bgra32.Transparent);
        var before = LayerCompositor.Composite(doc);

        LayerOperations.MergeDown(doc, upper);

        lower.Mask.ShouldBeNull();
        LayerCompositor.Composite(doc).ContentEquals(before).ShouldBeTrue();
    }

    [Fact]
    public void Flatten_ProducesSingleLayerWithSameAppearance_HiddenLayersDropped()
    {
        var doc = Doc();
        var gray = AddGray(doc);
        gray.BlendMode = BlendMode.Multiply;
        var hidden = AddGray(doc, 10, "Versteckt");
        hidden.IsVisible = false;
        LayerOperations.CreateGroup(doc, gray, "G");
        var before = LayerCompositor.Composite(doc);

        var flat = LayerOperations.Flatten(doc, "Hintergrund");

        doc.LayerCount.ShouldBe(1);
        doc.ActiveLayer.ShouldBeSameAs(flat);
        flat.Pixels.ContentEquals(before).ShouldBeTrue();
    }

    [Fact]
    public void ImportAsLayer_CopiesTopLeft_AndReportsCropping()
    {
        var doc = Doc();
        var small = PixelBuffer.Create(3, 2);
        small.Fill(Bgra32.Black);

        var (layer, cropped) = LayerOperations.ImportAsLayer(doc, small, "klein");

        cropped.ShouldBeFalse();
        layer.Pixels.GetPixel(2, 1).ShouldBe(Bgra32.Black);
        layer.Pixels.GetPixel(3, 1).ShouldBe(Bgra32.Transparent);
        doc.ActiveLayer.ShouldBeSameAs(layer);

        var large = PixelBuffer.Create(20, 3);
        large.Fill(Bgra32.White);
        var (bigLayer, bigCropped) = LayerOperations.ImportAsLayer(doc, large, "groß");
        bigCropped.ShouldBeTrue();
        bigLayer.Pixels.GetPixel(7, 2).ShouldBe(Bgra32.White);
        bigLayer.Pixels.GetPixel(7, 3).ShouldBe(Bgra32.Transparent);
    }

    [Fact]
    public void Operations_OnForeignLayer_AreRejected()
    {
        var doc = Doc();
        var other = Doc();
        Should.Throw<LayerOperationException>(() => LayerOperations.Rename(doc, Background(other), "x")).Error
            .ShouldBe(LayerOperationError.NotInDocument);
        Should.Throw<LayerOperationException>(() => LayerOperations.Delete(doc, doc.Root)).Error
            .ShouldBe(LayerOperationError.NotInDocument);
    }

    [Fact]
    public void LockedGroup_LocksChildrenForMoves()
    {
        var doc = Doc();
        var a = AddGray(doc, name: "A");
        AddGray(doc, name: "B");
        var group = LayerOperations.CreateGroup(doc, a, "G");
        LayerOperations.AddLayer(doc, "C"); // aktive Gruppe → C wird über der Gruppe eingefügt
        LayerOperations.SetLocked(doc, group, true);

        a.IsEffectivelyLocked.ShouldBeTrue();
        Should.Throw<LayerOperationException>(() => LayerOperations.CreateGroup(doc, a, "H")).Error
            .ShouldBe(LayerOperationError.Locked);
    }
}
