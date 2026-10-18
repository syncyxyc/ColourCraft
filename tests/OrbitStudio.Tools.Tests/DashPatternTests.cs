using OrbitStudio.Domain.Imaging;
using OrbitStudio.Domain.Selection;
using OrbitStudio.Tools.Drawing;

namespace OrbitStudio.Tools.Tests;

/// <summary>Segmentberechnung des Strichmusters (Spezifikation 1.1), ohne Oberfläche.</summary>
public class DashLineTests
{
    private static List<(double From, double To)> Segments(DashLine line) =>
        line.Segments.Select(s => (s.StartDistance, s.EndDistance)).ToList();

    [Fact]
    public void Period_IsDashPlusGap_10And5GivesTenPixelDashesWithFivePixelGaps()
    {
        var line = new DashLine(new PointD(0, 0), new PointD(100, 0), 10, 5);

        line.Period.ShouldBe(15);
        Segments(line).ShouldBe([(0, 10), (15, 25), (30, 40), (45, 55), (60, 70), (75, 85), (90, 100)]);
        line.Segments.Take(6).ShouldAllBe(s => s.Length == 10);
    }

    [Fact]
    public void LastSegment_IsShortenedAtEndPoint()
    {
        var line = new DashLine(new PointD(0, 0), new PointD(95, 0), 10, 5);

        line.Count.ShouldBe(7);
        var last = line.SegmentAt(6);
        last.StartDistance.ShouldBe(90);
        last.EndDistance.ShouldBe(95);
        last.End.ShouldBe(new PointD(95, 0));
    }

    [Fact]
    public void DashStartingExactlyAtEndPoint_IsOmitted()
    {
        Segments(new DashLine(new PointD(0, 0), new PointD(30, 0), 10, 5)).ShouldBe([(0, 10), (15, 25)]);
        Segments(new DashLine(new PointD(0, 0), new PointD(25, 0), 10, 5)).ShouldBe([(0, 10), (15, 25)]);
        Segments(new DashLine(new PointD(0, 0), new PointD(30.5, 0), 10, 5)).ShouldBe([(0, 10), (15, 25), (30, 30.5)]);
    }

    [Fact]
    public void IdenticalPoints_GiveNoSegmentsAndNoDirection()
    {
        var line = new DashLine(new PointD(7.5, 3), new PointD(7.5, 3), 10, 5);

        line.IsPoint.ShouldBeTrue();
        line.Length.ShouldBe(0);
        line.Count.ShouldBe(0);
        line.Direction.ShouldBe(new PointD(0, 0));
        line.AngleDegrees.ShouldBe(0);
        line.Segments.ShouldBeEmpty();
        Should.Throw<ArgumentOutOfRangeException>(() => line.SegmentAt(0));
    }

    [Fact]
    public void LineShorterThanOneDash_IsOneShortenedSegment()
    {
        var line = new DashLine(new PointD(2, 2), new PointD(5, 6), 10, 5);

        line.Length.ShouldBe(5);
        Segments(line).ShouldBe([(0, 5)]);
        line.SegmentAt(0).End.ShouldBe(new PointD(5, 6));
    }

    [Fact]
    public void GapZero_GivesOneContinuousSegment()
    {
        var line = new DashLine(new PointD(0, 0), new PointD(0, 47), 10, 0);

        Segments(line).ShouldBe([(0, 47)]);
    }

    [Fact]
    public void VeryLargeDash_GivesOneSegment()
    {
        var line = new DashLine(new PointD(0, 0), new PointD(50, 0), 1e9, 1e9);

        Segments(line).ShouldBe([(0, 50)]);
    }

    [Fact]
    public void VeryLongLine_IsComputedWithoutStoringSegments()
    {
        var line = new DashLine(new PointD(0, 0), new PointD(10_000_000, 0), 1, 1);

        line.Count.ShouldBe(5_000_000);
        var last = line.SegmentAt(line.Count - 1);
        last.StartDistance.ShouldBe(9_999_998);
        last.EndDistance.ShouldBe(9_999_999);
    }

    [Fact]
    public void VeryLargeGap_GivesOnlyFirstDash()
    {
        Segments(new DashLine(new PointD(0, 0), new PointD(1000, 0), 10, 5000)).ShouldBe([(0, 10)]);
    }

