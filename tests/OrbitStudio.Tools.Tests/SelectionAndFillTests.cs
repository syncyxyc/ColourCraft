using OrbitStudio.Domain.Imaging;
using OrbitStudio.Domain.Selection;
using OrbitStudio.Tools.Fill;
using OrbitStudio.Tools.Selection;

namespace OrbitStudio.Tools.Tests;

public class SelectionShapeTests
{
    [Fact]
    public void Rectangle_SelectsPixelsWithCentersInside()
    {
        var mask = SelectionShapes.Rectangle(10, 10, new PointD(7.4, 6), new PointD(2.6, 2));

        mask.Bounds().ShouldBe(new IntRect(3, 2, 4, 4));
        mask.Get(3, 2).ShouldBe((byte)255);
        mask.Get(7, 2).ShouldBe((byte)0);
    }

    [Fact]
    public void Ellipse_IsSymmetric_AndUsesCenterRule()
    {
        var mask = SelectionShapes.Ellipse(20, 20, new PointD(0, 0), new PointD(20, 20));

        mask.Get(10, 10).ShouldBe((byte)255);
        mask.Get(0, 0).ShouldBe((byte)0);
        mask.Get(0, 10).ShouldBe((byte)255); // Mittelpunkt (0,5; 10,5) liegt innerhalb
        for (var y = 0; y < 20; y++)
        {
            for (var x = 0; x < 20; x++)
            {
                mask.Get(x, y).ShouldBe(mask.Get(19 - x, y));
                mask.Get(x, y).ShouldBe(mask.Get(x, 19 - y));
            }
        }
    }

    [Fact]
    public void Polygon_Triangle()
    {
        var mask = SelectionShapes.Polygon(10, 10, [new PointD(0, 0), new PointD(10, 0), new PointD(0, 10)]);

        mask.Get(1, 1).ShouldBe((byte)255);
        mask.Get(8, 8).ShouldBe((byte)0);
        mask.Get(4, 4).ShouldBe((byte)255); // (4,5; 4,5): 4,5 + 4,5 = 9 < 10
        mask.Get(5, 5).ShouldBe((byte)0); // 5,5 + 5,5 = 11 > 10
    }

