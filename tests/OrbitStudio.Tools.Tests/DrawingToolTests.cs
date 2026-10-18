using OrbitStudio.Domain.Imaging;
using OrbitStudio.Domain.Layers;
using OrbitStudio.Domain.Selection;
using OrbitStudio.Tools.Drawing;
using OrbitStudio.Tools.Painting;

namespace OrbitStudio.Tools.Tests;

public class DrawingToolTests : ToolTestBase
{
    [Fact]
    public void Pencil_Size1_Click_SetsExactlyOnePixel()
    {
        var pencil = new PencilTool();
        Click(pencil, 5.5, 7.5);

        Pixels.GetPixel(5, 7).ShouldBe(Bgra32.Black);
        CountPixels(p => p != Bgra32.White).ShouldBe(1);
        History.UndoNameKey.ShouldBe("Tool.pencil");
    }

    [Fact]
    public void Pencil_HorizontalLine_IsContinuousAndOnePixelHigh()
    {
        var pencil = new PencilTool();
        Drag(pencil, 2.5, 10.5, 15.5, 10.5);

        for (var x = 2; x <= 15; x++)
        {
            Pixels.GetPixel(x, 10).ShouldBe(Bgra32.Black, $"x = {x}");
        }

        CountPixels(p => p != Bgra32.White).ShouldBe(14);
    }

    [Fact]
    public void Pencil_RightButtonUsesSecondaryColor()
    {
        Colors.Secondary = new ColorRgba(255, 0, 0);
        Click(new PencilTool(), 3.5, 3.5, PointerButton.Secondary);
        Pixels.GetPixel(3, 3).ShouldBe(new Bgra32(0, 0, 255, 255));
    }

    [Fact]
    public void Brush_Stroke_UndoRestoresExactly_RedoReappliesExactly()
    {
        var original = Pixels.Clone();
        var brush = new BrushTool();
        brush.Size.Value = 6;
        Drag(brush, 3, 3, 16, 14, steps: 10);
        var painted = Pixels.Clone();

        painted.ContentEquals(original).ShouldBeFalse();
        History.UndoCount.ShouldBe(1);
        History.Undo();
        Pixels.ContentEquals(original).ShouldBeTrue();
        History.Redo();
        Pixels.ContentEquals(painted).ShouldBeTrue();
    }

    [Fact]
    public void Brush_OverlappingStroke_DoesNotAccumulateOpacity()
    {
        var brush = new BrushTool();
        brush.Size.Value = 4;
        brush.HardnessOption.Value = 100;
        brush.Opacity.Value = 50;
        // Hin und zurück über dieselbe Stelle in einem Zug
        brush.PointerDown(Context, At(5.5, 10.5));
        brush.PointerMove(Context, At(15.5, 10.5));
        brush.PointerMove(Context, At(5.5, 10.5));
        brush.PointerUp(Context, At(5.5, 10.5));

        // Schwarz 50 % über Weiß: 255·0,5 = 127,5 → 128 (kaufmännisch)
        Pixels.GetPixel(10, 10).ShouldBe(new Bgra32(128, 128, 128, 255));
    }

    [Fact]
    public void Brush_RespectsSelection()
    {
        Document.SetSelection(SelectionShapes.Rectangle(20, 20, new PointD(0, 0), new PointD(10, 20)));
        var brush = new BrushTool();
        brush.Size.Value = 4;
        brush.HardnessOption.Value = 100;
        Drag(brush, 2.5, 10.5, 18.5, 10.5, steps: 8);

        Pixels.GetPixel(5, 10).ShouldBe(Bgra32.Black);
        Pixels.GetPixel(9, 10).ShouldBe(Bgra32.Black);
        Pixels.GetPixel(10, 10).ShouldBe(Bgra32.White); // außerhalb der Auswahl
        Pixels.GetPixel(15, 10).ShouldBe(Bgra32.White);
    }

    [Fact]
    public void Eraser_MakesPixelsTransparent()
    {
        var eraser = new EraserTool();
        eraser.Size.Value = 3;
        eraser.HardnessOption.Value = 100;
        Click(eraser, 10.5, 10.5);

        Pixels.GetPixel(10, 10).ShouldBe(Bgra32.Transparent);
        Pixels.GetPixel(0, 0).ShouldBe(Bgra32.White);
    }

    [Fact]
    public void Escape_CancelsStroke_RestoringOriginal()
    {
        var original = Pixels.Clone();
        var brush = new BrushTool();
        brush.PointerDown(Context, At(5, 5));
        brush.PointerMove(Context, At(15, 15));
        brush.IsActiveOperation.ShouldBeTrue();

        brush.Key(Context, ToolKey.Escape, ToolModifiers.None).ShouldBeTrue();

        Pixels.ContentEquals(original).ShouldBeTrue();
        History.CanUndo.ShouldBeFalse();
    }

    [Fact]
    public void LockedLayer_IsNotPainted_AndProblemReported()
    {
        Layer.IsLocked = true;
        var original = Pixels.Clone();

        Click(new PencilTool(), 5.5, 5.5);

        Pixels.ContentEquals(original).ShouldBeTrue();
        Problems.ShouldContain(ToolProblem.LayerLocked);
    }