    [Theory]
    [InlineData(10, 0, 0)]       // rechts
    [InlineData(0, -10, 90)]     // oben (y nach unten)
    [InlineData(-10, 0, 180)]    // links
    [InlineData(0, 10, 270)]     // unten
    [InlineData(10, -10, 45)]    // diagonal rechts oben
    [InlineData(-10, -10, 135)]
    [InlineData(-10, 10, 225)]
    [InlineData(10, 10, 315)]
    public void Angle_AndDirection_ForMainDirections(double dx, double dy, double expectedAngle)
    {
        var line = new DashLine(new PointD(5, 5), new PointD(5 + dx, 5 + dy), 3, 2);

        line.AngleDegrees.ShouldBe(expectedAngle, 1e-9);
        line.Direction.Length.ShouldBe(1, 1e-12);
        line.Direction.X.ShouldBe(dx / line.Length, 1e-12);
        line.Direction.Y.ShouldBe(dy / line.Length, 1e-12);
    }

    [Fact]
    public void AllAngles_SegmentsLieOnLineAndCoverExpectedLength()
    {
        for (var degrees = 0; degrees < 360; degrees += 5)
        {
            var radians = degrees * Math.PI / 180;
            var end = new PointD(50 + 37 * Math.Cos(radians), 50 - 37 * Math.Sin(radians));
            var line = new DashLine(new PointD(50, 50), end, 4, 3);

            line.Length.ShouldBe(37, 1e-9);
            line.AngleDegrees.ShouldBe(degrees == 0 ? 0 : degrees, 1e-9, $"Winkel {degrees}");

            // 37 = 5 · 7 + 2: fünf volle Striche, der sechste auf 2 px gekürzt.
            line.Count.ShouldBe(6, $"Winkel {degrees}");
            line.Segments.Sum(s => s.Length).ShouldBe(5 * 4 + 2, 1e-9);
            foreach (var segment in line.Segments)
            {
                // Endpunkte liegen auf der Linie im richtigen Abstand vom Start.
                (segment.Start - line.Start).Length.ShouldBe(segment.StartDistance, 1e-9);
                (segment.End - line.Start).Length.ShouldBe(segment.EndDistance, 1e-9);
            }

            line.SegmentAt(5).End.ShouldBe(end);
        }
    }

    [Theory]
    [InlineData(0, 5)]
    [InlineData(-1, 5)]
    [InlineData(double.NaN, 5)]
    [InlineData(10, -1)]
    [InlineData(10, double.PositiveInfinity)]
    public void InvalidParameters_AreRejected(double dash, double gap)
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new DashLine(new PointD(0, 0), new PointD(10, 0), dash, gap));
    }

    [Fact]
    public void NonFiniteCoordinates_AreRejected()
    {
        Should.Throw<ArgumentException>(() => new DashLine(new PointD(double.NaN, 0), new PointD(10, 0), 10, 5));
    }
}

/// <summary>Werkzeug: Kontrollpunkte, Vorschau-Überlagerung, Parameter, Anwenden/Abbrechen/Zurücksetzen, Historie.</summary>
public class DashPatternToolTests : ToolTestBase
{
    private readonly DashPatternTool _tool = new();

    public DashPatternToolTests()
        : base(40, 30)
    {
    }

    /// <summary>Zeigerereignis mit passender Viewport-Position (wie von der Leinwand geliefert).</summary>
    private ToolPointerEvent Ev(double x, double y, ToolModifiers modifiers = ToolModifiers.None)
    {
        var p = new PointD(x, y);
        return new ToolPointerEvent(p, View.DocumentToViewport(p), PointerButton.Primary, modifiers);
    }

    private void DragDash(double x0, double y0, double x1, double y1, ToolModifiers modifiers = ToolModifiers.None)
    {
        _tool.PointerDown(Context, Ev(x0, y0, modifiers));
        for (var i = 1; i <= 4; i++)
        {
            _tool.PointerMove(Context, Ev(x0 + (x1 - x0) * i / 4, y0 + (y1 - y0) * i / 4, modifiers));
        }

        _tool.PointerUp(Context, Ev(x1, y1, modifiers));
    }

