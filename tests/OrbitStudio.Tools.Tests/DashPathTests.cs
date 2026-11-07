using OrbitStudio.Domain.Documents;
using OrbitStudio.Domain.Imaging;
using OrbitStudio.Tools.Drawing;

namespace OrbitStudio.Tools.Tests;

/// <summary>Gebogene Strichmusterlinien: Geometrie über Kontrollpunkte, Bogenlänge und Striche.</summary>
public class DashPathGeometryTests
{
    private static readonly PointD[] Arc = [new(0, 0), new(50, 40), new(100, 0)];

    [Fact]
    public void TwoControls_IsOneStraightPiece_WithChordLength()
    {
        var path = new DashPath([new PointD(0, 0), new PointD(30, 40)], 10, 5);

        path.Pieces.Count.ShouldBe(1);
        path.Length.ShouldBe(50, 1e-12);
        path.Count.ShouldBe(4); // 0, 15, 30, 45
    }

    [Fact]
    public void CurveThroughControls_PassesExactlyThroughEveryControl()
    {
        var path = new DashPath(Arc, 10, 5);
        var vertices = path.Pieces.Select(p => p.A).Append(path.Pieces[^1].B).ToList();

        foreach (var control in Arc)
        {
            vertices.Any(v => (v - control).Length < 1e-9).ShouldBeTrue($"Kontrollpunkt {control} liegt nicht auf der Kurve");
        }
    }

    [Fact]
    public void CurveLength_LiesBetweenChordAndControlPolygon()
    {
        var path = new DashPath(Arc, 10, 5);
        var chord = (Arc[^1] - Arc[0]).Length;
        var polygon = Enumerable.Range(0, Arc.Length - 1).Sum(i => (Arc[i + 1] - Arc[i]).Length);

        path.Length.ShouldBeGreaterThan(chord);
        path.Length.ShouldBeLessThan(polygon * 1.1); // der Spline schwingt an den Enden leicht über
    }

    [Fact]
    public void CollinearControls_GiveTheExactStraightLength()
    {
        var path = new DashPath([new PointD(0, 0), new PointD(10, 0), new PointD(40, 0), new PointD(100, 0)], 10, 5);

        path.Length.ShouldBe(100, 1e-6);
        path.PointAt(37).X.ShouldBe(37, 1e-6);
        path.PointAt(37).Y.ShouldBe(0, 1e-9);
    }

    [Fact]
    public void PointAt_StartsAndEndsAtFirstAndLastControl()
    {
        var path = new DashPath(Arc, 10, 5);

        path.PointAt(0).ShouldBe(Arc[0]);
        (path.PointAt(path.Length) - Arc[^1]).Length.ShouldBeLessThan(1e-9);
        (path.PointAt(path.Length + 100) - Arc[^1]).Length.ShouldBeLessThan(1e-9);
        (path.PointAt(-5) - Arc[0]).Length.ShouldBeLessThan(1e-9);
    }

    [Fact]
    public void PointAt_IsMonotoneAlongTheCurve_AndAtArcDistance()
    {
        var path = new DashPath(Arc, 10, 5);
        var previous = path.PointAt(0);
        for (var s = 1.0; s <= path.Length; s += 1)
        {
            var current = path.PointAt(s);
            // Abstand zwischen zwei Abtastpunkten entspricht höchstens der Bogenlänge dazwischen.
            (current - previous).Length.ShouldBeLessThanOrEqualTo(1.0 + 1e-6);
            previous = current;
        }
    }

    [Fact]
    public void Dashes_AreMeasuredAlongTheCurve_AndEachFitsDashLength()
    {
        var path = new DashPath(Arc, 10, 5);

        var intervals = Enumerable.Range(0, (int)path.Count).Select(i => path.Interval(i)).ToList();
        intervals.ShouldAllBe(iv => iv.To - iv.From <= 10 + 1e-9 && iv.To > iv.From);
        intervals[^1].From.ShouldBeLessThan(path.Length);
        intervals[^1].To.ShouldBeLessThanOrEqualTo(path.Length + 1e-9);
        intervals.Zip(intervals.Skip(1)).ShouldAllBe(pair => pair.Second.From - pair.First.From == 15);
    }