    [Fact]
    public void Combine_Modes()
    {
        var a = SelectionShapes.Rectangle(10, 1, new PointD(0, 0), new PointD(6, 1));
        var b = SelectionShapes.Rectangle(10, 1, new PointD(4, 0), new PointD(10, 1));

        SelectionMask.Combine(a, b, SelectionCombineMode.Add).Bounds().ShouldBe(new IntRect(0, 0, 10, 1));
        SelectionMask.Combine(a, b, SelectionCombineMode.Subtract).Bounds().ShouldBe(new IntRect(0, 0, 4, 1));
        SelectionMask.Combine(a, b, SelectionCombineMode.Intersect).Bounds().ShouldBe(new IntRect(4, 0, 2, 1));
        SelectionMask.Combine(a, b, SelectionCombineMode.Replace).ContentEquals(b).ShouldBeTrue();
        SelectionMask.Combine(null, b, SelectionCombineMode.Subtract).IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void GrowAndShrink_UseEuclideanDistance()
    {
        var single = SelectionMask.CreateEmpty(11, 11);
        single.Set(5, 5, 255);

        var grown = SelectionShapes.GrowOrShrink(single, 2);
        grown.Get(7, 5).ShouldBe((byte)255); // Abstand 2
        grown.Get(6, 6).ShouldBe((byte)255); // √2
        grown.Get(7, 6).ShouldBe((byte)0); // √5 > 2
        grown.Get(7, 7).ShouldBe((byte)0); // √8

        var square = SelectionShapes.Rectangle(11, 11, new PointD(2, 2), new PointD(9, 9)); // 7×7
        var shrunk = SelectionShapes.GrowOrShrink(square, -2);
        shrunk.Bounds().ShouldBe(new IntRect(4, 4, 3, 3));
    }

    [Fact]
    public void Shrink_TreatsImageBorderAsUnselected()
    {
        var full = SelectionMask.CreateFull(10, 10);
        SelectionShapes.GrowOrShrink(full, -1).Bounds().ShouldBe(new IntRect(1, 1, 8, 8));
    }

    [Fact]
    public void SimilarColor_ContiguousStopsAtBarrier_GlobalDoesNot()
    {
        var pixels = PixelBuffer.Create(9, 3);
        pixels.Fill(Bgra32.White);
        pixels.Fill(new IntRect(4, 0, 1, 3), Bgra32.Black); // senkrechte Wand

        SelectionShapes.SimilarColor(pixels, 0, 0, 0, contiguous: true).Bounds().ShouldBe(new IntRect(0, 0, 4, 3));
        var global = SelectionShapes.SimilarColor(pixels, 0, 0, 0, contiguous: false);
        global.Get(8, 2).ShouldBe((byte)255);
        global.Get(4, 1).ShouldBe((byte)0);
    }

    [Fact]
    public void SimilarColor_ToleranceInPercentOfChannelRange()
    {
        var pixels = PixelBuffer.Create(3, 1);
        pixels.SetPixel(0, 0, Bgra32.FromStraight(100, 100, 100, 255));
        pixels.SetPixel(1, 0, Bgra32.FromStraight(125, 100, 100, 255)); // Abweichung 25 ≈ 9,8 %
        pixels.SetPixel(2, 0, Bgra32.FromStraight(130, 100, 100, 255)); // Abweichung 30 ≈ 11,8 %

        var mask = SelectionShapes.SimilarColor(pixels, 0, 0, 10, contiguous: false);

        mask.Get(1, 0).ShouldBe((byte)255);
        mask.Get(2, 0).ShouldBe((byte)0);
    }

    [Fact]
    public void SimilarColor_LargeAreaDoesNotOverflowStack()
    {
        var pixels = PixelBuffer.Create(1500, 1500);
        SelectionShapes.SimilarColor(pixels, 0, 0, 0, true).Bounds().ShouldBe(new IntRect(0, 0, 1500, 1500));
    }
}

public class SelectionToolTests : ToolTestBase
{
    [Fact]
    public void RectangleSelect_CreatesSelection_WithHistory_AndModifiers()
    {
        var tool = new RectangleSelectTool();
        Drag(tool, 2, 2, 8, 8);
        Document.Selection!.Bounds().ShouldBe(new IntRect(2, 2, 6, 6));

        Drag(tool, 6, 6, 12, 12, modifiers: ToolModifiers.Shift); // hinzufügen
        Document.Selection!.Bounds().ShouldBe(new IntRect(2, 2, 10, 10));

        Drag(tool, 2, 2, 12, 5, modifiers: ToolModifiers.Alt); // abziehen
        Document.Selection!.Get(3, 3).ShouldBe((byte)0);
        Document.Selection.Get(3, 6).ShouldBe((byte)255);

        History.UndoCount.ShouldBe(3);
        History.Undo();
        History.Undo();
        Document.Selection!.Bounds().ShouldBe(new IntRect(2, 2, 6, 6));
    }

    [Fact]
    public void ClickWithoutDrag_Deselects()
    {
        var tool = new RectangleSelectTool();
        Drag(tool, 2, 2, 8, 8);
        Click(tool, 5, 5);
        Document.Selection.ShouldBeNull();
    }

    [Fact]
    public void PolygonSelect_ClosesOnDoubleClick_AndEscapeCancels()
    {
        var tool = new PolygonSelectTool();
        Click(tool, 2, 2);
        Click(tool, 12, 2);
        Click(tool, 12, 12);
        tool.IsActiveOperation.ShouldBeTrue();
        Click(tool, 2, 12, clicks: 2);

        tool.IsActiveOperation.ShouldBeFalse();
        Document.Selection!.Get(10, 4).ShouldBe((byte)255);
        Document.Selection.Get(3, 10).ShouldBe((byte)0);

        Click(tool, 1, 1);
        tool.Key(Context, ToolKey.Escape, ToolModifiers.None).ShouldBeTrue();
        tool.IsActiveOperation.ShouldBeFalse();
    }

    [Fact]
    public void Lasso_SelectsEnclosedArea()
    {
        var tool = new LassoSelectTool();
        tool.PointerDown(Context, At(2, 2));
        foreach (var (x, y) in new[] { (15.0, 2.0), (15.0, 15.0), (2.0, 15.0) })
        {
            tool.PointerMove(Context, At(x, y));
        }

        tool.PointerUp(Context, At(2, 15));
        Document.Selection!.Get(8, 8).ShouldBe((byte)255);
        Document.Selection.Get(17, 17).ShouldBe((byte)0);
    }