    private bool IsBlack(int x, int y) => Pixels.GetPixel(x, y) == Bgra32.Black;

    private void ShouldBeHorizontalDashes(int y0, int rows)
    {
        for (var y = 0; y < Pixels.Height; y++)
        {
            for (var x = 0; x < Pixels.Width; x++)
            {
                var inRow = y >= y0 && y < y0 + rows;
                var inDash = x is >= 0 and < 10 or >= 15 and < 25;
                Pixels.GetPixel(x, y).ShouldBe(inRow && inDash ? Bgra32.Black : Bgra32.White, $"({x}; {y})");
            }
        }
    }

    [Fact]
    public void Defaults_MatchSpecification()
    {
        _tool.Thickness.Value.ShouldBe(4);
        _tool.Thickness.Minimum.ShouldBe(1);
        _tool.DashLength.Value.ShouldBe(10);
        _tool.DashLength.Minimum.ShouldBe(1);
        _tool.GapLength.Value.ShouldBe(5);
        _tool.GapLength.Minimum.ShouldBe(0);
        _tool.Color.Value.ShouldBe(ColorRgba.Black);
        _tool.Opacity.Value.ShouldBe(100);
        _tool.Opacity.Minimum.ShouldBe(0);
        _tool.Category.ShouldBe(ToolCategory.Draw);
    }

    [Fact]
    public void InvalidInputs_AreClamped()
    {
        _tool.Thickness.Value = 0;
        _tool.DashLength.Value = -5;
        _tool.GapLength.Value = -1;
        _tool.Opacity.Value = 140;
        _tool.X1.Value = double.NaN;

        _tool.Thickness.Value.ShouldBe(1);
        _tool.DashLength.Value.ShouldBe(1);
        _tool.GapLength.Value.ShouldBe(0);
        _tool.Opacity.Value.ShouldBe(100);
        _tool.X1.Value.ShouldBe(0);
    }

    [Fact]
    public void Drag_CreatesEditWithPreviewOverlay_LayerUnchanged()
    {
        var original = Pixels.Clone();

        DragDash(0, 12, 30, 12);

        _tool.IsActiveOperation.ShouldBeTrue();
        _tool.StartPoint.ShouldBe(new PointD(0, 12));
        _tool.EndPoint.ShouldBe(new PointD(30, 12));
        Pixels.ContentEquals(original).ShouldBeTrue();
        History.UndoCount.ShouldBe(0);
        _tool.Overlay.OfType<OverlayRaster>().ShouldHaveSingleItem();
        _tool.Overlay.OfType<OverlayHandle>().Count().ShouldBe(2);
        _tool.Overlay.OfType<OverlayLine>().ShouldHaveSingleItem();
    }

    [Fact]
    public void Apply_DrawsExactDashes_10PxDash5PxGap_4PxThick()
    {
        DragDash(0, 12, 30, 12);
        _tool.Key(Context, ToolKey.Enter, ToolModifiers.None);

        // Mittellinie y = 12, Stärke 4 → Zeilen 10–13; Striche x 0–9 und 15–24.
        ShouldBeHorizontalDashes(10, 4);
        _tool.IsActiveOperation.ShouldBeFalse();
        _tool.Overlay.ShouldBeEmpty();
        History.UndoNameKey.ShouldBe("Tool.dashPattern");
    }

    [Fact]
    public void Apply_IsOneUndoStep_UndoRestoresExactly_RedoReappliesExactly()
    {
        var original = Pixels.Clone();
        DragDash(2, 3, 37, 26);
        _tool.ApplyAction.Execute(Context);
        var applied = Pixels.Clone();

        applied.ContentEquals(original).ShouldBeFalse();
        History.UndoCount.ShouldBe(1);
        History.Undo();
        Pixels.ContentEquals(original).ShouldBeTrue();
        History.Redo();
        Pixels.ContentEquals(applied).ShouldBeTrue();
    }

