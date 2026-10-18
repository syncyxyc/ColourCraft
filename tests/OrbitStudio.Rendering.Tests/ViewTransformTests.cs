using OrbitStudio.Domain.Imaging;
using OrbitStudio.Rendering;

namespace OrbitStudio.Rendering.Tests;

public class ViewTransformTests
{
    private const double Tolerance = 1e-9;

    /// <summary>1000×500-Bild in einem 800×600-DIP-Viewport.</summary>
    private static ViewTransform Create(double dpiScale = 1.0)
    {
        var view = new ViewTransform(1000, 500);
        view.SetViewport(800, 600, dpiScale);
        return view;
    }

    [Theory]
    [InlineData(1.0)]
    [InlineData(1.5)]
    [InlineData(2.0)]
    public void DocumentAndViewport_RoundTrip(double dpi)
    {
        var view = Create(dpi);
        view.SetZoom(3.7);
        var doc = new PointD(123.25, 456.75);

        var back = view.ViewportToDocument(view.DocumentToViewport(doc));

        back.X.ShouldBe(doc.X, Tolerance);
        back.Y.ShouldBe(doc.Y, Tolerance);
        var dipBack = view.DipToDocument(view.DocumentToDip(doc));
        dipBack.X.ShouldBe(doc.X, Tolerance);
        dipBack.Y.ShouldBe(doc.Y, Tolerance);
    }

    [Fact]
    public void Zoom100_MeansOneDocumentPixelPerDevicePixel_RegardlessOfDpi()
    {
        var view = Create(dpiScale: 1.5);
        view.ActualSize();

        var a = view.DocumentToViewport(new PointD(10, 10));
        var b = view.DocumentToViewport(new PointD(11, 10));

        (b.X - a.X).ShouldBe(1.0, Tolerance); // 1 Gerätepixel
        var dipA = view.DocumentToDip(new PointD(10, 10));
        var dipB = view.DocumentToDip(new PointD(11, 10));
        (dipB.X - dipA.X).ShouldBe(1.0 / 1.5, Tolerance); // = 1 Gerätepixel in DIP
    }

    [Fact]
    public void Viewport_IsMeasuredInDevicePixels()
    {
        var view = Create(dpiScale: 1.25);
        view.ViewportWidth.ShouldBe(1000);
        view.ViewportHeight.ShouldBe(750);
    }

    [Fact]
    public void SetZoom_KeepsAnchorPointFixed()
    {
        var view = Create();
        view.SetZoom(4);
        var anchor = new PointD(300, 200);
        var under = view.ViewportToDocument(anchor);

        view.SetZoom(8, anchor);

        var after = view.ViewportToDocument(anchor);
        after.X.ShouldBe(under.X, 1e-6);
        after.Y.ShouldBe(under.Y, 1e-6);
    }

    [Fact]
    public void ZoomIn_And_ZoomOut_FollowPresets()
    {
        var view = Create();
        view.ActualSize();

        view.ZoomIn();
        view.Zoom.ShouldBe(1.5);
        view.ZoomIn();
        view.Zoom.ShouldBe(2);
        view.ZoomOut();
        view.ZoomOut();
        view.ZoomOut();
        view.Zoom.ShouldBe(2.0 / 3, Tolerance);
    }

    [Fact]
    public void ZoomFromNonPresetValue_GoesToNextPreset()
    {
        var view = Create();
        view.SetZoom(1.2);
        view.ZoomIn();
        view.Zoom.ShouldBe(1.5);

        view.SetZoom(1.2);
        view.ZoomOut();
        view.Zoom.ShouldBe(1.0);
    }

    [Fact]
    public void Zoom_IsClampedToLimits()
    {
        var view = Create();
        view.SetZoom(1000);
        view.Zoom.ShouldBe(ViewTransform.MaxZoom);
        view.ZoomIn();
        view.Zoom.ShouldBe(ViewTransform.MaxZoom);

        view.SetZoom(0.0001);
        view.Zoom.ShouldBe(ViewTransform.MinZoom);
        view.ZoomOut();
        view.Zoom.ShouldBe(ViewTransform.MinZoom);
    }

    [Fact]
    public void FitToViewport_ShowsWholeImageCentered()
    {
        var view = Create();

        view.FitToViewport();

        // Verfügbar: 800 − 64 = 736 breit, 600 − 64 = 536 hoch → min(0,736; 1,072) = 0,736
        view.Zoom.ShouldBe(0.736, Tolerance);
        view.VisibleDocumentRect().ShouldBe(new IntRect(0, 0, 1000, 500));
        var topLeft = view.DocumentToViewport(new PointD(0, 0));
        var bottomRight = view.DocumentToViewport(new PointD(1000, 500));
        topLeft.X.ShouldBe(800 - bottomRight.X, Tolerance); // horizontal zentriert
        topLeft.Y.ShouldBe(600 - bottomRight.Y, Tolerance); // vertikal zentriert
    }

