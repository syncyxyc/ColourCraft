using OrbitStudio.Domain.Imaging;

namespace OrbitStudio.Effects.Tests;

/// <summary>Tonwertkurve (Modell des Kurveneditors) und Farbkurven-Effekt.</summary>
public class CurveEffectTests : EffectTestBase
{
    private static ToneCurve Curve(params (int X, int Y)[] points) =>
        ToneCurve.Create(points.Select(p => new CurvePoint(p.X, p.Y)));

    // --- Kurvenmodell ---------------------------------------------------------------------------

    [Fact]
    public void Identity_MapsEveryValueToItself()
    {
        var lut = ToneCurve.Identity.ToLookupTable();
        for (var i = 0; i < 256; i++)
        {
            lut[i].ShouldBe((byte)i);
        }

        ToneCurve.Identity.IsIdentity.ShouldBeTrue();
    }

    [Fact]
    public void TwoPointLine_IsExactlyLinear()
    {
        // (0,255) → (255,0): 255 − x
        var lut = Curve((0, 255), (255, 0)).ToLookupTable();
        for (var i = 0; i < 256; i++)
        {
            lut[i].ShouldBe((byte)(255 - i));
        }
    }

    [Fact]
    public void Curve_PassesExactlyThroughAllControlPoints()
    {
        var curve = Curve((0, 10), (64, 40), (128, 200), (200, 210), (255, 250));
        foreach (var p in curve.Points)
        {
            curve.ToLookupTable()[p.X].ShouldBe((byte)p.Y);
        }
    }

    [Fact]
    public void MonotoneData_GivesMonotoneLookup_WithoutOvershoot()
    {
        // Steiler Sprung zwischen zwei flachen Abschnitten: ein normaler kubischer Spline würde überschwingen.
        var curve = Curve((0, 0), (100, 10), (110, 240), (255, 250));
        var lut = curve.ToLookupTable();
        for (var i = 1; i < 256; i++)
        {
            lut[i].ShouldBeGreaterThanOrEqualTo(lut[i - 1], $"bei {i}");
        }

        for (var i = 0; i <= 100; i++)
        {
            lut[i].ShouldBeLessThanOrEqualTo((byte)10);
        }

        for (var i = 110; i < 256; i++)
        {
            lut[i].ShouldBeGreaterThanOrEqualTo((byte)240);
        }
    }

    [Fact]
    public void OutsideTheEndPoints_CurveIsHorizontal()
    {
        var lut = Curve((50, 100), (200, 150)).ToLookupTable();
        lut[0].ShouldBe((byte)100);
        lut[50].ShouldBe((byte)100);
        lut[200].ShouldBe((byte)150);
        lut[255].ShouldBe((byte)150);
    }

