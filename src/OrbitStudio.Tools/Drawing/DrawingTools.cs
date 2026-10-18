using OrbitStudio.Domain.Imaging;
using OrbitStudio.Tools.Painting;

namespace OrbitStudio.Tools.Drawing;

/// <summary>Gemeinsame Startprüfung für Werkzeuge, die Pixel (oder die Maske) der aktiven Ebene verändern.</summary>
public static class PaintTarget
{
    public static PaintSession? Start(ToolContext context, PaintMode mode, ColorRgba color, double opacity) =>
        CanPaint(context) ? new PaintSession(context.Document, context.Document.ActivePixelLayer!, mode, color, opacity,
            context.EditMask) : null;

    /// <summary>Prüft, ob die aktive Ebene (bzw. ihre Maske) bemalt werden darf; meldet sonst den Grund.</summary>
    public static bool CanPaint(ToolContext context)
    {
        var layer = context.Document.ActivePixelLayer;
        if (layer is null)
        {
            context.Report(ToolProblem.NoPixelLayer);
            return false;
        }

        if (layer.IsEffectivelyLocked)
        {
            context.Report(ToolProblem.LayerLocked);
            return false;
        }

        if (context.EditMask && layer.Mask is null)
        {
            context.Report(ToolProblem.NoMask);
            return false;
        }

        return true;
    }

    /// <summary>Umschließendes Pixelrechteck zweier Punkte, um <paramref name="margin"/> erweitert.</summary>
    public static IntRect Bounds(PointD a, PointD b, double margin)
    {
        var left = (int)Math.Floor(Math.Min(a.X, b.X) - margin);
        var top = (int)Math.Floor(Math.Min(a.Y, b.Y) - margin);
        var right = (int)Math.Ceiling(Math.Max(a.X, b.X) + margin);
        var bottom = (int)Math.Ceiling(Math.Max(a.Y, b.Y) + margin);
        return new IntRect(left, top, right - left + 1, bottom - top + 1);
    }
}

/// <summary>
/// Freihandzüge (Pinsel, Bleistift, Radierer, Freihand): Abdeckung = Profil(Abstand zur gezeichneten Strecke).
/// So entsteht ein lückenloser Zug ohne Stempelabstände; überlappende Teile addieren ihre Deckkraft nicht.
/// </summary>
public abstract class StrokeTool : ToolBase
{
    private PaintSession? _session;
    private PointD _last;

    public override bool IsActiveOperation => _session is not null;

    public override ToolCursor Cursor => ToolCursor.None;

    protected abstract double Diameter { get; }

    protected virtual double Hardness => 1.0;

    protected virtual bool Antialias => true;

    protected virtual double OpacityValue => 1.0;

    protected virtual PaintMode Mode => PaintMode.Paint;

    public override void PointerDown(ToolContext context, ToolPointerEvent e)
    {
        Commit(context);
        _session = PaintTarget.Start(context, Mode, context.Colors.For(e.Button), OpacityValue);
        if (_session is null)
        {
            return;
        }

        _last = e.Position;
        Stroke(_last, _last);
    }

    public override void PointerMove(ToolContext context, ToolPointerEvent e)
    {
        Hover(context, e.Position);
        if (_session is null)
        {
            return;
        }

        Stroke(_last, e.Position);
        _last = e.Position;
    }

    public override void PointerUp(ToolContext context, ToolPointerEvent e) => Commit(context);

    public override void Hover(ToolContext context, PointD position) =>
        SetOverlay([new OverlayCircle(position, Math.Max(0.5, Diameter / 2))]);

    public override void Commit(ToolContext context)
    {
        _session?.Commit(context, NameKey);
        _session = null;
    }

    public override void Cancel(ToolContext context)
    {
        _session?.Cancel();
        _session = null;
    }

    private void Stroke(PointD a, PointD b)
    {
        var radius = Diameter / 2;
        var bounds = PaintTarget.Bounds(a, b, radius + 1);
        var hardness = Hardness;
        var antialias = Antialias;
        _session!.Rasterize(bounds,
            p => Coverage.BrushProfile(Coverage.DistanceToSegment(p, a, b), radius, hardness, antialias));
        _session.Apply(bounds);
    }
}

public sealed class BrushTool : StrokeTool
{
    public NumberOption Size { get; } = new("Opt.Size", 1, 500, 12);

    public NumberOption HardnessOption { get; } = new("Opt.Hardness", 0, 100, 75, unitKey: "Unit.Percent");

    public NumberOption Opacity { get; } = new("Opt.Opacity", 0, 100, 100, unitKey: "Unit.Percent");

    public ToggleOption AntialiasOption { get; } = new("Opt.Antialias", true);

    public override string Id => "brush";

    public override ToolCategory Category => ToolCategory.Draw;

    public override string Glyph => "🖌";

    public override string? Shortcut => "B";

    public override IReadOnlyList<ToolOption> Options => [Size, HardnessOption, Opacity, AntialiasOption];

    protected override double Diameter => Size.Value;

    protected override double Hardness => HardnessOption.Value / 100;

    protected override bool Antialias => AntialiasOption.Value;

    protected override double OpacityValue => Opacity.Value / 100;
}

/// <summary>Harte, nicht geglättete Pixel (Pixelmittelpunkt innerhalb des Zuges).</summary>
public sealed class PencilTool : StrokeTool
{
    public NumberOption Size { get; } = new("Opt.Size", 1, 100, 1);

    public override string Id => "pencil";

    public override ToolCategory Category => ToolCategory.Draw;

    public override string Glyph => "✏";

    public override string? Shortcut => "P";

    public override IReadOnlyList<ToolOption> Options => [Size];

    public override ToolCursor Cursor => ToolCursor.Cross;

    protected override double Diameter => Size.Value;

    protected override bool Antialias => false;
}

public sealed class EraserTool : StrokeTool
{
    public NumberOption Size { get; } = new("Opt.Size", 1, 500, 20);

    public NumberOption HardnessOption { get; } = new("Opt.Hardness", 0, 100, 75, unitKey: "Unit.Percent");

    public NumberOption Opacity { get; } = new("Opt.Opacity", 0, 100, 100, unitKey: "Unit.Percent");

    public override string Id => "eraser";

    public override ToolCategory Category => ToolCategory.Draw;

    public override string Glyph => "⌫";

    public override string? Shortcut => "E";

    public override IReadOnlyList<ToolOption> Options => [Size, HardnessOption, Opacity];

    protected override double Diameter => Size.Value;

    protected override double Hardness => HardnessOption.Value / 100;

    protected override double OpacityValue => Opacity.Value / 100;

    protected override PaintMode Mode => PaintMode.Erase;
}

/// <summary>Freihandlinie mit fester Linienbreite und harter, geglätteter Kante.</summary>
public sealed class FreehandTool : StrokeTool
{
    public NumberOption LineWidth { get; } = new("Opt.LineWidth", 1, 200, 3);

    public NumberOption Opacity { get; } = new("Opt.Opacity", 0, 100, 100, unitKey: "Unit.Percent");

    public ToggleOption AntialiasOption { get; } = new("Opt.Antialias", true);

    public override string Id => "freehand";

    public override ToolCategory Category => ToolCategory.Draw;

    public override string Glyph => "〰";

    public override string? Shortcut => "F";

    public override IReadOnlyList<ToolOption> Options => [LineWidth, Opacity, AntialiasOption];

    protected override double Diameter => LineWidth.Value;

    protected override bool Antialias => AntialiasOption.Value;

    protected override double OpacityValue => Opacity.Value / 100;
}
