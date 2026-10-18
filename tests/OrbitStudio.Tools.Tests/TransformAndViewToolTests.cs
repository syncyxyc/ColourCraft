using OrbitStudio.Domain.Imaging;
using OrbitStudio.Domain.Layers;
using OrbitStudio.Domain.Selection;
using OrbitStudio.Tools.Editing;
using OrbitStudio.Tools.Transform;

namespace OrbitStudio.Tools.Tests;

public class TransformToolTests : ToolTestBase
{
    [Fact]
    public void Move_SelectedPixels_LeavesTransparentHole_MovesSelection_Undoable()
    {
        Pixels.Fill(new IntRect(2, 2, 3, 3), Bgra32.Black);
        Document.SetSelection(SelectionShapes.Rectangle(20, 20, new PointD(2, 2), new PointD(5, 5)));
        var original = Pixels.Clone();

        Drag(new MoveTool(), 3, 3, 13, 8);

        Pixels.GetPixel(12, 7).ShouldBe(Bgra32.Black);
        Pixels.GetPixel(14, 9).ShouldBe(Bgra32.Black);
        Pixels.GetPixel(2, 2).ShouldBe(Bgra32.Transparent);
        Document.Selection!.Bounds().ShouldBe(new IntRect(12, 7, 3, 3));

        History.UndoCount.ShouldBe(1);
        History.Undo();
        Pixels.ContentEquals(original).ShouldBeTrue();
        Document.Selection!.Bounds().ShouldBe(new IntRect(2, 2, 3, 3));
    }

    [Fact]
    public void Move_WithoutSelection_MovesWholeLayer_AndEscapeRestores()
    {
        Pixels.Fill(Bgra32.Transparent);
        Pixels.SetPixel(0, 0, Bgra32.Black);
        var original = Pixels.Clone();
        var move = new MoveTool();

        move.PointerDown(Context, At(5, 5));
        move.PointerMove(Context, At(8, 9));
        Pixels.GetPixel(3, 4).ShouldBe(Bgra32.Black);
        move.Key(Context, ToolKey.Escape, ToolModifiers.None);

        Pixels.ContentEquals(original).ShouldBeTrue();
        History.CanUndo.ShouldBeFalse();
    }

    [Fact]
    public void Move_ArrowKeysNudge()
    {
        Pixels.Fill(Bgra32.Transparent);
        Pixels.SetPixel(5, 5, Bgra32.Black);
        var move = new MoveTool();

        move.Key(Context, ToolKey.Right, ToolModifiers.None);
        move.Key(Context, ToolKey.Down, ToolModifiers.Shift);

        Pixels.GetPixel(6, 15).ShouldBe(Bgra32.Black);
        History.UndoCount.ShouldBe(2);
    }

    [Fact]
    public void Crop_Tool_CommitsWithEnter()
    {
        Pixels.SetPixel(5, 6, Bgra32.Black);
        var crop = new CropTool();
        Drag(crop, 5, 6, 15, 12);
        Document.Width.ShouldBe(20); // noch nicht übernommen

        crop.Key(Context, ToolKey.Enter, ToolModifiers.None);

        Document.Width.ShouldBe(10);
        Document.Height.ShouldBe(6);
        Pixels.GetPixel(0, 0).ShouldBe(Bgra32.Black);
        History.Undo();
        Document.Width.ShouldBe(20);
        Pixels.GetPixel(5, 6).ShouldBe(Bgra32.Black);
    }

    [Fact]
    public void Copy_And_Delete_Selection()
    {
        Pixels.Fill(new IntRect(2, 2, 2, 2), Bgra32.Black);
        Document.SetSelection(SelectionShapes.Rectangle(20, 20, new PointD(2, 2), new PointD(5, 4)));

        var (copied, origin) = SelectionContent.Copy(Document, Layer)!.Value;
        origin.ShouldBe(new IntRect(2, 2, 3, 2));
        copied.GetPixel(0, 0).ShouldBe(Bgra32.Black);
        copied.GetPixel(2, 0).ShouldBe(Bgra32.White);

        SelectionContent.Delete(Context).ShouldBeTrue();
        Pixels.GetPixel(2, 2).ShouldBe(Bgra32.Transparent);
        Pixels.GetPixel(5, 2).ShouldBe(Bgra32.White);
        History.Undo();
        Pixels.GetPixel(2, 2).ShouldBe(Bgra32.Black);
    }
}

public class DocumentTransformTests : ToolTestBase
{
    public DocumentTransformTests()
        : base(4, 2)
    {
    }

    [Fact]
    public void Rotate90_SwapsSize_AllLayersAndSelection_Undoable()
    {
        Pixels.SetPixel(0, 0, Bgra32.Black);
        var second = new PixelLayer("Zwei", PixelBuffer.Create(4, 2));
        second.Pixels.SetPixel(3, 1, Bgra32.Black);
        Document.Root.Add(second);
        Document.SetSelection(SelectionShapes.Rectangle(4, 2, new PointD(0, 0), new PointD(1, 1)));

        DocumentTransforms.Rotate(Document, History, 90);

        Document.Width.ShouldBe(2);
        Document.Height.ShouldBe(4);
        Pixels.GetPixel(1, 0).ShouldBe(Bgra32.Black); // (0,0) → (H−1−0, 0)
        second.Pixels.GetPixel(0, 3).ShouldBe(Bgra32.Black); // (3,1) → (2−1−1, 3)
        Document.Selection!.Get(1, 0).ShouldBe((byte)255);

        History.Undo();
        Document.Width.ShouldBe(4);
        Pixels.GetPixel(0, 0).ShouldBe(Bgra32.Black);
        second.Pixels.GetPixel(3, 1).ShouldBe(Bgra32.Black);
    }

