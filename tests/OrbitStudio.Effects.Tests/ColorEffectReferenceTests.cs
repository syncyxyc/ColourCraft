using OrbitStudio.Domain.Imaging;

namespace OrbitStudio.Effects.Tests;

/// <summary>Referenzwerte der Farbeffekte (exakt, von Hand berechnet; Rechenweg im Kommentar).</summary>
public class ColorEffectReferenceTests : EffectTestBase
{
    private static readonly PixelBuffer Sample = Image(1, 1, Rgb(100, 150, 200));

    private static Bgra32 One(IEffect effect, Bgra32 pixel, params (string, object)[] values) =>
        Run(effect, Image(1, 1, pixel), values).GetPixel(0, 0);

    [Fact]
    public void Brightness_Adds255TimesPercent()
    {
        // 2,55 · 20 = 51
        One(new BrightnessEffect(), Rgb(100, 150, 200), ("brightness", 20.0)).ShouldBe(Rgb(151, 201, 251));
        One(new BrightnessEffect(), Rgb(100, 150, 200), ("brightness", 100.0)).ShouldBe(Rgb(255, 255, 255));
        One(new BrightnessEffect(), Rgb(100, 150, 200), ("brightness", -100.0)).ShouldBe(Rgb(0, 0, 0));
    }

    [Fact]
    public void Contrast_ScalesAroundMiddleGray()
    {
        // f = 1,5² = 2,25: (100 − 127,5) · 2,25 + 127,5 = 65,625 → 66; 150 → 178,125 → 178; 200 → 290,6 → 255
        One(new ContrastEffect(), Rgb(100, 150, 200), ("contrast", 50.0)).ShouldBe(Rgb(66, 178, 255));
        // f = 0: alles 127,5 → 128
        One(new ContrastEffect(), Rgb(100, 150, 200), ("contrast", -100.0)).ShouldBe(Gray(128));
        One(new ContrastEffect(), Rgb(100, 150, 200)).ShouldBe(Rgb(100, 150, 200));
    }

    [Fact]
    public void Hue_RotatesHslHue()
    {
        One(new HueEffect(), Rgb(255, 0, 0), ("hue", 120.0)).ShouldBe(Rgb(0, 255, 0));
        One(new HueEffect(), Rgb(255, 0, 0), ("hue", -120.0)).ShouldBe(Rgb(0, 0, 255));
        One(new HueEffect(), Rgb(255, 0, 0), ("hue", 180.0)).ShouldBe(Rgb(0, 255, 255));
        One(new HueEffect(), Rgb(200, 100, 50)).ShouldBe(Rgb(200, 100, 50)); // Rundreise RGB → HSL → RGB exakt
        One(new HueEffect(), Gray(90), ("hue", 77.0)).ShouldBe(Gray(90)); // Grau hat keinen Farbton
    }

    [Fact]
    public void Saturation_ScalesHslSaturation()
    {
        // (200, 100, 50): H = 20°, S = 0,6, L = 125/255. −100 % → Grau 125.
        One(new SaturationEffect(), Rgb(200, 100, 50), ("saturation", -100.0)).ShouldBe(Gray(125));
        // +100 % → S = 1: q = 2L = 250/255 → R 250; G = q · 6 · (20/360) · 255 = 83,3 → 83; B = p = 0
        One(new SaturationEffect(), Rgb(200, 100, 50), ("saturation", 100.0)).ShouldBe(Rgb(250, 83, 0));
    }

    [Fact]
    public void Invert_255MinusValue()
    {
        Run(new InvertEffect(), Sample).GetPixel(0, 0).ShouldBe(Rgb(155, 105, 55));
    }

    [Fact]
    public void BlackWhite_Bt601Luma()
    {
        // 0,299 · 100 + 0,587 · 150 + 0,114 · 200 = 140,75 → 141
        Run(new BlackWhiteEffect(), Sample).GetPixel(0, 0).ShouldBe(Gray(141));
    }

