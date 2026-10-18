using OrbitStudio.Domain.Imaging;

namespace OrbitStudio.Effects;

/// <summary>Abbildung einer geraden Farbe (0–255, double) auf eine neue; x, y = Bildkoordinaten (z. B. für Rauschen).</summary>
public delegate (double R, double G, double B) ColorMap(double r, double g, double b, int x, int y);

/// <summary>
/// Effekt, der jedes Pixel für sich verändert. Rechnet auf der geraden Farbe; Alpha bleibt unverändert, vollständig
/// transparente Pixel bleiben transparent. Ergebnis: <see cref="PixelMath.Premultiply"/>.
/// </summary>
public abstract class PointEffect : EffectBase
{
    public override EffectCategory Category => EffectCategory.Adjustment;

    /// <summary>Bereitet die Abbildung für eine Berechnung vor (Parameter einmal auslesen).</summary>
    protected abstract ColorMap Prepare(EffectSettings settings);

    public override void Render(EffectContext context)
    {
        var map = Prepare(context.Settings);
        var region = context.Region;
        PixelMath.ForEachRow(context, y =>
        {
            var source = context.Source.GetPixelRow(y);
            var target = context.Output.GetPixelRow(y - region.Y);
            for (var x = region.X; x < region.Right; x++)
            {
                var p = source[x];
                if (p.A == 0)
                {
                    target[x - region.X] = Bgra32.Transparent;
                    continue;
                }

                var (r, g, b) = PixelMath.Straight(p);
                var (nr, ng, nb) = map(r, g, b, x, y);
                target[x - region.X] = PixelMath.Premultiply(nr, ng, nb, p.A);
            }
        });
    }
}

/// <summary>Helligkeit: c' = c + 2,55 · Wert (Wert −100 … 100 %).</summary>
public sealed class BrightnessEffect : PointEffect
{
    public static readonly NumberParameter Amount = new("brightness", "EffectParam.Brightness", -100, 100, 0,
        UnitKey: "Unit.Percent");

    public override string Id => "brightness";

    public override IReadOnlyList<EffectParameter> Parameters => [Amount];

    protected override ColorMap Prepare(EffectSettings settings)
    {
        var delta = 2.55 * settings.Number(Amount.Id);
        return (r, g, b, _, _) => (r + delta, g + delta, b + delta);
    }
}

/// <summary>Kontrast: c' = (c − 127,5) · f + 127,5 mit f = ((100 + Wert) / 100)² (−100 → Grau, 0 → unverändert, 100 → f = 4).</summary>
public sealed class ContrastEffect : PointEffect
{
    public static readonly NumberParameter Amount = new("contrast", "EffectParam.Contrast", -100, 100, 0,
        UnitKey: "Unit.Percent");

    public override string Id => "contrast";

    public override IReadOnlyList<EffectParameter> Parameters => [Amount];

    protected override ColorMap Prepare(EffectSettings settings)
    {
        var f = Math.Pow((100 + settings.Number(Amount.Id)) / 100, 2);
        return (r, g, b, _, _) => ((r - 127.5) * f + 127.5, (g - 127.5) * f + 127.5, (b - 127.5) * f + 127.5);
    }
}

/// <summary>Farbton: Drehung des HSL-Farbtons um −180 … 180°; Sättigung und Helligkeit bleiben.</summary>
public sealed class HueEffect : PointEffect
{
    public static readonly NumberParameter Angle = new("hue", "EffectParam.Hue", -180, 180, 0, UnitKey: "Unit.Degree");

    public override string Id => "hue";

    public override IReadOnlyList<EffectParameter> Parameters => [Angle];

    protected override ColorMap Prepare(EffectSettings settings)
    {
        var angle = settings.Number(Angle.Id);
        return (r, g, b, _, _) =>
        {
            var (h, s, l) = PixelMath.ToHsl(r, g, b);
            return PixelMath.FromHsl(h + angle, s, l);
        };
    }
}

/// <summary>Sättigung: HSL-Sättigung · (1 + Wert/100), begrenzt auf 0–1 (−100 % → Grau gleicher HSL-Helligkeit).</summary>
public sealed class SaturationEffect : PointEffect
{
    public static readonly NumberParameter Amount = new("saturation", "EffectParam.Saturation", -100, 100, 0,
        UnitKey: "Unit.Percent");

    public override string Id => "saturation";

    public override IReadOnlyList<EffectParameter> Parameters => [Amount];

    protected override ColorMap Prepare(EffectSettings settings)
    {
        var factor = 1 + settings.Number(Amount.Id) / 100;
        return (r, g, b, _, _) =>
        {
            var (h, s, l) = PixelMath.ToHsl(r, g, b);
            return PixelMath.FromHsl(h, Math.Clamp(s * factor, 0, 1), l);
        };
    }
}

/// <summary>Invertieren: c' = 255 − c (gerade Farbe), Alpha bleibt.</summary>
public sealed class InvertEffect : PointEffect
{
    public override string Id => "invert";

    protected override ColorMap Prepare(EffectSettings settings) => (r, g, b, _, _) => (255 - r, 255 - g, 255 - b);
}

/// <summary>Schwarzweiß: Grauwert = 0,299 R + 0,587 G + 0,114 B (BT.601).</summary>
public sealed class BlackWhiteEffect : PointEffect
{
    public override string Id => "blackWhite";

    protected override ColorMap Prepare(EffectSettings settings) => (r, g, b, _, _) =>
    {
        var y = PixelMath.Luma(r, g, b);
        return (y, y, y);
    };
}