    [Fact]
    public void Cancel_DiscardsEditCompletely()
    {
        var original = Pixels.Clone();
        DragDash(0, 12, 30, 12);

        _tool.Key(Context, ToolKey.Escape, ToolModifiers.None).ShouldBeTrue();

        _tool.IsActiveOperation.ShouldBeFalse();
        _tool.Overlay.ShouldBeEmpty();
        Pixels.ContentEquals(original).ShouldBeTrue();
        History.UndoCount.ShouldBe(0);
    }

    [Fact]
    public void CancelAction_DiscardsEdit()
    {
        DragDash(0, 12, 30, 12);
        _tool.CancelAction.Execute(Context);

        _tool.IsActiveOperation.ShouldBeFalse();
        History.UndoCount.ShouldBe(0);
    }

    [Fact]
    public void Reset_RestoresDefaultParameters_KeepsPoints()
    {
        DragDash(0, 12, 30, 12);
        _tool.Thickness.Value = 9;
        _tool.DashLength.Value = 3;
        _tool.GapLength.Value = 0;
        _tool.Color.Value = new ColorRgba(10, 20, 30, 40);
        _tool.Opacity.Value = 25;
        _tool.AntialiasOption.Value = false;

        _tool.ResetAction.Execute(null);

        _tool.Thickness.Value.ShouldBe(4);
        _tool.DashLength.Value.ShouldBe(10);
        _tool.GapLength.Value.ShouldBe(5);
        _tool.Color.Value.ShouldBe(ColorRgba.Black);
        _tool.Opacity.Value.ShouldBe(100);
        _tool.AntialiasOption.Value.ShouldBeTrue();
        _tool.EndPoint.ShouldBe(new PointD(30, 12));
    }

    [Fact]
    public void DraggingEndHandle_MovesOnlyEndPoint_AndSyncsFields()
    {
        DragDash(5, 5, 25, 5);

        // Kontrollpunkt leicht daneben greifen (innerhalb des Fangradius) und ziehen.
        _tool.PointerDown(Context, Ev(25.5, 5.5));
        _tool.PointerMove(Context, Ev(25.5, 20.5));
        _tool.PointerUp(Context, Ev(25.5, 20.5));

        _tool.StartPoint.ShouldBe(new PointD(5, 5));
        _tool.EndPoint.ShouldBe(new PointD(25, 20));
        (_tool.X2.Value, _tool.Y2.Value).ShouldBe((25, 20));
        (_tool.X1.Value, _tool.Y1.Value).ShouldBe((5, 5));
        _tool.CurrentLine!.Length.ShouldBe(25, 1e-9);
    }

    [Fact]
    public void DraggingStartHandle_MovesOnlyStartPoint()
    {
        DragDash(5, 5, 25, 5);

        _tool.PointerDown(Context, Ev(5, 5));
        _tool.PointerUp(Context, Ev(10, 25));

        _tool.StartPoint.ShouldBe(new PointD(10, 25));
        _tool.EndPoint.ShouldBe(new PointD(25, 5));
    }

    [Fact]
    public void DraggingLine_MovesBothPoints()
    {
        DragDash(5, 5, 25, 5);

        _tool.PointerDown(Context, Ev(15, 5));
        _tool.PointerUp(Context, Ev(17, 9));

        _tool.StartPoint.ShouldBe(new PointD(7, 9));
        _tool.EndPoint.ShouldBe(new PointD(27, 9));
    }

    [Fact]
    public void Preview_FollowsControlPoints()
    {
        DragDash(0, 5, 30, 5);
        var before = _tool.Overlay.OfType<OverlayRaster>().Single();

        _tool.PointerDown(Context, Ev(30, 5));
        _tool.PointerMove(Context, Ev(30, 25));
        var during = _tool.Overlay.OfType<OverlayRaster>().Single();
        _tool.PointerUp(Context, Ev(30, 25));

        during.ShouldNotBeSameAs(before);
        during.Pixels.Height.ShouldBeGreaterThan(before.Pixels.Height);
    }

    [Fact]
    public void CoordinateFields_MoveThePoints()
    {
        DragDash(0, 5, 30, 5);

        _tool.X2.Value = 20;
        _tool.Y2.Value = 15;

        _tool.EndPoint.ShouldBe(new PointD(20, 15));
        _tool.Info.TextKey.ShouldBe("Opt.Dash.Info.Line");
    }

