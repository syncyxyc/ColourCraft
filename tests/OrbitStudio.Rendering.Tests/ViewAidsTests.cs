using OrbitStudio.Domain.Imaging;

namespace OrbitStudio.Rendering.Tests;

public class ViewAidsTests
{
    // --- Hilfslinien ----------------------------------------------------------------------------

    [Fact]
    public void Add_RejectsDuplicatesAndNonFinite_AndRespectsMaximum()
    {
        var guides = GuideSet.Empty.Add(GuideOrientation.Vertical, 10).Add(GuideOrientation.Horizontal, 10);
        guides.Guides.Count.ShouldBe(2); // gleiche Zahl, andere Richtung = andere Linie
        guides.Add(GuideOrientation.Vertical, 10).ShouldBeSameAs(guides);
        Should.Throw<ArgumentOutOfRangeException>(() => guides.Add(GuideOrientation.Vertical, double.NaN));
        Should.Throw<ArgumentOutOfRangeException>(() => guides.Add(GuideOrientation.Vertical, double.PositiveInfinity));

        var full = GuideSet.Empty;
        for (var i = 0; i < GuideSet.MaximumGuides; i++)
        {
            full = full.Add(GuideOrientation.Vertical, i);
        }

        full.Add(GuideOrientation.Vertical, 5000).ShouldBeSameAs(full);
    }

    [Fact]
    public void Move_Remove_Clear()
    {
        var guides = GuideSet.Empty.Add(GuideOrientation.Vertical, 10).Add(GuideOrientation.Vertical, 50);
        guides.Move(0, 20).Guides[0].Position.ShouldBe(20);
        guides.Move(0, 50).Guides.Count.ShouldBe(1); // würde mit der zweiten zusammenfallen
        guides.RemoveAt(0).Guides.Single().Position.ShouldBe(50);
        guides.Clear().Guides.ShouldBeEmpty();
        GuideSet.Empty.Clear().ShouldBeSameAs(GuideSet.Empty);
        Should.Throw<ArgumentOutOfRangeException>(() => guides.RemoveAt(2));
        Should.Throw<ArgumentOutOfRangeException>(() => guides.Move(-1, 1));
    }

    [Fact]
    public void HitTest_FindsNearestWithinTolerance()
    {
        var guides = GuideSet.Empty.Add(GuideOrientation.Vertical, 100).Add(GuideOrientation.Vertical, 104)
            .Add(GuideOrientation.Horizontal, 40);
        guides.HitTest(new PointD(103, 0), 2).ShouldBe(1);
        guides.HitTest(new PointD(101, 999), 2).ShouldBe(0);
        guides.HitTest(new PointD(0, 41), 2).ShouldBe(2);
        guides.HitTest(new PointD(50, 90), 2).ShouldBe(-1);
    }

    [Fact]
    public void Snap_SnapsEachAxisIndependently()
    {
        var guides = GuideSet.Empty.Add(GuideOrientation.Vertical, 100).Add(GuideOrientation.Horizontal, 40);
        var both = guides.Snap(new PointD(101.5, 38), 3);
        both.Point.ShouldBe(new PointD(100, 40));
        both.SnappedX.ShouldBeTrue();
        both.SnappedY.ShouldBeTrue();

        var onlyY = guides.Snap(new PointD(150, 41), 3);
        onlyY.Point.ShouldBe(new PointD(150, 40));
        onlyY.SnappedX.ShouldBeFalse();

        var none = guides.Snap(new PointD(150, 150), 3);
        none.Point.ShouldBe(new PointD(150, 150));
        none.SnappedX.ShouldBeFalse();
        none.SnappedY.ShouldBeFalse();
    }

    [Fact]
    public void Snap_PicksNearestOfSeveralGuides()
    {
        var guides = GuideSet.Empty.Add(GuideOrientation.Vertical, 10).Add(GuideOrientation.Vertical, 14);
        guides.Snap(new PointD(12.5, 0), 5).Point.X.ShouldBe(14);
        guides.Snap(new PointD(11, 0), 5).Point.X.ShouldBe(10);
    }

    [Fact]
    public void GuideSets_AreComparedByValue()
    {
        var a = GuideSet.Empty.Add(GuideOrientation.Vertical, 1);
        a.ShouldBe(GuideSet.Empty.Add(GuideOrientation.Vertical, 1));
        a.ShouldNotBe(GuideSet.Empty);
    }

    // --- Raster ---------------------------------------------------------------------------------

    [Theory]
    [InlineData(0, 0)]
    [InlineData(7.9, 0)]
    [InlineData(8, 16)] // Mitte rundet auf
    [InlineData(23.9, 16)]
    [InlineData(24, 32)]
    [InlineData(-7.9, 0)]
    [InlineData(-8, 0)]
    [InlineData(-8.1, -16)]
    public void Grid_SnapsToNearestPoint(double input, double expected)
    {
        new GridSettings(16, true).Snap(new PointD(input, input)).ShouldBe(new PointD(expected, expected));
    }