/// <summary>
/// Sepia: übliche Sepia-Matrix (R' = 0,393 R + 0,769 G + 0,189 B; G' = 0,349 R + 0,686 G + 0,168 B;
/// B' = 0,272 R + 0,534 G + 0,131 B), mit Intensität 0–100 % zwischen Original und Sepia gemischt.
/// </summary>
public sealed class SepiaEffect : PointEffect
{
    public static readonly NumberParameter Intensity = new("intensity", "EffectParam.Intensity", 0, 100, 100,
        UnitKey: "Unit.Percent");

    public override string Id => "sepia";

    public override IReadOnlyList<EffectParameter> Parameters => [Intensity];

    protected override ColorMap Prepare(EffectSettings settings)
    {
        var k = settings.Number(Intensity.Id) / 100;
        return (r, g, b, _, _) =>
        {
            var sr = Math.Min(255, 0.393 * r + 0.769 * g + 0.189 * b);
            var sg = Math.Min(255, 0.349 * r + 0.686 * g + 0.168 * b);
            var sb = Math.Min(255, 0.272 * r + 0.534 * g + 0.131 * b);
            return (r + (sr - r) * k, g + (sg - g) * k, b + (sb - b) * k);
        };
    }
}

/// <summary>Farbkanalanpassung: jeder Kanal wird mit seinem Faktor 0–200 % multipliziert.</summary>
public sealed class ChannelEffect : PointEffect
{
    public static readonly NumberParameter Red = new("red", "EffectParam.Red", 0, 200, 100, UnitKey: "Unit.Percent");
    public static readonly NumberParameter Green = new("green", "EffectParam.Green", 0, 200, 100, UnitKey: "Unit.Percent");
    public static readonly NumberParameter Blue = new("blue", "EffectParam.Blue", 0, 200, 100, UnitKey: "Unit.Percent");

    public override string Id => "channels";

    public override IReadOnlyList<EffectParameter> Parameters => [Red, Green, Blue];

    protected override ColorMap Prepare(EffectSettings settings)
    {
        var fr = settings.Number(Red.Id) / 100;
        var fg = settings.Number(Green.Id) / 100;
        var fb = settings.Number(Blue.Id) / 100;
        return (r, g, b, _, _) => (r * fr, g * fg, b * fb);
    }
}

/// <summary>
/// Tonwertkorrektur: t = (c − Schwarz) / (Weiß − Schwarz) begrenzt auf 0–1, dann t^(1/Gamma),
/// c' = Ausgabe-Schwarz + (Ausgabe-Weiß − Ausgabe-Schwarz) · t. Regel: Eingabe-Schwarz &lt; Eingabe-Weiß.
/// </summary>
public sealed class LevelsEffect : PointEffect
{
    public static readonly NumberParameter InputBlack = new("inBlack", "EffectParam.InputBlack", 0, 254, 0);
    public static readonly NumberParameter InputWhite = new("inWhite", "EffectParam.InputWhite", 1, 255, 255);
    public static readonly NumberParameter Gamma = new("gamma", "EffectParam.Gamma", 0.1, 10, 1, 0.01, 2);
    public static readonly NumberParameter OutputBlack = new("outBlack", "EffectParam.OutputBlack", 0, 255, 0);
    public static readonly NumberParameter OutputWhite = new("outWhite", "EffectParam.OutputWhite", 0, 255, 255);

    public override string Id => "levels";

    public override IReadOnlyList<EffectParameter> Parameters => [InputBlack, InputWhite, Gamma, OutputBlack, OutputWhite];

    public override IEnumerable<EffectValidationError> ValidateRules(EffectSettings settings)
    {
        if (settings.Number(InputBlack.Id) >= settings.Number(InputWhite.Id))
        {
            yield return new EffectValidationError(InputWhite.Id, "Effect.Error.LevelsInput");
        }
    }

    protected override ColorMap Prepare(EffectSettings settings)
    {
        var black = settings.Number(InputBlack.Id);
        var range = settings.Number(InputWhite.Id) - black;
        var exponent = 1 / settings.Number(Gamma.Id);
        var outBlack = settings.Number(OutputBlack.Id);
        var outRange = settings.Number(OutputWhite.Id) - outBlack;
        double Map(double c) => outBlack + outRange * Math.Pow(Math.Clamp((c - black) / range, 0, 1), exponent);
        return (r, g, b, _, _) => (Map(r), Map(g), Map(b));
    }
}

/// <summary>
/// Mehrere Pixel-Effekte nacheinander mit gemeinsamen Parametern (z. B. „Helligkeit/Kontrast"). Zwischenergebnisse
/// werden je Schritt auf 8 Bit gerundet, wie bei getrennter Anwendung.
/// </summary>
public sealed class CompositeEffect(string id, EffectCategory category, params PointEffect[] parts) : EffectBase
{
    public override string Id { get; } = id;

    public override EffectCategory Category { get; } = category;

    public IReadOnlyList<PointEffect> Parts { get; } = parts;

    public override IReadOnlyList<EffectParameter> Parameters { get; } = parts.SelectMany(p => p.Parameters).ToList();

    public override IEnumerable<EffectValidationError> ValidateRules(EffectSettings settings) =>
        Parts.SelectMany(p => p.ValidateRules(settings));

    public override void Render(EffectContext context)
    {
        var source = context.Source;
        var region = context.Region;
        for (var i = 0; i < Parts.Count; i++)
        {
            var last = i == Parts.Count - 1;
            var output = last ? context.Output : PixelBuffer.Create(region.Width, region.Height);
            Parts[i].Render(new EffectContext(source, region, output, context.Settings, context.Cancellation));
            source = output;
            region = output.Bounds;
        }
    }
}