    [Fact]
    public void Fields_AndApplyCancel_AreOnlyEnabledWhileEditing()
    {
        _tool.X1.IsEnabled.ShouldBeFalse();
        _tool.ApplyAction.IsEnabled.ShouldBeFalse();
        _tool.CancelAction.IsEnabled.ShouldBeFalse();
        _tool.ResetAction.IsEnabled.ShouldBeTrue();
        _tool.Info.TextKey.ShouldBe("Opt.Dash.Info.Empty");

        DragDash(0, 5, 30, 5);
        _tool.X1.IsEnabled.ShouldBeTrue();
        _tool.ApplyAction.IsEnabled.ShouldBeTrue();
        _tool.CancelAction.IsEnabled.ShouldBeTrue();

        _tool.Commit(Context);
        _tool.Y2.IsEnabled.ShouldBeFalse();
        _tool.ApplyAction.IsEnabled.ShouldBeFalse();
    }

    [Fact]
    public void Info_ShowsLengthAngleDirectionMidpointAndCount()
    {
        DragDash(0, 20, 30, -10);

        _tool.Info.TextKey.ShouldBe("Opt.Dash.Info.Line");
        var args = _tool.Info.Arguments;
        ((double)args[0]).ShouldBe(Math.Sqrt(1800), 1e-9);  // Länge
        ((double)args[1]).ShouldBe(45, 1e-9);               // Winkel
        ((double)args[2]).ShouldBe(Math.Sqrt(0.5), 1e-9);   // Richtung x
        ((double)args[3]).ShouldBe(-Math.Sqrt(0.5), 1e-9);  // Richtung y
        ((double)args[4]).ShouldBe(15, 1e-9);               // Mitte x
        ((double)args[5]).ShouldBe(5, 1e-9);                // Mitte y
        ((long)args[6]).ShouldBe(3);                        // 42,4 px → Striche bei 0, 15, 30
    }

    [Fact]
    public void Thickness_GapAndDashLength_Take_Effect()
    {
        DragDash(0, 12, 30, 12);
        _tool.Thickness.Value = 2;
        _tool.DashLength.Value = 4;
        _tool.GapLength.Value = 2;
        _tool.Commit(Context);

        // Zeilen 11–12, Striche [0,4), [6,10), [12,16) …
        for (var x = 0; x < 30; x++)
        {
            var expected = x % 6 < 4;
            IsBlack(x, 11).ShouldBe(expected, $"x = {x}");
            IsBlack(x, 12).ShouldBe(expected, $"x = {x}");
        }

        IsBlack(1, 10).ShouldBeFalse();
        IsBlack(1, 13).ShouldBeFalse();
    }

    [Fact]
    public void GapZero_DrawsSolidLineWithoutSeams()
    {
        DragDash(0, 12, 30, 12);
        _tool.GapLength.Value = 0;
        _tool.Commit(Context);

        for (var x = 0; x < 30; x++)
        {
            IsBlack(x, 11).ShouldBeTrue($"x = {x}");
        }

        IsBlack(30, 11).ShouldBeFalse();
    }

    [Fact]
    public void Vertical_Line_DrawsVerticalDashes()
    {
        DragDash(20, 0, 20, 30);
        _tool.Commit(Context);

        for (var y = 0; y < 30; y++)
        {
            var expected = y is < 10 or >= 15 and < 25;
            IsBlack(18, y).ShouldBe(expected, $"y = {y}");
            IsBlack(21, y).ShouldBe(expected, $"y = {y}");
            IsBlack(17, y).ShouldBeFalse();
            IsBlack(22, y).ShouldBeFalse();
        }
    }

    [Fact]
    public void Diagonal_Line_HasDashesAndGapsAlongTheLine()
    {
        DragDash(2, 2, 32, 32);
        _tool.Commit(Context);

        var dir = 1 / Math.Sqrt(2);
        PointD At(double distance) => new(2 + distance * dir, 2 + distance * dir);

        // Mitte des ersten Strichs (5 px) deckend, Mitte der ersten Lücke (12,5 px) unverändert.
        var dash = At(5);
        var gap = At(12.5);
        IsBlack((int)dash.X, (int)dash.Y).ShouldBeTrue();
        Pixels.GetPixel((int)gap.X, (int)gap.Y).ShouldBe(Bgra32.White);
    }

