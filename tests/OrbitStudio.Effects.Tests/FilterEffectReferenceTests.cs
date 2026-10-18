using OrbitStudio.Domain.Imaging;

namespace OrbitStudio.Effects.Tests;

/// <summary>Referenzwerte der Nachbarschaftseffekte (kleine Testbilder, Rechenweg im Kommentar).</summary>
public class FilterEffectReferenceTests : EffectTestBase
{
    [Fact]
    public void GaussianKernel_Radius2_IsSigma1_HalfWidth3_Normalized()
    {
        var kernel = Convolution.GaussianKernel(2);

        kernel.Length.ShouldBe(7);
        kernel.Sum().ShouldBe(1, 1e-12);
        // 1 / (1 + 2 · (e^−0,5 + e^−2 + e^−4,5)) = 1 / 2,50596 = 0,39905
        kernel[3].ShouldBe(0.39905, 1e-5);
        kernel[4].ShouldBe(kernel[2]);
        Convolution.GaussianKernel(0).ShouldBe([1.0]);
    }

    [Fact]
    public void Gaussian_SinglePoint_MatchesKernel()
    {
        // Zeile 9 × 1 schwarz, Mitte weiß; senkrecht (Höhe 1, Rand verlängert) bleibt die Zeile gleich.
        var source = GrayRows([0, 0, 0, 0, 255, 0, 0, 0, 0]);
        var result = Run(new GaussianBlurEffect(), source, ("radius", 2.0));

        // 255 · 0,39905 = 101,8 → 102; · e^−0,5 → 61,7 → 62; · e^−2 → 13,8 → 14; · e^−4,5 → 1,1 → 1
        Enumerable.Range(0, 9).Select(x => R(result, x)).ShouldBe(new byte[] { 0, 1, 14, 62, 102, 62, 14, 1, 0 });
        result.GetPixel(4, 0).A.ShouldBe((byte)255);
    }

    [Fact]
    public void Gaussian_MatchesNaiveTwoDimensionalReference_WithinOne()
    {
        var source = RandomImage(17, 13, seed: 3);
        var result = Run(new GaussianBlurEffect(), source, ("radius", 3.0));
        var kernel = Convolution.GaussianKernel(3);
        var k = kernel.Length / 2;

        var maxDiff = 0;
        for (var y = 0; y < 13; y++)
        {
            for (var x = 0; x < 17; x++)
            {
                double a = 0, r = 0;
                for (var j = -k; j <= k; j++)
                {
                    for (var i = -k; i <= k; i++)
                    {
                        var p = PixelMath.SampleClamped(source, x + i, y + j);
                        a += p.A * kernel[i + k] * kernel[j + k];
                        r += p.R * kernel[i + k] * kernel[j + k];
                    }
                }

                maxDiff = Math.Max(maxDiff, Math.Abs(result.GetPixel(x, y).A - PixelMath.ToByte(a)));
                maxDiff = Math.Max(maxDiff, Math.Abs(result.GetPixel(x, y).R - Math.Min(PixelMath.ToByte(r), PixelMath.ToByte(a))));
            }
        }

        maxDiff.ShouldBeLessThanOrEqualTo(1); // nur abweichende Summationsreihenfolge
    }

    [Fact]
    public void Blurs_KeepUniformImageUnchanged_RadiusZeroIsIdentity()
    {
        var uniform = Image(9, 7, new Bgra32(30, 60, 90, 120));
        Run(new GaussianBlurEffect(), uniform, ("radius", 7.5)).ContentEquals(uniform).ShouldBeTrue();
        Run(new BoxBlurEffect(), uniform, ("radius", 3.0)).ContentEquals(uniform).ShouldBeTrue();
        var random = RandomImage(8, 6, seed: 1);
        Run(new GaussianBlurEffect(), random, ("radius", 0.0)).ContentEquals(random).ShouldBeTrue();
        Run(new BoxBlurEffect(), random, ("radius", 0.0)).ContentEquals(random).ShouldBeTrue();
    }

