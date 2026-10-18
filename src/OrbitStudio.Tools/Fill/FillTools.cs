using OrbitStudio.Domain.Imaging;
using OrbitStudio.Domain.Selection;
using OrbitStudio.Rendering;
using OrbitStudio.Tools.Drawing;
using OrbitStudio.Tools.Painting;

namespace OrbitStudio.Tools.Fill;

/// <summary>
/// Farbeimer: füllt Pixel ähnlicher Farbe mit Vorder- (links) bzw. Hintergrundfarbe (rechts), Source-over.
/// Ähnlichkeit: größte Kanalabweichung (gerades RGBA) ≤ Toleranz. Begrenzt auf Auswahl und Bildgrenzen.
/// </summary>
public sealed class BucketTool : ToolBase
{
    public NumberOption Tolerance { get; } = new("Opt.Tolerance", 0, 100, 10, unitKey: "Unit.Percent");

    public ChoiceOption FloodMode { get; } = new("Opt.FloodMode", ["Choice.Contiguous", "Choice.Global"]);

    public ChoiceOption SampleFrom { get; } = new("Opt.SampleFrom", ["Choice.Layer", "Choice.Image"]);

    public NumberOption Opacity { get; } = new("Opt.Opacity", 0, 100, 100, unitKey: "Unit.Percent");

    public override string Id => "bucket";

    public override ToolCategory Category => ToolCategory.Fill;

    public override string Glyph => "▧";

    public override string? Shortcut => "G";

    public override IReadOnlyList<ToolOption> Options => [Tolerance, FloodMode, SampleFrom, Opacity];

    public override void PointerDown(ToolContext context, ToolPointerEvent e)
    {
        var x = (int)Math.Floor(e.Position.X);
        var y = (int)Math.Floor(e.Position.Y);
        if (!context.Document.Bounds.Contains(x, y))
        {
            return;
        }

        var session = PaintTarget.Start(context, PaintMode.Paint, context.Colors.For(e.Button), Opacity.Value / 100);
        if (session is null)
        {
            return;
        }

        // Bereich wird vor dem Malen bestimmt; die Ebene selbst dient daher direkt als Quelle.
        var source = SampleFrom.Index == 0
            ? session.Layer.Pixels
            : LayerCompositor.Composite(context.Document);
        var region = SelectionShapes.SimilarColor(source, x, y, Tolerance.Value, FloodMode.Index == 0);
        var bounds = region.Bounds();
        session.Rasterize(bounds, p => region.Get((int)p.X, (int)p.Y) / 255.0);
        session.Apply(bounds);
        session.Commit(context, NameKey);
    }
}

/// <summary>Pipette: übernimmt die Farbe unter dem Zeiger als Vorder- (links) bzw. Hintergrundfarbe (rechts).</summary>
public sealed class PipetteTool : ToolBase
{
    private bool _active;
    private PointerButton _button;

    public ChoiceOption SampleFrom { get; } = new("Opt.SampleFrom", ["Choice.Layer", "Choice.Image"], index: 1);

    public override string Id => "pipette";

    public override ToolCategory Category => ToolCategory.Fill;

    public override string Glyph => "💧";

    public override string? Shortcut => "I";

    public override IReadOnlyList<ToolOption> Options => [SampleFrom];

    public override bool IsActiveOperation => _active;

    public override void PointerDown(ToolContext context, ToolPointerEvent e)
    {
        _active = true;
        _button = e.Button;
        Pick(context, e.Position);
    }

    public override void PointerMove(ToolContext context, ToolPointerEvent e)
    {
        if (_active)
        {
            Pick(context, e.Position);
        }
    }

    public override void PointerUp(ToolContext context, ToolPointerEvent e) => _active = false;

    public override void Cancel(ToolContext context) => _active = false;

    public override void Commit(ToolContext context) => _active = false;

