using OrbitStudio.Domain.Imaging;
using OrbitStudio.Domain.Layers;
using OrbitStudio.Rendering;

namespace OrbitStudio.Rendering.Tests;

/// <summary>
/// Rendering-Referenztests mit exakt berechneten Erwartungswerten (Rechenweg im Kommentar).
/// Toleranz: keine – das Ergebnis ist byteweise festgelegt (Rundung kaufmännisch, siehe LayerCompositor).
/// </summary>
public class LayerCompositorTests
{
    private static PixelLayer Solid(Bgra32 color, int width = 1, int height = 1, string name = "L")
    {
        var buffer = PixelBuffer.Create(width, height);
        buffer.Fill(color);
        return new PixelLayer(name, buffer);
    }

    private static Bgra32 Gray(byte v) => new(v, v, v, 255);

    private static Bgra32 CompositePixel(LayerGroup root, int width = 1, int height = 1, int x = 0, int y = 0)
    {
        var target = PixelBuffer.Create(width, height);
        LayerCompositor.CompositeRegion(root, target, target.Bounds);
        return target.GetPixel(x, y);
    }

    private static LayerGroup Stack(params LayerNode[] bottomToTop)
    {
        var root = new LayerGroup("root");
        foreach (var node in bottomToTop)
        {
            root.Add(node);
        }

        return root;
    }

    [Theory]
    [InlineData(0, 0, 0, 0)]
    [InlineData(10, 20, 30, 40)]
    [InlineData(1, 2, 3, 255)]
    [InlineData(255, 255, 255, 255)]
    [InlineData(64, 0, 64, 64)]
    public void SingleNormalLayer_IsReproducedExactly(byte b, byte g, byte r, byte a)
    {
        CompositePixel(Stack(Solid(new Bgra32(b, g, r, a))))
            .ShouldBe(new Bgra32(b, g, r, a));
    }

    [Fact]
    public void EmptyStack_IsTransparent()
    {
        CompositePixel(Stack()).ShouldBe(Bgra32.Transparent);
    }

    [Fact]
    public void Normal_SemiTransparentOverWhite()
    {
        // Rot 50 % (vormultipliert 0,0,128,128) über Weiß:
        // R = 128 + 255·(1 − 128/255) = 255; G = B = 0 + 255·127/255 = 127; A = 255
        CompositePixel(Stack(Solid(Bgra32.White), Solid(new Bgra32(0, 0, 128, 128))))
            .ShouldBe(new Bgra32(127, 127, 255, 255));
    }

    // Grau 128 (oben, deckend) über Grau 200 (unten, deckend): co = B(Cb = 200/255, Cs = 128/255)·255
    [Theory]
    [InlineData(BlendMode.Normal, 128)]
    [InlineData(BlendMode.Multiply, 100)] // 200·128/255 = 100,39 → 100
    [InlineData(BlendMode.Screen, 228)] // 200 + 128 − 100,39 = 227,61 → 228
    [InlineData(BlendMode.Add, 255)] // min(255, 328)
    [InlineData(BlendMode.Negation, 182)] // 255 − |255 − 200 − 128| = 182
    [InlineData(BlendMode.Difference, 72)] // |200 − 128|
    [InlineData(BlendMode.Darken, 128)]
    [InlineData(BlendMode.Lighten, 200)]
    [InlineData(BlendMode.Overlay, 200)] // Cb > 0,5: 255 − 2·127·55/255 = 200,22 → 200
    public void BlendModes_OpaqueOverOpaque(BlendMode mode, byte expected)
    {
        var top = Solid(Gray(128));
        top.BlendMode = mode;

        CompositePixel(Stack(Solid(Gray(200)), top)).ShouldBe(Gray(expected));
    }

    [Theory]
    [InlineData(BlendMode.Overlay, 100)] // Cb = 100/255 ≤ 0,5: 2·128·100/255 = 100,39 → 100
    [InlineData(BlendMode.Negation, 228)] // 255 − |255 − 100 − 128| = 228
    [InlineData(BlendMode.Difference, 28)]
    public void BlendModes_DarkBackdrop(BlendMode mode, byte expected)
    {
        var top = Solid(Gray(128));
        top.BlendMode = mode;

        CompositePixel(Stack(Solid(Gray(100)), top)).ShouldBe(Gray(expected));
    }

