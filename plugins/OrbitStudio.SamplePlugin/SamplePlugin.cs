using OrbitStudio.Effects;
using OrbitStudio.Plugin.Abstractions;

namespace OrbitStudio.SamplePlugin;

/// <summary>Einstiegspunkt des Beispielplugins: meldet den Effekt „Tonwertstufen“ an.</summary>
public sealed class SamplePlugin : IOrbitPlugin
{
    public void Register(IPluginHost host) => host.RegisterEffect(new PosterizeEffect());
}

/// <summary>
/// Tonwertstufen (Posterisieren): Jeder Farbkanal wird auf <c>Stufen</c> gleichmäßig verteilte Werte gerundet,
/// c' = Runden(Runden(c / 255 · (n − 1)) · 255 / (n − 1)). Alpha bleibt unverändert.
/// </summary>
public sealed class PosterizeEffect : PointEffect
{
    public static readonly NumberParameter Levels = new("levels", "EffectParam.Levels", 2, 16, 4);

    public override string Id => "orbit-sample.posterize";

    public override IReadOnlyList<EffectParameter> Parameters => [Levels];

    protected override ColorMap Prepare(EffectSettings settings)
    {
        var steps = settings.Integer(Levels.Id) - 1;
        double Map(double c) => Math.Round(Math.Round(c / 255 * steps, MidpointRounding.AwayFromZero) * 255.0 / steps,
            MidpointRounding.AwayFromZero);
        return (r, g, b, _, _) => (Map(r), Map(g), Map(b));
    }
}