    private void Pick(ToolContext context, PointD position)
    {
        var x = (int)Math.Floor(position.X);
        var y = (int)Math.Floor(position.Y);
        if (!context.Document.Bounds.Contains(x, y))
        {
            return;
        }

        Bgra32 pixel;
        if (SampleFrom.Index == 0 && context.Document.ActivePixelLayer is { } layer)
        {
            pixel = layer.Pixels.GetPixel(x, y);
        }
        else
        {
            pixel = LayerCompositor.CompositePixel(context.Document.Root, x, y);
        }

        var color = ColorRgba.FromPremultiplied(pixel);
        if (_button == PointerButton.Primary)
        {
            context.Colors.Primary = color;
        }
        else
        {
            context.Colors.Secondary = color;
        }
    }
}

/// <summary>
/// Verlauf von Vorder- zu Hintergrundfarbe (rechte Maustaste: umgekehrt), linear oder radial.
/// Interpolation im vormultiplierten Raum, Auftrag mit Source-over.
/// </summary>
public sealed class GradientTool : ToolBase
{
    private PaintSession? _session;
    private PointD _start;
    private ColorRgba _from;
    private ColorRgba _to;

    public ChoiceOption Type { get; } = new("Opt.GradientType", ["Choice.Linear", "Choice.Radial"]);

    public NumberOption Opacity { get; } = new("Opt.Opacity", 0, 100, 100, unitKey: "Unit.Percent");

    public override string Id => "gradient";

    public override ToolCategory Category => ToolCategory.Fill;

    public override string Glyph => "◐";

    public override string? Shortcut => "Shift+G";

    public override IReadOnlyList<ToolOption> Options => [Type, Opacity];

    public override bool IsActiveOperation => _session is not null;

    public override void PointerDown(ToolContext context, ToolPointerEvent e)
    {
        Commit(context);
        _from = e.Button == PointerButton.Primary ? context.Colors.Primary : context.Colors.Secondary;
        _to = e.Button == PointerButton.Primary ? context.Colors.Secondary : context.Colors.Primary;
        _session = PaintTarget.Start(context, PaintMode.Paint, _from, Opacity.Value / 100);
        if (_session is null)
        {
            return;
        }

        _start = e.Position;
        _session.Rasterize(context.Document.Bounds, _ => 1);
    }

    public override void PointerMove(ToolContext context, ToolPointerEvent e)
    {
        if (_session is null)
        {
            return;
        }

        SetOverlay([new OverlayLine(_start, e.Position)]);
        Render(context, e.Position);
    }

    public override void PointerUp(ToolContext context, ToolPointerEvent e)
    {
        if (_session is null)
        {
            return;
        }

        Render(context, e.Position);
        Commit(context);
    }

    public override void Commit(ToolContext context)
    {
        _session?.Commit(context, NameKey);
        _session = null;
        ClearOverlay();
    }

    public override void Cancel(ToolContext context)
    {
        _session?.Cancel();
        _session = null;
        ClearOverlay();
    }

    /// <summary>Verlaufsposition t (0–1) eines Punkts.</summary>
    public static double Position(PointD p, PointD start, PointD end, bool radial)
    {
        var d = end - start;
        var lengthSquared = d.X * d.X + d.Y * d.Y;
        if (lengthSquared <= 0)
        {
            return 0;
        }

        return radial
            ? Math.Clamp((p - start).Length / Math.Sqrt(lengthSquared), 0, 1)
            : Math.Clamp(((p.X - start.X) * d.X + (p.Y - start.Y) * d.Y) / lengthSquared, 0, 1);
    }

    private void Render(ToolContext context, PointD end)
    {
        var from = _from.ToPremultiplied();
        var to = _to.ToPremultiplied();
        var radial = Type.Index == 1;
        var start = _start;
        _session!.SetColorFunction((x, y) =>
        {
            var t = Position(new PointD(x + 0.5, y + 0.5), start, end, radial);
            return (from.B + (to.B - from.B) * t, from.G + (to.G - from.G) * t, from.R + (to.R - from.R) * t,
                from.A + (to.A - from.A) * t);
        });
        _session.Apply(context.Document.Bounds);
    }
}