    [Fact]
    public void Opacity_And_ColorAlpha_Take_Effect()
    {
        DragDash(0, 12, 30, 12);
        _tool.Opacity.Value = 50;
        _tool.Commit(Context);
        // Schwarz mit 50 % über Weiß: 127,5 → 128
        Pixels.GetPixel(5, 11).ShouldBe(new Bgra32(128, 128, 128, 255));

        DragDash(0, 22, 30, 22);
        _tool.Opacity.Value = 100;
        _tool.Color.Value = new ColorRgba(255, 0, 0, 128);
        _tool.Commit(Context);
        // Rot (Alpha 128) über Weiß: R = 128 + 255 · 127/255 = 255, G = B = 127
        Pixels.GetPixel(5, 21).ShouldBe(new Bgra32(127, 127, 255, 255));
    }

    [Fact]
    public void Apply_IsClippedToSelection()
    {
        var selection = SelectionMask.CreateEmpty(40, 30);
        selection.Fill(new IntRect(0, 0, 20, 30), 255);
        Document.SetSelection(selection);

        DragDash(0, 12, 30, 12);
        _tool.Commit(Context);

        IsBlack(5, 11).ShouldBeTrue();
        IsBlack(16, 11).ShouldBeTrue();
        IsBlack(21, 11).ShouldBeFalse(); // Strich 15–24 außerhalb der Auswahl abgeschnitten
    }

    [Fact]
    public void Apply_IsClippedToImageBounds_PointsMayLieOutside()
    {
        _tool.SetPoints(Context, new PointD(-50, 12), new PointD(100, 12));
        _tool.Commit(Context);

        // Start bei −50: Striche bei −50, −35, −20, −5 (bis 5), 10 (bis 20), 25 (bis 35) …
        IsBlack(0, 11).ShouldBeTrue();
        IsBlack(4, 11).ShouldBeTrue();
        IsBlack(5, 11).ShouldBeFalse();
        IsBlack(10, 11).ShouldBeTrue();
        IsBlack(39, 11).ShouldBeFalse();
    }

    [Fact]
    public void Preview_EqualsAppliedResult_OnTransparentLayer()
    {
        var transparent = new TransparentFixture();
        var tool = transparent.Tool;
        tool.Thickness.Value = 3.5;
        tool.Color.Value = new ColorRgba(200, 40, 90, 180);
        tool.Opacity.Value = 70;
        tool.SetPoints(transparent.NoViewContext, new PointD(3.3, 4.1), new PointD(36.2, 25.7));
        var preview = tool.RenderPreview()!;
        tool.Commit(transparent.NoViewContext);

        preview.Scale.ShouldBe(1);
        var layer = transparent.Doc.PixelLayers.First().Pixels;
        for (var y = 0; y < layer.Height; y++)
        {
            for (var x = 0; x < layer.Width; x++)
            {
                var px = x - (int)preview.Origin.X;
                var py = y - (int)preview.Origin.Y;
                var expected = px >= 0 && py >= 0 && px < preview.Pixels.Width && py < preview.Pixels.Height
                    ? preview.Pixels.GetPixel(px, py)
                    : Bgra32.Transparent;
                layer.GetPixel(x, y).ShouldBe(expected, $"({x}; {y})");
            }
        }
    }

    [Fact]
    public void Preview_UsesReducedResolution_WhenZoomedOut()
    {
        View.SetZoom(0.5);
        DragDash(0, 12, 30, 12);

        var preview = _tool.Overlay.OfType<OverlayRaster>().Single();
        preview.Scale.ShouldBe(0.5);
        preview.Pixels.Width.ShouldBeLessThanOrEqualTo(20);
    }