    [Fact]
    public void Multiply_OverSemiTransparentBackdrop_FollowsW3CFormula()
    {
        // Hintergrund: gerade 200 bei α = 128 → vormultipliert 100. Quelle: Grau 128 deckend, Multiplizieren.
        // co = cs·(1 − αb) + αb·Cb·Cs = (128·127 + 100·128) / 255 = 113,95 → 114; αo = 1 → 255
        var backdrop = Solid(new Bgra32(100, 100, 100, 128));
        var top = Solid(Gray(128));
        top.BlendMode = BlendMode.Multiply;

        CompositePixel(Stack(backdrop, top)).ShouldBe(Gray(114));
    }

    [Fact]
    public void AnyMode_OverTransparentBackdrop_EqualsNormal()
    {
        foreach (var mode in Enum.GetValues<BlendMode>())
        {
            var top = Solid(new Bgra32(30, 60, 90, 120));
            top.BlendMode = mode;
            CompositePixel(Stack(top)).ShouldBe(new Bgra32(30, 60, 90, 120), mode.ToString());
        }
    }

    [Fact]
    public void Opacity_ScalesSource()
    {
        // 128·0,4 + 255·0,6 = 204,2 → 204
        var top = Solid(Gray(128));
        top.Opacity = 0.4;

        CompositePixel(Stack(Solid(Bgra32.White), top)).ShouldBe(Gray(204));
    }

    [Fact]
    public void HiddenOrZeroOpacityLayers_ContributeNothing()
    {
        var hidden = Solid(Bgra32.Black);
        hidden.IsVisible = false;
        var invisible = Solid(Bgra32.Black);
        invisible.Opacity = 0;

        CompositePixel(Stack(Solid(Bgra32.White), hidden, invisible)).ShouldBe(Bgra32.White);
    }

    [Fact]
    public void Mask_ScalesAlpha_AndCanBeDisabled()
    {
        // Weiß mit Maske 128 über transparent: 255·128/255 = 128 in allen Kanälen
        var layer = Solid(Bgra32.White);
        layer.Mask = LayerMask.CreateRevealAll(1, 1);
        layer.Mask.Set(0, 0, 128);

        var root = Stack(layer);
        CompositePixel(root).ShouldBe(new Bgra32(128, 128, 128, 128));

        layer.Mask.IsEnabled = false;
        CompositePixel(root).ShouldBe(Bgra32.White);
    }

    [Fact]
    public void Group_IsCompositedInIsolation_ThenBlendedWithGroupOpacity()
    {
        // In der Gruppe: Weiß (Multiplizieren) über Grau 128 → 128. Gruppe 50 % über transparent:
        // Farbe 128·0,5 = 64; Alpha 255·0,5 = 127,5 → 128
        var group = new LayerGroup("G") { Opacity = 0.5 };
        group.Add(Solid(Gray(128)));
        var multiply = Solid(Bgra32.White);
        multiply.BlendMode = BlendMode.Multiply;
        group.Add(multiply);

        CompositePixel(Stack(group)).ShouldBe(new Bgra32(64, 64, 64, 128));
    }

    [Fact]
    public void Group_BlendModeAppliesToGroupResult()
    {
        // Gruppe (Inhalt Grau 128) mit Multiplizieren über Grau 200 → 100 (wie eine einzelne Ebene)
        var group = new LayerGroup("G") { BlendMode = BlendMode.Multiply };
        group.Add(Solid(Gray(128)));

        CompositePixel(Stack(Solid(Gray(200)), group)).ShouldBe(Gray(100));
    }

    [Fact]
    public void HiddenGroup_HidesChildren()
    {
        var group = new LayerGroup("G") { IsVisible = false };
        group.Add(Solid(Bgra32.Black));

        CompositePixel(Stack(Solid(Bgra32.White), group)).ShouldBe(Bgra32.White);
    }

    [Fact]
    public void CompositeRegion_OnlyWritesInsideRegion()
    {
        var root = Stack(Solid(Bgra32.White, 4, 4));
        var target = PixelBuffer.Create(4, 4);
        var marker = new Bgra32(1, 2, 3, 4);
        target.Fill(marker);

        LayerCompositor.CompositeRegion(root, target, new IntRect(1, 1, 2, 2));

        target.GetPixel(0, 0).ShouldBe(marker);
        target.GetPixel(3, 3).ShouldBe(marker);
        target.GetPixel(1, 1).ShouldBe(Bgra32.White);
        target.GetPixel(2, 2).ShouldBe(Bgra32.White);
    }