    [Fact]
    public void Group_AsActiveLayer_ReportsNoPixelLayer()
    {
        var group = new LayerGroup("G");
        Document.Root.Add(group);
        Document.ActiveLayer = group;

        Click(new BrushTool(), 5, 5);

        Problems.ShouldContain(ToolProblem.NoPixelLayer);
    }

    [Fact]
    public void FilledRectangle_HasCrispPixelExactEdges()
    {
        var rect = new RectangleTool(filled: true);
        Drag(rect, 4.2, 5.4, 9.6, 8.7); // Ecken gerundet → (4,5)–(10,9)

        for (var y = 0; y < 20; y++)
        {
            for (var x = 0; x < 20; x++)
            {
                var inside = x is >= 4 and < 10 && y is >= 5 and < 9;
                Pixels.GetPixel(x, y).ShouldBe(inside ? Bgra32.Black : Bgra32.White, $"({x},{y})");
            }
        }
    }

    [Fact]
    public void RectangleOutline_HasLineWidthInside()
    {
        var rect = new RectangleTool(filled: false);
        rect.LineWidth.Value = 2;
        Drag(rect, 2, 2, 12, 12);

        Pixels.GetPixel(2, 2).ShouldBe(Bgra32.Black);
        Pixels.GetPixel(3, 6).ShouldBe(Bgra32.Black);
        Pixels.GetPixel(4, 6).ShouldBe(Bgra32.White); // innen frei
        Pixels.GetPixel(11, 11).ShouldBe(Bgra32.Black);
        Pixels.GetPixel(12, 12).ShouldBe(Bgra32.White); // außerhalb
    }

    [Fact]
    public void ShapePreview_DoesNotLeaveTracesOfEarlierPositions()
    {
        var rect = new RectangleTool(filled: true);
        rect.PointerDown(Context, At(2, 2));
        rect.PointerMove(Context, At(18, 18)); // große Vorschau
        rect.PointerMove(Context, At(5, 5)); // kleiner
        rect.PointerUp(Context, At(5, 5));

        CountPixels(p => p == Bgra32.Black).ShouldBe(9);
        History.UndoCount.ShouldBe(1);
    }

    [Fact]
    public void FilledEllipse_IsSymmetricAndInsideBox()
    {
        var ellipse = new EllipseTool(filled: true);
        ellipse.AntialiasOption.Value = false;
        Drag(ellipse, 2, 2, 18, 12);

        Pixels.GetPixel(10, 7).ShouldBe(Bgra32.Black); // Mitte
        Pixels.GetPixel(2, 2).ShouldBe(Bgra32.White); // Ecke außerhalb der Ellipse
        for (var y = 0; y < 20; y++)
        {
            for (var x = 0; x < 20; x++)
            {
                Pixels.GetPixel(x, y).ShouldBe(Pixels.GetPixel(19 - x, y), $"waagerecht ({x},{y})");
                if (x < 2 || x >= 18 || y < 2 || y >= 12)
                {
                    Pixels.GetPixel(x, y).ShouldBe(Bgra32.White);
                }
            }
        }
    }

    [Fact]
    public void Line_ShiftSnapsTo15Degrees()
    {
        var line = new LineTool();
        line.LineWidth.Value = 1;
        line.AntialiasOption.Value = false;
        line.PointerDown(Context, At(2.5, 10.5));
        line.PointerUp(Context, At(17.5, 11.6, modifiers: ToolModifiers.Shift)); // fast waagerecht → 0°

        for (var x = 3; x <= 17; x++)
        {
            Pixels.GetPixel(x, 10).ShouldBe(Bgra32.Black);
        }

        Pixels.GetPixel(17, 11).ShouldBe(Bgra32.White);
    }

    [Fact]
    public void PaintingOnMask_ChangesMaskNotPixels_AndIsUndoable()
    {
        Layer.Mask = LayerMask.CreateRevealAll(20, 20);
        Context = new ToolContext(Document, History, Colors, View) { EditMask = true };
        var pixels = Pixels.Clone();

        Click(new PencilTool(), 4.5, 4.5); // Schwarz → Maskenwert 0

        Layer.Mask.Get(4, 4).ShouldBe((byte)0);
        Pixels.ContentEquals(pixels).ShouldBeTrue();
        History.Undo();
        Layer.Mask.Get(4, 4).ShouldBe((byte)255);
    }

    [Fact]
    public void BrushProfile_HardEdgeAndSoftFalloff()
    {
        Coverage.BrushProfile(0, 5, 1, true).ShouldBe(1);
        Coverage.BrushProfile(5, 5, 1, true).ShouldBe(0.5);
        Coverage.BrushProfile(6, 5, 1, true).ShouldBe(0);
        Coverage.BrushProfile(2, 5, 0.5, true).ShouldBe(1); // innerhalb des harten Kerns
        Coverage.BrushProfile(4, 5, 0.5, true).ShouldBeInRange(0.01, 0.99);
        Coverage.BrushProfile(5.6, 5, 0.5, true).ShouldBe(0);
    }
}
