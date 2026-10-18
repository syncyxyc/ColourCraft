using OrbitStudio.Domain.Documents;
using OrbitStudio.Domain.Imaging;
using OrbitStudio.Domain.Selection;
using OrbitStudio.History;

namespace OrbitStudio.Effects.Tests;

/// <summary>Effektmodell, Registry, Validierung, Auswahl, Historie, Abbruch und Vorschau.</summary>
public class EffectEngineTests : EffectTestBase
{
    [Fact]
    public void Registry_ContainsAllFourteenRequiredEffects_WithUniqueIds()
    {
        var registry = EffectRegistry.CreateDefault();
        string[] required =
        [
            "gaussianBlur", "boxBlur", "sharpen", "brightness", "contrast", "hue", "saturation", "invert", "blackWhite",
            "sepia", "edges", "noise", "denoise", "channels",
        ];

        foreach (var id in required)
        {
            registry.Find(id).ShouldNotBeNull(id);
        }

        registry.All.Select(e => e.Id).ShouldBeUnique();
        registry.All.Count.ShouldBe(19);
        Should.Throw<InvalidOperationException>(() => registry.Register(new InvertEffect()));
    }

    [Fact]
    public void EveryEffect_HasValidDefaults_AndProducesValidPremultipliedPixels()
    {
        var source = RandomImage(16, 12, seed: 11);
        foreach (var effect in EffectRegistry.CreateDefault().All)
        {
            var settings = EffectSettings.Defaults(effect);
            EffectEngine.Validate(effect, settings).ShouldBeEmpty(effect.Id);
            var result = EffectEngine.Render(effect, settings, source, null, CancellationToken.None).Pixels;
            for (var y = 0; y < 12; y++)
            {
                foreach (var p in result.GetPixelRow(y))
                {
                    p.IsValidPremultiplied.ShouldBeTrue(effect.Id);
                }
            }
        }
    }

    [Fact]
    public void Settings_ClampNumbers_RejectInvalidRawValues()
    {
        var blur = new GaussianBlurEffect();
        EffectSettings.Defaults(blur).With(blur, "radius", 999.0).Number("radius").ShouldBe(200);
        EffectSettings.Defaults(blur).With(blur, "radius", 3.14159).Number("radius").ShouldBe(3.1); // 1 Nachkommastelle

        var invalid = EffectSettings.Defaults(blur).WithRaw("radius", -5.0);
        EffectEngine.Validate(blur, invalid).ShouldHaveSingleItem().ShouldBe(new EffectValidationError("radius", "Effect.Error.Range"));
        EffectEngine.Validate(blur, EffectSettings.Defaults(blur).WithRaw("radius", double.NaN)).ShouldNotBeEmpty();
        EffectEngine.Validate(blur, EffectSettings.Defaults(blur).WithRaw("radius", "x")).ShouldNotBeEmpty();
        Should.Throw<ArgumentException>(() => EffectSettings.Defaults(blur).With(blur, "gibtsnicht", 1.0));
    }

    [Fact]
    public void Selection_LimitsEffect_SoftSelectionBlends()
    {
        var source = Image(4, 1, Gray(100));
        var selection = SelectionMask.CreateEmpty(4, 1);
        selection.Set(0, 0, 255);
        selection.Set(1, 0, 128);

        var result = Run(new InvertEffect(), source, selection);
        R(result, 0).ShouldBe((byte)155);
        R(result, 1).ShouldBe((byte)128); // 100 + (155 − 100) · 128/255 = 127,6
        R(result, 2).ShouldBe((byte)100);
    }

    [Fact]
    public void Apply_ChangesOnlyActiveLayer_IsOneUndoStep_UndoRedoExact()
    {
        var document = ImageDocument.Create("E", 8, 6, "HG");
        var history = new DocumentHistory(document);
        var layer = document.ActivePixelLayer!;
        layer.Pixels.Fill(new IntRect(0, 0, 4, 6), Gray(40));
        var before = layer.Pixels.Clone();

        var entry = EffectEngine.Apply(document, history, new InvertEffect(), EffectSettings.Defaults(new InvertEffect()));

        entry.ShouldNotBeNull();
        entry!.NameKey.ShouldBe("Effect.invert");
        layer.Pixels.GetPixel(0, 0).ShouldBe(Gray(215));
        layer.Pixels.GetPixel(7, 0).ShouldBe(Gray(0));
        var after = layer.Pixels.Clone();
        history.Undo();
        layer.Pixels.ContentEquals(before).ShouldBeTrue();
        history.Redo();
        layer.Pixels.ContentEquals(after).ShouldBeTrue();
    }