    [Fact]
    public void Grid_NormalizesSpacing_AndLeavesNonFiniteValuesAlone()
    {
        new GridSettings(0, true).Normalized().Spacing.ShouldBe(GridSettings.MinSpacing);
        new GridSettings(999_999, true).Normalized().Spacing.ShouldBe(GridSettings.MaxSpacing);
        new GridSettings(-5, false).Snap(new PointD(2.4, 2.6)).ShouldBe(new PointD(2, 3)); // wie Abstand 1
        var nan = new GridSettings(16, true).Snap(new PointD(double.NaN, 5));
        double.IsNaN(nan.X).ShouldBeTrue();
    }

    // --- Lineale --------------------------------------------------------------------------------

    [Theory]
    [InlineData(1.0, 100)]   // 60 px → nächster 1-2-5-Schritt ≥ 60 ist 100
    [InlineData(2.0, 50)]    // 30 px → 50
    [InlineData(10.0, 10)]   // 6 px → 10
    [InlineData(32.0, 2)]    // 1,875 → 2
    [InlineData(0.5, 200)]   // 120 → 200
    [InlineData(0.01, 10000)] // 6000 → 10000
    [InlineData(0.6, 100)]   // 100 → genau 100 (Grenzwert, kein Sprung auf 200)
    public void MajorStep_IsOneTwoFiveTimesPowerOfTen_AndAtLeast60DevicePixels(double zoom, double expected)
    {
        RulerScale.MajorStep(zoom).ShouldBe(expected, 1e-9);
        (RulerScale.MajorStep(zoom) * zoom).ShouldBeGreaterThanOrEqualTo(RulerScale.MinMajorSpacingDevicePx * (1 - 1e-9));
    }

    [Fact]
    public void MajorStep_RejectsInvalidZoom()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => RulerScale.MajorStep(0));
        Should.Throw<ArgumentOutOfRangeException>(() => RulerScale.MajorStep(double.NaN));
    }

    [Fact]
    public void Ticks_At100Percent_HaveMajorEvery100AndMinorEvery20()
    {
        var ticks = RulerScale.Ticks(1.0, 0, 200);
        ticks.Select(t => t.Position).ShouldBe([0, 20, 40, 60, 80, 100, 120, 140, 160, 180, 200]);
        ticks.Where(t => t.Major).Select(t => t.Label).ShouldBe(["0", "100", "200"]);
        ticks.Where(t => !t.Major).ShouldAllBe(t => t.Label == null);
    }

    [Fact]
    public void Ticks_OnlyInsideVisibleRange_AndAscending()
    {
        var ticks = RulerScale.Ticks(1.0, 30, 130);
        ticks.Select(t => t.Position).ShouldBe([40, 60, 80, 100, 120]);
        ticks.Select(t => t.Position).ShouldBe(ticks.Select(t => t.Position).OrderBy(x => x));
    }

    [Fact]
    public void Ticks_AtHighZoom_UseFractionlessLabelsAndMinorSteps()
    {
        // Zoom 32: Hauptabstand 2 px (64 Gerätepixel), Teilstriche alle 0,4 px (12,8 Gerätepixel).
        var ticks = RulerScale.Ticks(32, 0, 2);
        ticks.Select(t => t.Position).ShouldBe([0, 0.4, 0.8, 1.2, 1.6, 2]);
        ticks.Where(t => t.Major).Select(t => t.Label).ShouldBe(["0", "2"]);
    }

    [Fact]
    public void Ticks_AtSmallZoom_StillHaveMinorTicksAtLeast12DevicePixelsApart()
    {
        // Zoom 0,6: Haupt 100 px (60 Gerätepixel), Teilstrich alle 20 px = 12 Gerätepixel.
        var ticks = RulerScale.Ticks(0.6, 0, 300);
        ticks.Select(t => t.Position).ShouldBe([0, 20, 40, 60, 80, 100, 120, 140, 160, 180, 200, 220, 240, 260, 280, 300]);
        ticks.Where(t => t.Major).Select(t => t.Position).ShouldBe([0, 100, 200, 300]);
    }

    [Fact]
    public void Ticks_InvalidRange_IsEmpty()
    {
        RulerScale.Ticks(1.0, 10, 5).ShouldBeEmpty();
        RulerScale.Ticks(1.0, double.NaN, 5).ShouldBeEmpty();
    }

    [Fact]
    public void Ticks_NegativeRange_Works()
    {
        RulerScale.Ticks(1.0, -50, 50).Select(t => t.Position).ShouldBe([-40, -20, 0, 20, 40]);
    }
}