    [Fact]
    public void IdenticalControls_GiveNoPiecesAndNoDashes()
    {
        var path = new DashPath([new PointD(7, 3), new PointD(7, 3)], 10, 5);

        path.Pieces.ShouldBeEmpty();
        path.Length.ShouldBe(0);
        path.Count.ShouldBe(0);
    }

    [Fact]
    public void Bounds_ContainTheCurveWithMargin()
    {
        var path = new DashPath(Arc, 10, 5);
        var bounds = path.Bounds(2);

        foreach (var piece in path.Pieces)
        {
            bounds.Contains((int)Math.Floor(piece.A.X), (int)Math.Floor(piece.A.Y)).ShouldBeTrue();
        }

        bounds.X.ShouldBeLessThanOrEqualTo(-2);
        bounds.Right.ShouldBeGreaterThanOrEqualTo(102);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(17)]
    public void ControlCount_OutsideRange_IsRejected(int count) =>
        Should.Throw<ArgumentException>(() => new DashPath(Enumerable.Range(0, count).Select(i => new PointD(i, 0)).ToList(), 10, 5));

    [Fact]
    public void NonFiniteControl_IsRejected() =>
        Should.Throw<ArgumentException>(() => new DashPath([new PointD(0, 0), new PointD(double.NaN, 3)], 10, 5));

    [Fact]
    public void InvalidDashParameters_AreRejected()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new DashPath(Arc, 0, 5));
        Should.Throw<ArgumentOutOfRangeException>(() => new DashPath(Arc, 10, -1));
    }
}

/// <summary>Werkzeug: Punkte einfügen, ziehen und entfernen; Vorschau = Ergebnis bei gebogenen Linien.</summary>
public class DashPathToolTests : ToolTestBase
{
    private readonly DashPatternTool _tool = new();

    public DashPathToolTests()
        : base(60, 50, DocumentBackground.Transparent)
    {
    }

    private ToolPointerEvent Ev(double x, double y, PointerButton button = PointerButton.Primary)
    {
        var p = new PointD(x, y);
        return new ToolPointerEvent(p, View.DocumentToViewport(p), button, ToolModifiers.None);
    }

    private void Click(double x, double y, PointerButton button = PointerButton.Primary)
    {
        _tool.PointerDown(Context, Ev(x, y, button));
        _tool.PointerUp(Context, Ev(x, y, button));
    }

    private void Drag(double x0, double y0, double x1, double y1)
    {
        _tool.PointerDown(Context, Ev(x0, y0));
        for (var i = 1; i <= 4; i++)
        {
            _tool.PointerMove(Context, Ev(x0 + (x1 - x0) * i / 4, y0 + (y1 - y0) * i / 4));
        }

        _tool.PointerUp(Context, Ev(x1, y1));
    }

    private void DrawStraight()
    {
        _tool.Thickness.Value = 3;
        _tool.DashLength.Value = 1000;
        _tool.GapLength.Value = 0;
        Drag(10, 30, 50, 30);
    }

    [Fact]
    public void ClickOnLine_InsertsControlPoint_AtThatSegment()
    {
        DrawStraight();

        Click(30, 30); // auf der Geraden, weit weg von Anfang und Ende

        _tool.ControlPoints!.Count.ShouldBe(3);
        _tool.ControlPoints[1].X.ShouldBe(30, 1e-9);
    }

    [Fact]
    public void DraggingInnerControl_BendsTheLine()
    {
        DrawStraight();
        Click(30, 30);

        Drag(30, 30, 30, 12);

        _tool.ControlPoints![1].ShouldBe(new PointD(30, 12));
        _tool.CurrentPath!.Pieces.Min(p => p.A.Y).ShouldBeLessThan(25); // Kurve reicht nach oben
        _tool.StartPoint!.Value.ShouldBe(new PointD(10, 30));
        _tool.EndPoint!.Value.ShouldBe(new PointD(50, 30));
    }