    [Fact]
    public void Flip_And_CanvasSize_WithAnchor()
    {
        Pixels.SetPixel(0, 0, Bgra32.Black);
        DocumentTransforms.Flip(Document, History, horizontal: true);
        Pixels.GetPixel(3, 0).ShouldBe(Bgra32.Black);

        Layer.Mask = LayerMask.CreateRevealAll(4, 2);
        DocumentTransforms.ResizeCanvas(Document, History, 8, 4, CanvasAnchor.Center);
        Document.Width.ShouldBe(8);
        Pixels.GetPixel(5, 1).ShouldBe(Bgra32.Black); // Versatz (2,1)
        Pixels.GetPixel(0, 0).ShouldBe(Bgra32.Transparent); // neue Fläche transparent
        Layer.Mask!.Get(0, 0).ShouldBe((byte)255); // neue Maskenfläche sichtbar
        DocumentTransforms.AnchorOffset(4, 2, 8, 4, CanvasAnchor.BottomRight).ShouldBe((4, 2));
    }

    [Fact]
    public void ResizeImage_DownscaleAveragesArea_UpscaleKeepsUniformColor()
    {
        Pixels.Fill(Bgra32.White);
        Pixels.SetPixel(0, 0, Bgra32.Black);
        Pixels.SetPixel(1, 0, Bgra32.Black);
        Pixels.SetPixel(0, 1, Bgra32.Black);
        Pixels.SetPixel(1, 1, Bgra32.Black);

        DocumentTransforms.ResizeImage(Document, History, 2, 1);

        Pixels.GetPixel(0, 0).ShouldBe(Bgra32.Black); // Mittel aus 4 schwarzen Pixeln
        Pixels.GetPixel(1, 0).ShouldBe(Bgra32.White);

        DocumentTransforms.ResizeImage(Document, History, 20, 10);
        Pixels.GetPixel(0, 5).ShouldBe(Bgra32.Black);
        Pixels.GetPixel(19, 5).ShouldBe(Bgra32.White);
        History.Undo();
        History.Undo();
        Document.Width.ShouldBe(4);
    }

    [Fact]
    public void Resize_TransparentNeighbours_DoNotDarkenColor()
    {
        var source = PixelBuffer.Create(2, 1);
        source.SetPixel(0, 0, Bgra32.FromStraight(255, 0, 0, 255));
        // (1,0) transparent
        var result = Resampler.Resize(source, 1, 1).GetPixel(0, 0);

        // vormultipliert gemittelt: Rot bleibt reines Rot mit halber Deckkraft (kein Abdunkeln)
        result.ShouldBe(new Bgra32(0, 0, 128, 128));
        ColorRgba.FromPremultiplied(result).R.ShouldBe((byte)255);
    }

    [Fact]
    public void RotateScale_ZeroAngleAndScaleOne_IsIdentity()
    {
        Pixels.SetPixel(1, 1, Bgra32.FromStraight(10, 200, 30, 255));
        var rotated = Resampler.RotateScale(Pixels, 0, 1);
        rotated.ContentEquals(Pixels).ShouldBeTrue();
    }

    [Fact]
    public void FlipLayer_OnlyAffectsThatLayer()
    {
        var second = new PixelLayer("Zwei", PixelBuffer.Create(4, 2));
        second.Pixels.SetPixel(0, 0, Bgra32.Black);
        Document.Root.Add(second);
        Pixels.SetPixel(0, 0, Bgra32.Black);

        DocumentTransforms.FlipLayer(Document, History, second, horizontal: true);

        second.Pixels.GetPixel(3, 0).ShouldBe(Bgra32.Black);
        Pixels.GetPixel(0, 0).ShouldBe(Bgra32.Black);
        History.Undo();
        second.Pixels.GetPixel(0, 0).ShouldBe(Bgra32.Black);
    }
}

public class ViewToolTests : ToolTestBase
{
    [Fact]
    public void Zoom_ClickZoomsIn_RightClickZoomsOut()
    {
        View.ActualSize();
        Click(new ZoomTool(), 10, 10);
        View.Zoom.ShouldBe(1.5);
        Click(new ZoomTool(), 10, 10, PointerButton.Secondary);
        View.Zoom.ShouldBe(1.0);
    }

    [Fact]
    public void Hand_PansView_WithoutChangingPixels()
    {
        View.SetZoom(32);
        var before = View.OffsetX;
        var pixels = Pixels.Clone();

        var hand = new HandTool();
        hand.PointerDown(Context, At(100, 100));
        hand.PointerMove(Context, At(60, 100));
        hand.PointerUp(Context, At(60, 100));

        View.OffsetX.ShouldBe(before - 40, 1e-9);
        Pixels.ContentEquals(pixels).ShouldBeTrue();
        History.CanUndo.ShouldBeFalse();
    }

    [Fact]
    public void DefaultToolSet_HasUniqueIdsAndShortcuts()
    {
        var tools = ToolSet.CreateDefault();
        tools.Select(t => t.Id).ShouldBeUnique();
        tools.Where(t => t.Shortcut is not null).Select(t => t.Shortcut).ShouldBeUnique();
        tools.Count.ShouldBe(23);
    }
}