    [Fact]
    public void Evaluate_ClampsBetween0And255()
    {
        var curve = Curve((0, 0), (10, 255), (20, 0), (255, 255));
        for (var x = 0.0; x <= 255; x += 0.5)
        {
            curve.Evaluate(x).ShouldBeInRange(0, 255);
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(17)]
    public void Create_RejectsWrongPointCount(int count)
    {
        var points = Enumerable.Range(0, count).Select(i => new CurvePoint(i * 10, i));
        Should.Throw<ArgumentException>(() => ToneCurve.Create(points));
    }

    [Fact]
    public void Create_RejectsUnorderedDuplicateAndOutOfRangePoints()
    {
        Should.Throw<ArgumentException>(() => Curve((0, 0), (100, 5), (50, 9), (255, 255)));
        Should.Throw<ArgumentException>(() => Curve((0, 0), (100, 5), (100, 9), (255, 255)));
        Should.Throw<ArgumentException>(() => Curve((0, 0), (256, 255)));
        Should.Throw<ArgumentException>(() => Curve((-1, 0), (255, 255)));
        Should.Throw<ArgumentException>(() => Curve((0, 0), (255, 300)));
    }

    [Fact]
    public void AddPoint_InsertsSorted_AndMovesExistingPointAtSameX()
    {
        var curve = ToneCurve.Identity.AddPoint(128, 200);
        curve.Points.Select(p => (p.X, p.Y)).ShouldBe([(0, 0), (128, 200), (255, 255)]);

        var moved = curve.AddPoint(128, 50);
        moved.Points.Count.ShouldBe(3);
        moved.Points[1].ShouldBe(new CurvePoint(128, 50));
    }

    [Fact]
    public void AddPoint_AtMaximum_ChangesNothing()
    {
        var curve = ToneCurve.Identity;
        for (var i = 1; i < ToneCurve.MaximumPoints - 1; i++)
        {
            curve = curve.AddPoint(i * 15, i * 15);
        }

        curve.Points.Count.ShouldBe(ToneCurve.MaximumPoints);
        curve.AddPoint(250, 10).ShouldBeSameAs(curve);
    }

    [Fact]
    public void MovePoint_KeepsOrder_AndClampsToNeighbours()
    {
        var curve = Curve((0, 0), (100, 100), (200, 200), (255, 255));
        // X wird zwischen die Nachbarn begrenzt (1 Abstand), Y auf 0–255.
        var moved = curve.MovePoint(1, 500, -20);
        moved.Points[1].ShouldBe(new CurvePoint(199, 0));
        var movedLeft = curve.MovePoint(2, -10, 300);
        movedLeft.Points[2].ShouldBe(new CurvePoint(101, 255));
        // Endpunkte bleiben im Bereich 0–255.
        curve.MovePoint(0, -5, 10).Points[0].ShouldBe(new CurvePoint(0, 10));
        curve.MovePoint(3, 999, 10).Points[3].ShouldBe(new CurvePoint(255, 10));
        ToneCurve.Validate(moved.Points).ShouldBeNull();
    }

    [Fact]
    public void RemovePoint_KeepsAtLeastTwoPoints()
    {
        var curve = Curve((0, 0), (100, 100), (255, 255));
        var two = curve.RemovePoint(1);
        two.Points.Count.ShouldBe(2);
        two.RemovePoint(0).ShouldBeSameAs(two);
        Should.Throw<ArgumentOutOfRangeException>(() => curve.RemovePoint(5));
    }

    [Fact]
    public void HitTest_FindsNearestPointWithinTolerance()
    {
        var curve = Curve((0, 0), (100, 100), (110, 110), (255, 255));
        curve.HitTest(104, 104, 10).ShouldBe(1);
        curve.HitTest(108, 108, 10).ShouldBe(2);
        curve.HitTest(50, 200, 10).ShouldBe(-1);
    }

    [Fact]
    public void Curves_AreImmutable_AndComparedByValue()
    {
        var a = Curve((0, 0), (128, 100), (255, 255));
        var b = Curve((0, 0), (128, 100), (255, 255));
        a.ShouldBe(b);
        a.GetHashCode().ShouldBe(b.GetHashCode());
        a.MovePoint(1, 128, 120).ShouldNotBe(a);
        a.Points[1].ShouldBe(new CurvePoint(128, 100));
    }

    // --- Effekt ---------------------------------------------------------------------------------

    [Fact]
    public void Defaults_ChangeNothing()
    {
        var source = RandomImage(8, 8, seed: 1);
        Run(new CurvesEffect(), source).ContentEquals(source).ShouldBeTrue();
    }

    [Fact]
    public void MasterInvertCurve_EqualsInvertEffect()
    {
        var source = RandomImage(16, 16, seed: 2);
        var curved = Run(new CurvesEffect(), source, ("master", Curve((0, 255), (255, 0))));
        curved.ContentEquals(Run(new InvertEffect(), source)).ShouldBeTrue();
    }

    [Fact]
    public void ChannelCurve_ChangesOnlyThatChannel()
    {
        var pixel = Image(1, 1, Rgb(100, 150, 200));
        var result = Run(new CurvesEffect(), pixel, ("red", Curve((0, 255), (255, 0)))).GetPixel(0, 0);
        result.ShouldBe(Rgb(155, 150, 200));
        Run(new CurvesEffect(), pixel, ("green", Curve((0, 0), (255, 0)))).GetPixel(0, 0).ShouldBe(Rgb(100, 0, 200));
        Run(new CurvesEffect(), pixel, ("blue", Curve((0, 255), (255, 255)))).GetPixel(0, 0).ShouldBe(Rgb(100, 150, 255));
    }

    [Fact]
    public void MasterIsAppliedBeforeChannelCurve()
    {
        // Gesamt: x → 255 − x (100 → 155), danach Rot: 0..255 → 0..127 (Gerade, 155 → 77,5 → 78)
        var pixel = Image(1, 1, Gray(100));
        var result = Run(new CurvesEffect(), pixel,
            ("master", Curve((0, 255), (255, 0))),
            ("red", Curve((0, 0), (255, 127)))).GetPixel(0, 0);
        result.R.ShouldBe((byte)PixelMath.ToByte(155 * 127.0 / 255));
        result.G.ShouldBe((byte)155);
        result.B.ShouldBe((byte)155);
    }

    [Fact]
    public void Alpha_IsPreserved_AndTransparentStaysTransparent()
    {
        var source = PixelBuffer.Create(2, 1);
        source.SetPixel(0, 0, Bgra32.FromStraight(200, 100, 50, 128));
        source.SetPixel(1, 0, Bgra32.Transparent);
        var result = Run(new CurvesEffect(), source, ("master", Curve((0, 255), (255, 0))));
        result.GetPixel(0, 0).A.ShouldBe((byte)128);
        result.GetPixel(1, 0).ShouldBe(Bgra32.Transparent);
    }

    [Fact]
    public void Parameter_ValidatesType_AndIsNotRegisteredInDefaultSet()
    {
        CurvesEffect.Master.Validate(ToneCurve.Identity).ShouldBeNull();
        CurvesEffect.Master.Validate(3.0).ShouldBe("Effect.Error.Type");
        CurvesEffect.Master.DefaultValue.ShouldBe(ToneCurve.Identity);
        // Bis der Kurveneditor (WPF) existiert, steht der Effekt nicht im Menü (siehe bekannte-einschraenkungen.md).
        EffectRegistry.CreateDefault().Find("curves").ShouldBeNull();
    }
}