    [Fact]
    public void ParallelPath_EqualsTileWiseSerialPath()
    {
        // 300×300 = 90.000 Pixel → paralleler Pfad; 30×30-Kacheln → serieller Pfad
        var random = new Random(42);
        var root = Stack(RandomLayer(random, 300, 300, BlendMode.Normal), RandomLayer(random, 300, 300, BlendMode.Overlay),
            RandomLayer(random, 300, 300, BlendMode.Screen));
        var whole = PixelBuffer.Create(300, 300);
        LayerCompositor.CompositeRegion(root, whole, whole.Bounds);

        var tiled = PixelBuffer.Create(300, 300);
        for (var y = 0; y < 300; y += 30)
        {
            for (var x = 0; x < 300; x += 30)
            {
                LayerCompositor.CompositeRegion(root, tiled, new IntRect(x, y, 30, 30));
            }
        }

        tiled.ContentEquals(whole).ShouldBeTrue();
    }

    [Fact]
    public void Output_IsAlwaysValidPremultiplied_ForAllModes()
    {
        var random = new Random(7);
        foreach (var mode in Enum.GetValues<BlendMode>())
        {
            var root = Stack(RandomLayer(random, 40, 25, BlendMode.Normal), RandomLayer(random, 40, 25, mode));
            ((PixelLayer)root.Children[1]).Opacity = random.NextDouble();
            var target = PixelBuffer.Create(40, 25);
            LayerCompositor.CompositeRegion(root, target, target.Bounds);

            for (var y = 0; y < 25; y++)
            {
                foreach (var p in target.GetPixelRow(y))
                {
                    p.IsValidPremultiplied.ShouldBeTrue($"{mode}: {p}");
                }
            }
        }
    }

    [Fact]
    public void BlendInto_MatchesCompositeOfTwoLayers()
    {
        var random = new Random(3);
        var lower = RandomLayer(random, 20, 20, BlendMode.Normal, opaque: true);
        var upper = RandomLayer(random, 20, 20, BlendMode.Multiply);
        upper.Opacity = 0.6;
        var expected = PixelBuffer.Create(20, 20);
        LayerCompositor.CompositeRegion(Stack(lower, upper), expected, expected.Bounds);

        var merged = lower.Pixels.Clone();
        LayerCompositor.BlendInto(merged, upper.Pixels, upper.Mask, upper.Opacity, upper.BlendMode);

        merged.ContentEquals(expected).ShouldBeTrue();
    }

    [Fact]
    public void BlendInto_RejectsDifferentSizes()
    {
        Should.Throw<ArgumentException>(() =>
            LayerCompositor.BlendInto(PixelBuffer.Create(2, 2), PixelBuffer.Create(3, 2), null, 1, BlendMode.Normal));
    }

    [Theory]
    [InlineData(BlendMode.Multiply, 0.5, 0.5, 0.25)]
    [InlineData(BlendMode.Screen, 0.5, 0.5, 0.75)]
    [InlineData(BlendMode.Add, 0.7, 0.6, 1.0)]
    [InlineData(BlendMode.Negation, 0.2, 0.3, 0.5)]
    [InlineData(BlendMode.Difference, 0.2, 0.9, 0.7)]
    [InlineData(BlendMode.Overlay, 0.25, 0.5, 0.25)]
    [InlineData(BlendMode.Overlay, 0.75, 0.5, 0.75)]
    public void BlendFunction_Values(BlendMode mode, double backdrop, double source, double expected)
    {
        LayerCompositor.Blend(backdrop, source, mode).ShouldBe(expected, 1e-12);
    }

    private static PixelLayer RandomLayer(Random random, int width, int height, BlendMode mode, bool opaque = false)
    {
        var buffer = PixelBuffer.Create(width, height);
        for (var y = 0; y < height; y++)
        {
            var row = buffer.GetPixelRow(y);
            for (var x = 0; x < width; x++)
            {
                var a = opaque ? (byte)255 : (byte)random.Next(256);
                row[x] = Bgra32.FromStraight((byte)random.Next(256), (byte)random.Next(256), (byte)random.Next(256), a);
            }
        }

        return new PixelLayer("Zufall", buffer) { BlendMode = mode };
    }
}