    [Fact]
    public void MagicWand_SelectsContiguousRegion()
    {
        Pixels.Fill(new IntRect(10, 0, 1, 20), Bgra32.Black);
        Click(new MagicWandTool(contiguous: true), 2, 2);
        Document.Selection!.Bounds().ShouldBe(new IntRect(0, 0, 10, 20));

        Click(new MagicWandTool(contiguous: false), 2, 2);
        Document.Selection!.Get(15, 5).ShouldBe((byte)255);
    }

    [Fact]
    public void SelectionEditing_InvertGrowShrinkFromAlpha()
    {
        SelectionEditing.Invert(Document, History);
        Document.Selection!.IsEmpty.ShouldBeFalse(); // ohne Auswahl → alles
        SelectionEditing.Deselect(Document, History);
        Document.Selection.ShouldBeNull();

        Document.SetSelection(SelectionShapes.Rectangle(20, 20, new PointD(5, 5), new PointD(10, 10)));
        SelectionEditing.Grow(Document, History, 1);
        Document.Selection!.Bounds().ShouldBe(new IntRect(4, 4, 7, 7));
        SelectionEditing.Shrink(Document, History, 2);
        Document.Selection!.Bounds().ShouldBe(new IntRect(6, 6, 3, 3));

        Pixels.Fill(Bgra32.Transparent);
        Pixels.Fill(new IntRect(1, 1, 2, 2), Bgra32.White);
        SelectionEditing.FromLayerAlpha(Document, History);
        Document.Selection!.Bounds().ShouldBe(new IntRect(1, 1, 2, 2));
    }
}

public class FillToolTests : ToolTestBase
{
    [Fact]
    public void Bucket_FillsContiguousArea_StopsAtBarrier_IsUndoable()
    {
        Pixels.Fill(new IntRect(10, 0, 1, 20), Bgra32.Black);
        Colors.Primary = new ColorRgba(255, 0, 0);
        var original = Pixels.Clone();

        Click(new BucketTool(), 2, 2);

        Pixels.GetPixel(0, 0).ShouldBe(new Bgra32(0, 0, 255, 255));
        Pixels.GetPixel(9, 19).ShouldBe(new Bgra32(0, 0, 255, 255));
        Pixels.GetPixel(10, 5).ShouldBe(Bgra32.Black);
        Pixels.GetPixel(15, 5).ShouldBe(Bgra32.White);
        History.Undo();
        Pixels.ContentEquals(original).ShouldBeTrue();
    }

    [Fact]
    public void Bucket_GlobalMode_FillsAllMatchingPixels_InsideSelectionOnly()
    {
        Pixels.Fill(new IntRect(10, 0, 1, 20), Bgra32.Black);
        Document.SetSelection(SelectionShapes.Rectangle(20, 20, new PointD(0, 0), new PointD(20, 10)));
        var bucket = new BucketTool();
        bucket.FloodMode.Index = 1;

        Click(bucket, 2, 2);

        Pixels.GetPixel(15, 5).ShouldBe(Bgra32.Black);
        Pixels.GetPixel(15, 15).ShouldBe(Bgra32.White); // außerhalb der Auswahl
    }

    [Fact]
    public void Pipette_PicksLayerOrImageColor()
    {
        Pixels.SetPixel(3, 3, Bgra32.FromStraight(10, 20, 30, 255));
        var pipette = new PipetteTool();

        Click(pipette, 3.5, 3.5);
        Colors.Primary.ShouldBe(new ColorRgba(10, 20, 30));

        Click(pipette, 0.5, 0.5, PointerButton.Secondary);
        Colors.Secondary.ShouldBe(ColorRgba.White);
        History.CanUndo.ShouldBeFalse(); // Pipette ändert keine Pixel
    }

    [Fact]
    public void Gradient_LinearFromBlackToWhite()
    {
        Colors.Primary = ColorRgba.Black;
        Colors.Secondary = ColorRgba.White;
        var gradient = new GradientTool();

        Drag(gradient, 0, 10, 20, 10);

        Pixels.GetPixel(0, 5).R.ShouldBeLessThan((byte)10);
        Pixels.GetPixel(19, 5).R.ShouldBeGreaterThan((byte)245);
        Pixels.GetPixel(10, 5).R.ShouldBeInRange((byte)125, (byte)140);
        GradientTool.Position(new PointD(10, 0), new PointD(0, 0), new PointD(20, 0), radial: false).ShouldBe(0.5);
        GradientTool.Position(new PointD(0, 5), new PointD(0, 0), new PointD(10, 0), radial: true).ShouldBe(0.5);
        History.UndoCount.ShouldBe(1);
    }
}