    [Fact]
    public void Sepia_MatrixAndIntensity()
    {
        // (10, 20, 30): R' = 3,93 + 15,38 + 5,67 = 24,98 → 25; G' = 3,49 + 13,72 + 5,04 = 22,25 → 22;
        // B' = 2,72 + 10,68 + 3,93 = 17,33 → 17
        One(new SepiaEffect(), Rgb(10, 20, 30)).ShouldBe(Rgb(25, 22, 17));
        // 50 %: 10 + 14,98/2 = 17,49 → 17; 20 + 2,25/2 = 21,125 → 21; 30 − 12,67/2 = 23,665 → 24
        One(new SepiaEffect(), Rgb(10, 20, 30), ("intensity", 50.0)).ShouldBe(Rgb(17, 21, 24));
        One(new SepiaEffect(), Rgb(10, 20, 30), ("intensity", 0.0)).ShouldBe(Rgb(10, 20, 30));
    }

    [Fact]
    public void Channels_MultiplyEachChannel()
    {
        One(new ChannelEffect(), Rgb(100, 150, 200), ("red", 50.0), ("green", 200.0), ("blue", 0.0))
            .ShouldBe(Rgb(50, 255, 0));
    }

    [Fact]
    public void Levels_InputRangeGammaOutputRange()
    {
        var levels = new LevelsEffect();
        // Eingang 50–200: 100 → 50/150 · 255 = 85; 150 → 170; 200 → 255
        One(levels, Rgb(100, 150, 200), ("inBlack", 50.0), ("inWhite", 200.0)).ShouldBe(Rgb(85, 170, 255));
        // Gamma 2: (1/3)^0,5 · 255 = 147,2 → 147
        One(levels, Gray(100), ("inBlack", 50.0), ("inWhite", 200.0), ("gamma", 2.0)).ShouldBe(Gray(147));
        // Ausgabe 20–220: 20 + 200/3 = 86,67 → 87
        One(levels, Gray(100), ("inBlack", 50.0), ("inWhite", 200.0), ("outBlack", 20.0), ("outWhite", 220.0))
            .ShouldBe(Gray(87));
    }

    [Fact]
    public void Levels_InputBlackMustBeBelowInputWhite()
    {
        var levels = new LevelsEffect();
        var settings = Settings(levels, ("inBlack", 200.0), ("inWhite", 100.0));

        EffectEngine.Validate(levels, settings).ShouldHaveSingleItem().MessageKey.ShouldBe("Effect.Error.LevelsInput");
        Should.Throw<EffectValidationException>(() =>
            EffectEngine.Render(levels, settings, Sample, null, CancellationToken.None));
    }

    [Fact]
    public void BrightnessContrast_AppliesBothInOrder()
    {
        var effect = StandardEffects.Create().Single(e => e.Id == "brightnessContrast");
        // 100 + 51 = 151; (151 − 127,5) · 2,25 + 127,5 = 180,375 → 180
        One(effect, Gray(100), ("brightness", 20.0), ("contrast", 50.0)).ShouldBe(Gray(180));
    }

    [Fact]
    public void HueSaturation_AppliesBoth()
    {
        var effect = StandardEffects.Create().Single(e => e.Id == "hueSaturation");
        One(effect, Rgb(255, 0, 0), ("hue", 120.0), ("saturation", -100.0)).ShouldBe(Gray(128)); // L = 0,5 → 127,5 → 128
    }

    [Fact]
    public void PointEffects_UseStraightColor_KeepAlpha_TransparentStaysTransparent()
    {
        // Vormultipliziert (50, 50, 50, 128): gerade 99,61 → invertiert 155,39 → · 128/255 = 78,0 → 78
        One(new InvertEffect(), new Bgra32(50, 50, 50, 128)).ShouldBe(new Bgra32(78, 78, 78, 128));
        One(new BrightnessEffect(), Bgra32.Transparent, ("brightness", 100.0)).ShouldBe(Bgra32.Transparent);
    }
}