    [Fact]
    public void HitRadius_IsInScreenPixels_IndependentOfZoom()
    {
        View.SetZoom(4);
        DragDash(5, 5, 25, 5);

        // 1 Dokumentpixel = 4 Bildschirmpixel neben dem Endpunkt: trifft (Radius 8 Bildschirmpixel).
        _tool.PointerDown(Context, Ev(26, 5));
        _tool.PointerUp(Context, Ev(26, 15));
        _tool.EndPoint.ShouldBe(new PointD(25, 15));

        // 4 Dokumentpixel = 16 Bildschirmpixel daneben (nicht auf der Linie): kein Treffer → übernimmt, neue Linie.
        _tool.PointerDown(Context, Ev(25, 19));
        _tool.PointerUp(Context, Ev(35, 19));
        History.UndoCount.ShouldBe(1);
        _tool.StartPoint.ShouldBe(new PointD(25, 19));
    }

    [Fact]
    public void ClickWithoutDrag_CreatesNothing()
    {
        _tool.PointerDown(Context, Ev(10, 10));
        _tool.PointerUp(Context, Ev(10, 10));

        _tool.IsActiveOperation.ShouldBeFalse();
        _tool.Overlay.ShouldBeEmpty();
    }

    [Fact]
    public void PointerLost_KeepsEditAndEndsDrag()
    {
        _tool.PointerDown(Context, Ev(0, 5));
        _tool.PointerMove(Context, Ev(20, 5));
        _tool.PointerLost(Context);
        _tool.PointerMove(Context, Ev(30, 30));

        _tool.IsActiveOperation.ShouldBeTrue();
        _tool.EndPoint.ShouldBe(new PointD(20, 5));
        History.UndoCount.ShouldBe(0);
    }

    [Fact]
    public void ArrowKeys_MoveBothPoints()
    {
        DragDash(5, 5, 25, 5);

        _tool.Key(Context, ToolKey.Right, ToolModifiers.None).ShouldBeTrue();
        _tool.Key(Context, ToolKey.Down, ToolModifiers.Shift).ShouldBeTrue();

        _tool.StartPoint.ShouldBe(new PointD(6, 15));
        _tool.EndPoint.ShouldBe(new PointD(26, 15));
    }

    [Fact]
    public void Shift_SnapsAngleTo15Degrees()
    {
        DragDash(0, 0, 20, 1, ToolModifiers.Shift);

        _tool.CurrentLine!.AngleDegrees.ShouldBe(0, 1e-9);
    }

    [Fact]
    public void LockedLayer_ReportsProblem_AndStartsNoEdit()
    {
        Layer.IsLocked = true;
        DragDash(0, 5, 30, 5);

        Problems.ShouldContain(ToolProblem.LayerLocked);
        _tool.IsActiveOperation.ShouldBeFalse();
    }

    [Fact]
    public void EditMaskMode_AppliesPatternToMask_NotPixels_AndIsUndoable()
    {
        Layer.Mask = Domain.Layers.LayerMask.CreateRevealAll(40, 30);
        Context = new ToolContext(Document, History, Colors, View) { EditMask = true };
        var pixels = Pixels.Clone();

        DragDash(0, 12, 30, 12);
        _tool.Commit(Context);

        Layer.Mask.Get(5, 11).ShouldBe((byte)0);    // Strich: Schwarz → Maskenwert 0
        Layer.Mask.Get(12, 11).ShouldBe((byte)255); // Lücke
        Pixels.ContentEquals(pixels).ShouldBeTrue();
        History.Undo();
        Layer.Mask.Get(5, 11).ShouldBe((byte)255);
    }

    [Fact]
    public void ApplyWithIdenticalPoints_ChangesNothing()
    {
        DragDash(0, 5, 30, 5);
        _tool.X2.Value = 0;
        _tool.Y2.Value = 5;
        _tool.Commit(Context);

        History.UndoCount.ShouldBe(0);
    }

    /// <summary>Transparentes Dokument ohne Ansicht (Vorschau in voller Auflösung, ganzer Bereich).</summary>
    private sealed class TransparentFixture : ToolTestBase
    {
        public TransparentFixture()
            : base(40, 30, Domain.Documents.DocumentBackground.Transparent)
        {
            NoViewContext = new ToolContext(Document, History, Colors, view: null);
        }

        public DashPatternTool Tool { get; } = new();

        public ToolContext NoViewContext { get; }

        public Domain.Documents.ImageDocument Doc => Document;
    }
}