    [Fact]
    public void RightClickOnInnerControl_RemovesIt_EndsStay()
    {
        DrawStraight();
        Click(30, 30);
        _tool.ControlPoints!.Count.ShouldBe(3);

        Click(30, 30, PointerButton.Secondary);

        _tool.ControlPoints.Count.ShouldBe(2);
        _tool.StartPoint!.Value.ShouldBe(new PointD(10, 30));
        _tool.EndPoint!.Value.ShouldBe(new PointD(50, 30));
    }

    [Fact]
    public void RightClickOnEndpoint_DoesNotRemoveIt()
    {
        DrawStraight();

        Click(10, 30, PointerButton.Secondary);

        _tool.ControlPoints!.Count.ShouldBe(2);
    }

    [Fact]
    public void DraggingTheLine_MovesAllControls()
    {
        DrawStraight();
        Click(30, 30);

        Drag(20, 30, 22, 35); // auf der Linie greifen, zwischen Anfang und eingefügtem Punkt

        _tool.ControlPoints![0].ShouldBe(new PointD(12, 35));
        _tool.ControlPoints[1].ShouldBe(new PointD(32, 35));
        _tool.ControlPoints[2].ShouldBe(new PointD(52, 35));
    }

    [Fact]
    public void CurvedLine_CenterControlPixelsAreDarkAndFarAwayStayTransparent()
    {
        _tool.Thickness.Value = 4;
        _tool.DashLength.Value = 1000;
        _tool.GapLength.Value = 0;
        Drag(10, 40, 50, 40);
        Click(30, 40);
        Drag(30, 40, 30, 15); // Mitte nach oben biegen

        _tool.Commit(Context);

        Layer.Pixels.GetPixel(30, 15).A.ShouldBe((byte)255);   // Kontrollpunkt liegt auf der Kurve
        Layer.Pixels.GetPixel(30, 40).A.ShouldBe((byte)0);      // ursprüngliche Gerade ist nicht mehr da
        Layer.Pixels.GetPixel(30, 5).A.ShouldBe((byte)0);
    }

    [Fact]
    public void Preview_EqualsAppliedResult_ForCurvedDashedLine()
    {
        _tool.Thickness.Value = 3.5;
        _tool.DashLength.Value = 9;
        _tool.GapLength.Value = 4;
        _tool.Color.Value = new ColorRgba(200, 40, 90, 180);
        _tool.Opacity.Value = 70;
        _tool.SetPoints(Context, new PointD(5.3, 40.1), new PointD(52.2, 38.7));
        Click(28, 38);
        Drag(28, 38, 30, 20);

        var preview = _tool.RenderPreview()!;
        _tool.Commit(Context);

        preview.Scale.ShouldBe(1);
        for (var y = 0; y < preview.Pixels.Height; y++)
        {
            for (var x = 0; x < preview.Pixels.Width; x++)
            {
                var px = preview.Origin.X + x;
                var py = preview.Origin.Y + y;
                Layer.Pixels.GetPixel((int)px, (int)py).ShouldBe(preview.Pixels.GetPixel(x, y), $"({px}; {py})");
            }
        }
    }

    [Fact]
    public void Info_ShowsStartToEndChord_AndCountOfCurveDashes()
    {
        _tool.DashLength.Value = 10;
        _tool.GapLength.Value = 5;
        DrawStraight();
        Click(30, 30);
        Drag(30, 30, 30, 10);

        var args = _tool.Info.Arguments;
        ((double)args[0]).ShouldBe(_tool.CurrentPath!.Length, 1e-9);
        ((long)args[6]).ShouldBe(_tool.CurrentPath.Count);
        ((double)args[1]).ShouldBe(0, 1e-9); // Winkel der Verbindung Anfang → Ende
    }

    [Fact]
    public void Commit_IsOneUndoStep()
    {
        DrawStraight();
        Click(30, 30);
        Drag(30, 30, 30, 15);

        _tool.Commit(Context);

        History.UndoCount.ShouldBe(1);
    }
}