    [Fact]
    public void BoxBlur_IsExactAverageOfSquare_EdgesExtended()
    {
        var source = Image(5, 5, Gray(0));
        source.SetPixel(2, 2, Gray(255));

        var r1 = Run(new BoxBlurEffect(), source, ("radius", 1.0));
        R(r1, 2, 2).ShouldBe((byte)28); // 255 / 9 = 28,3
        R(r1, 1, 1).ShouldBe((byte)28);
        R(r1, 0, 0).ShouldBe((byte)0);
        R(Run(new BoxBlurEffect(), source, ("radius", 2.0)), 2, 2).ShouldBe((byte)10); // 255 / 25 = 10,2
    }

    [Fact]
    public void BoxBlur_MatchesNaiveReferenceExactly()
    {
        var source = RandomImage(23, 19, seed: 7);
        const int radius = 2;
        var result = Run(new BoxBlurEffect(), source, ("radius", (double)radius));
        const int n = (2 * radius + 1) * (2 * radius + 1);
        for (var y = 0; y < 19; y++)
        {
            for (var x = 0; x < 23; x++)
            {
                long b = 0, a = 0;
                for (var j = -radius; j <= radius; j++)
                {
                    for (var i = -radius; i <= radius; i++)
                    {
                        var p = PixelMath.SampleClamped(source, x + i, y + j);
                        b += p.B;
                        a += p.A;
                    }
                }

                result.GetPixel(x, y).A.ShouldBe((byte)((a + n / 2) / n));
                result.GetPixel(x, y).B.ShouldBe((byte)((b + n / 2) / n));
            }
        }
    }

    [Fact]
    public void Sharpen_UnsharpMask_ReferenceValues()
    {
        // Gauß Radius 2 auf [100, 100, 100, 150, 150, 150]: x = 2 → 100 + 50 · (0,24203 + 0,05400 + 0,00443) = 115,0 → 115
        // Schärfen 50 %: 100 + 0,5 · (100 − 115) = 92,5 → 93; x = 3: Gauß 135, 150 + 0,5 · 15 = 157,5 → 158
        var source = GrayRows([100, 100, 100, 150, 150, 150]);
        var result = Run(new SharpenEffect(), source, ("amount", 50.0), ("radius", 2.0));

        R(result, 2).ShouldBe((byte)93);
        R(result, 3).ShouldBe((byte)158);
        R(result, 0).ShouldBeLessThanOrEqualTo((byte)100);
        Run(new SharpenEffect(), Image(6, 4, Gray(77))).ContentEquals(Image(6, 4, Gray(77))).ShouldBeTrue();
    }

    [Fact]
    public void EdgeDetect_SobelOnStepEdge()
    {
        var source = GrayRows([0, 0, 255, 255], [0, 0, 255, 255], [0, 0, 255, 255]);

        // x = 1: gx = 255 + 2 · 255 + 255 = 1020 → / 4 = 255; x = 0 und x = 3: 0
        var result = Run(new EdgeDetectEffect(), source);
        Enumerable.Range(0, 4).Select(x => R(result, x, 1)).ShouldBe(new byte[] { 0, 255, 255, 0 });
        Enumerable.Range(0, 4).Select(x => R(Run(new EdgeDetectEffect(), source, ("invert", true)), x, 1))
            .ShouldBe(new byte[] { 255, 0, 0, 255 });
        R(Run(new EdgeDetectEffect(), source, ("strength", 50.0)), 1, 1).ShouldBe((byte)128); // 127,5
    }

    [Fact]
    public void Noise_ZeroAmountIsIdentity_SameSeedSameResult_DifferentSeedDiffers()
    {
        var source = Image(20, 20, Gray(128));
        Run(new NoiseEffect(), source, ("amount", 0.0)).ContentEquals(source).ShouldBeTrue();
        var a = Run(new NoiseEffect(), source, ("seed", 5.0));
        Run(new NoiseEffect(), source, ("seed", 5.0)).ContentEquals(a).ShouldBeTrue();
        Run(new NoiseEffect(), source, ("seed", 6.0)).ContentEquals(a).ShouldBeFalse();
    }

    [Fact]
    public void Noise_IsIndependentOfRegion()
    {
        var source = RandomImage(30, 20, seed: 2, opaque: true);
        var effect = new NoiseEffect();
        var full = EffectEngine.Render(effect, EffectSettings.Defaults(effect), source, null, CancellationToken.None).Pixels;
        var part = EffectEngine.Render(effect, EffectSettings.Defaults(effect), source, null, CancellationToken.None,
            new IntRect(10, 5, 8, 6)).Pixels;

        for (var y = 0; y < 6; y++)
        {
            for (var x = 0; x < 8; x++)
            {
                part.GetPixel(x, y).ShouldBe(full.GetPixel(10 + x, 5 + y));
            }
        }
    }