    [Fact]
    public void Apply_WithSelection_ComputesOnlySelectionBounds()
    {
        var document = ImageDocument.Create("E", 10, 10, "HG");
        var selection = SelectionMask.CreateEmpty(10, 10);
        selection.Fill(new IntRect(2, 3, 4, 2), 255);
        document.SetSelection(selection);
        EffectEngine.TargetRegion(document.ActivePixelLayer!.Pixels, document.Selection).ShouldBe(new IntRect(2, 3, 4, 2));

        EffectEngine.Apply(document, new DocumentHistory(document), new InvertEffect(), EffectSettings.Defaults(new InvertEffect()));

        document.ActivePixelLayer!.Pixels.GetPixel(3, 4).ShouldBe(Gray(0));
        document.ActivePixelLayer.Pixels.GetPixel(1, 4).ShouldBe(Bgra32.White);
    }

    [Fact]
    public void Apply_OnLockedLayer_IsRejected()
    {
        var document = ImageDocument.Create("E", 4, 4, "HG");
        document.ActivePixelLayer!.IsLocked = true;

        Should.Throw<InvalidOperationException>(() =>
            EffectEngine.Apply(document, new DocumentHistory(document), new InvertEffect(), EffectSettings.Defaults(new InvertEffect())));
    }

    [Theory]
    [InlineData("gaussianBlur")]
    [InlineData("boxBlur")]
    [InlineData("invert")]
    [InlineData("denoise")]
    [InlineData("brightnessContrast")]
    public void CancelledToken_Throws_AndDocumentStaysUnchanged(string id)
    {
        var document = ImageDocument.Create("E", 64, 64, "HG");
        var before = document.ActivePixelLayer!.Pixels.Clone();
        var effect = EffectRegistry.CreateDefault().Get(id);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Should.Throw<OperationCanceledException>(() =>
            EffectEngine.Apply(document, new DocumentHistory(document), effect, EffectSettings.Defaults(effect), cts.Token));
        document.ActivePixelLayer.Pixels.ContentEquals(before).ShouldBeTrue();
    }

    [Fact]
    public void Preview_IsDownscaled_SpatialParametersScaled()
    {
        var source = Image(400, 300, Gray(90));
        var preview = EffectEngine.CreatePreview(source, null, 100, 100);

        preview.Scale.ShouldBe(0.25);
        (preview.Source.Width, preview.Source.Height).ShouldBe((100, 75));
        var blur = new GaussianBlurEffect();
        EffectSettings.Defaults(blur).With(blur, "radius", 8.0).Scaled(blur, 0.25).Number("radius").ShouldBe(2);
        EffectSettings.Defaults(blur).With(blur, "radius", 1.0).Scaled(blur, 0.25).Number("radius").ShouldBe(1); // sichtbar bleiben
        var shadow = new DropShadowEffect();
        EffectSettings.Defaults(shadow).With(shadow, "offsetX", -6.0).Scaled(shadow, 0.5).Number("offsetX").ShouldBe(-3);
        EffectSettings.Defaults(shadow).Scaled(shadow, 0.5).Number("opacity").ShouldBe(60); // keine Länge

        preview.Render(blur, EffectSettings.Defaults(blur), CancellationToken.None).ContentEquals(preview.Source).ShouldBeTrue();
    }

    [Fact]
    public void Preview_RespectsSelection()
    {
        var source = Image(200, 100, Gray(100));
        var selection = SelectionMask.CreateEmpty(200, 100);
        selection.Fill(new IntRect(0, 0, 100, 100), 255);
        var preview = EffectEngine.CreatePreview(source, selection, 50, 50);

        var result = preview.Render(new InvertEffect(), EffectSettings.Defaults(new InvertEffect()), CancellationToken.None);
        R(result, 5, 5).ShouldBe((byte)155);
        R(result, 45, 5).ShouldBe((byte)100);
    }

    [Fact]
    public void SmallImage_PreviewKeepsFullResolution()
    {
        var preview = EffectEngine.CreatePreview(Image(20, 10, Gray(1)), null, 100, 100);
        preview.Scale.ShouldBe(1);
        preview.Source.Width.ShouldBe(20);
    }
}