    [Fact]
    public void FitWithoutUpscale_StaysAt100Percent_ForSmallImages()
    {
        var view = new ViewTransform(100, 50);
        view.SetViewport(800, 600, 1.0);

        view.FitToViewport(allowUpscale: false);
        view.Zoom.ShouldBe(1.0);

        view.FitToViewport();
        view.Zoom.ShouldBeGreaterThan(1.0);
    }

    [Fact]
    public void PanBy_IsLimitedToMargin()
    {
        var view = Create();
        view.SetZoom(4); // 4000×2000 Gerätepixel – größer als der Viewport

        view.PanBy(1_000_000, 1_000_000);
        view.OffsetX.ShouldBe(view.MarginDevice, Tolerance); // linke Bildkante höchstens am Rand
        view.OffsetY.ShouldBe(view.MarginDevice, Tolerance);

        view.PanBy(-10_000_000, -10_000_000);
        view.OffsetX.ShouldBe(800 - 4000 - view.MarginDevice, Tolerance);
        view.OffsetY.ShouldBe(600 - 2000 - view.MarginDevice, Tolerance);
    }

    [Fact]
    public void SmallImage_IsAlwaysCentered_EvenAfterPan()
    {
        var view = new ViewTransform(100, 50);
        view.SetViewport(800, 600, 1.0);
        view.ActualSize();

        view.PanBy(300, 300);

        view.OffsetX.ShouldBe(350, Tolerance);
        view.OffsetY.ShouldBe(275, Tolerance);
    }

    [Fact]
    public void ScrollRange_MatchesOffset_AndSetScrollMovesView()
    {
        var view = Create();
        view.SetZoom(2); // Inhalt 2000 + 64 breit

        var h = view.HorizontalScroll;
        h.Maximum.ShouldBe(2000 + 64 - 800, Tolerance);
        h.ViewportSize.ShouldBe(800);
        h.Value.ShouldBe(view.MarginDevice - view.OffsetX, Tolerance);

        view.SetHorizontalScroll(0);
        view.OffsetX.ShouldBe(view.MarginDevice, Tolerance);
        view.SetHorizontalScroll(h.Maximum);
        view.HorizontalScroll.Value.ShouldBe(h.Maximum, Tolerance);
    }

    [Fact]
    public void ScrollRange_IsZero_WhenImageFits()
    {
        var view = Create();
        view.FitToViewport();
        view.HorizontalScroll.Maximum.ShouldBe(0);
        view.VerticalScroll.Maximum.ShouldBe(0);
    }

    [Fact]
    public void PixelAtViewport_ReturnsIntegerPixelOrNull()
    {
        var view = Create();
        view.SetZoom(10);
        view.SetHorizontalScroll(view.MarginDevice); // Dokument-x 0 am linken Viewportrand
        view.SetVerticalScroll(view.MarginDevice);

        view.PixelAtViewport(new PointD(0.5, 0.5)).ShouldBe((0, 0));
        view.PixelAtViewport(new PointD(9.99, 19.99)).ShouldBe((0, 1));
        view.PixelAtViewport(new PointD(10, 20)).ShouldBe((1, 2));
        view.PixelAtViewport(new PointD(-1, 0)).ShouldBeNull();
    }

    [Fact]
    public void Resize_KeepsCenterDocumentPoint()
    {
        var view = Create();
        view.SetZoom(4);
        var center = view.ViewportToDocument(new PointD(400, 300));

        view.SetViewport(1000, 700, 1.0);

        var newCenter = view.ViewportToDocument(new PointD(500, 350));
        newCenter.X.ShouldBe(center.X, 1e-6);
        newCenter.Y.ShouldBe(center.Y, 1e-6);
    }

    [Fact]
    public void DpiChange_KeepsZoomMeaning()
    {
        var view = Create();
        view.ActualSize();

        view.SetViewport(800, 600, 2.0);

        view.Zoom.ShouldBe(1.0);
        view.ViewportWidth.ShouldBe(1600);
    }

    [Fact]
    public void Changed_IsRaisedOnEveryChange()
    {
        var view = Create();
        var count = 0;
        view.Changed += (_, _) => count++;

        view.ZoomIn();
        view.PanBy(5, 5);
        view.SetViewport(700, 500, 1.0);
        view.FitToViewport();

        count.ShouldBe(4);
    }

    [Fact]
    public void Presets_AreSortedAndWithinLimits()
    {
        ViewTransform.ZoomPresets.ShouldBe(ViewTransform.ZoomPresets.OrderBy(z => z).ToList());
        ViewTransform.ZoomPresets.First().ShouldBe(ViewTransform.MinZoom);
        ViewTransform.ZoomPresets.Last().ShouldBe(ViewTransform.MaxZoom);
        ViewTransform.ZoomPresets.ShouldContain(1.0);
    }

    [Fact]
    public void InvalidInputs_AreRejected()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new ViewTransform(0, 10));
        var view = Create();
        Should.Throw<ArgumentOutOfRangeException>(() => view.SetZoom(double.NaN));
        Should.Throw<ArgumentOutOfRangeException>(() => view.SetViewport(100, 100, 0));
    }
}