    [Fact]
    public void Noise_StatisticsMatchAmount_MonochromeWithoutColorSaturation()
    {
        var result = Run(new NoiseEffect(), Image(200, 200, Gray(128)), ("amount", 20.0), ("colorSaturation", 0.0));
        double sum = 0, sumSq = 0;
        for (var y = 0; y < 200; y++)
        {
            foreach (var p in result.GetPixelRow(y))
            {
                p.R.ShouldBe(p.G);
                p.G.ShouldBe(p.B);
                sum += p.R;
                sumSq += p.R * (double)p.R;
            }
        }

        var mean = sum / 40000;
        var std = Math.Sqrt(sumSq / 40000 - mean * mean);
        mean.ShouldBe(128, 0.5);
        std.ShouldBe(20, 0.6);
    }

    [Fact]
    public void Denoise_MedianRemovesSaltAndPepper_StrengthBlends()
    {
        var source = Image(5, 5, Gray(100));
        source.SetPixel(2, 2, Gray(255));

        Run(new DenoiseEffect(), source).ContentEquals(Image(5, 5, Gray(100))).ShouldBeTrue();
        R(Run(new DenoiseEffect(), source, ("strength", 50.0)), 2, 2).ShouldBe((byte)178); // 100 + 155/2 = 177,5
        var edge = GrayRows([0, 0, 0, 200, 200, 200], [0, 0, 0, 200, 200, 200], [0, 0, 0, 200, 200, 200]);
        Run(new DenoiseEffect(), edge).ContentEquals(edge).ShouldBeTrue(); // Kanten bleiben erhalten
    }

    [Fact]
    public void Pixelate_AveragesCells_ClippedAtEdge()
    {
        var source = GrayRows([0, 100, 200, 40, 9], [100, 100, 0, 40, 9]);
        var result = Run(new PixelateEffect(), source, ("cellSize", 2.0));

        // Zelle 1: (0 + 100 + 100 + 100) / 4 = 75; Zelle 2: (200 + 40 + 0 + 40) / 4 = 70; Randzelle 1 × 2: 9
        Enumerable.Range(0, 5).Select(x => R(result, x, 0)).ShouldBe(new byte[] { 75, 75, 70, 70, 9 });
        Enumerable.Range(0, 5).Select(x => R(result, x, 1)).ShouldBe(new byte[] { 75, 75, 70, 70, 9 });
    }

    [Fact]
    public void DropShadow_OffsetOpacitySoftness()
    {
        var source = PixelBuffer.Create(10, 10);
        source.SetPixel(2, 2, Bgra32.White);

        var hard = Run(new DropShadowEffect(), source, ("offsetX", 3.0), ("offsetY", 3.0), ("softness", 0.0), ("opacity", 100.0));
        hard.GetPixel(5, 5).ShouldBe(Bgra32.Black);
        hard.GetPixel(2, 2).ShouldBe(Bgra32.White);
        hard.GetPixel(3, 3).ShouldBe(Bgra32.Transparent);

        var half = Run(new DropShadowEffect(), source, ("offsetX", 3.0), ("offsetY", 3.0), ("softness", 0.0), ("opacity", 50.0));
        half.GetPixel(5, 5).ShouldBe(new Bgra32(0, 0, 0, 128)); // 127,5

        // Weichheit 2 (σ = 1): Mitte 255 · 0,39905² = 40,6 → 41
        var soft = Run(new DropShadowEffect(), source, ("offsetX", 3.0), ("offsetY", 3.0), ("softness", 2.0), ("opacity", 100.0));
        soft.GetPixel(5, 5).A.ShouldBe((byte)41);

        var red = Run(new DropShadowEffect(), source, ("offsetX", 3.0), ("offsetY", 3.0), ("softness", 0.0), ("opacity", 100.0),
            ("color", new ColorRgba(255, 0, 0)));
        red.GetPixel(5, 5).ShouldBe(new Bgra32(0, 0, 255, 255));
    }
}
