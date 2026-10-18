using OrbitStudio.Domain.Imaging;
using OrbitStudio.Tools.Painting;

namespace OrbitStudio.Tools.Drawing;

/// <summary>
/// Formwerkzeuge: Die Vorschau wird bei jeder Mausbewegung aus Sicherung und neuer Abdeckung neu berechnet,
/// der Originalzustand bleibt bis zum Loslassen erhalten. Escape bricht ab.
/// </summary>
public abstract class ShapeTool : ToolBase
{
    private PaintSession? _session;
    private PointD _start;
    private IntRect _lastBounds = IntRect.Empty;

    public override bool IsActiveOperation => _session is not null;

    public override void PointerDown(ToolContext context, ToolPointerEvent e)
    {
        Commit(context);
        _session = PaintTarget.Start(context, PaintMode.Paint, context.Colors.For(e.Button), OpacityValue);
        if (_session is null)
        {
            return;
        }

        _start = SnapStart(e.Position);
        _lastBounds = IntRect.Empty;
        Update(e);
    }

    public override void PointerMove(ToolContext context, ToolPointerEvent e)
    {
        if (_session is not null)
        {
            Update(e);
        }
    }

    public override void PointerUp(ToolContext context, ToolPointerEvent e)
    {
        if (_session is null)
        {
            return;
        }

        Update(e);
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

    protected virtual double OpacityValue => 1.0;

    protected virtual PointD SnapStart(PointD p) => p;

    /// <summary>Endpunkt unter Berücksichtigung von Umschalt (Einrasten) bestimmen.</summary>
    protected abstract PointD Constrain(PointD start, PointD end, bool shift);

    /// <summary>Betroffener Bereich der Form.</summary>
    protected abstract IntRect ShapeBounds(PointD start, PointD end);

    /// <summary>Abdeckung am Pixelmittelpunkt.</summary>
    protected abstract Func<PointD, double> ShapeCoverage(PointD start, PointD end);

    private void Update(ToolPointerEvent e)
    {
        var end = Constrain(_start, e.Position, e.Shift);
        var bounds = ShapeBounds(_start, end);
        _session!.ClearCoverage(_lastBounds);
        _session.Rasterize(bounds, ShapeCoverage(_start, end));
        _session.Apply(_lastBounds.Union(bounds));
        _lastBounds = bounds;
    }
}

public sealed class LineTool : ShapeTool
{
    public NumberOption LineWidth { get; } = new("Opt.LineWidth", 1, 200, 3);

    public NumberOption Opacity { get; } = new("Opt.Opacity", 0, 100, 100, unitKey: "Unit.Percent");

    public ToggleOption AntialiasOption { get; } = new("Opt.Antialias", true);

    public override string Id => "line";

    public override ToolCategory Category => ToolCategory.Draw;

    public override string Glyph => "╱";

    public override string? Shortcut => "L";

    public override IReadOnlyList<ToolOption> Options => [LineWidth, Opacity, AntialiasOption];

    protected override double OpacityValue => Opacity.Value / 100;

    /// <summary>Umschalt: Winkel rastet in 15°-Schritten ein.</summary>
    protected override PointD Constrain(PointD start, PointD end, bool shift)
    {
        if (!shift)
        {
            return end;
        }

        var d = end - start;
        var length = d.Length;
        var angle = Math.Round(Math.Atan2(d.Y, d.X) / (Math.PI / 12)) * (Math.PI / 12);
        return new PointD(start.X + length * Math.Cos(angle), start.Y + length * Math.Sin(angle));
    }

    protected override IntRect ShapeBounds(PointD start, PointD end) =>
        PaintTarget.Bounds(start, end, LineWidth.Value / 2 + 1);

    protected override Func<PointD, double> ShapeCoverage(PointD start, PointD end)
    {
        var radius = LineWidth.Value / 2;
        var antialias = AntialiasOption.Value;
        return p => Coverage.BrushProfile(Coverage.DistanceToSegment(p, start, end), radius, 1, antialias);
    }
}

/// <summary>Rechteck oder Ellipse in einem auf ganze Pixel gerundeten Rahmen (Umschalt: Quadrat bzw. Kreis).</summary>
public abstract class BoxShapeTool(bool filled) : ShapeTool
{
    public bool Filled { get; } = filled;

    public NumberOption LineWidth { get; } = new("Opt.LineWidth", 1, 200, 3);

    public NumberOption Opacity { get; } = new("Opt.Opacity", 0, 100, 100, unitKey: "Unit.Percent");

    public ToggleOption AntialiasOption { get; } = new("Opt.Antialias", true);

    public override ToolCategory Category => ToolCategory.Draw;

    public override IReadOnlyList<ToolOption> Options =>
        Filled ? [Opacity, AntialiasOption] : [LineWidth, Opacity, AntialiasOption];

    protected override double OpacityValue => Opacity.Value / 100;

    protected override PointD SnapStart(PointD p) => new(Math.Round(p.X), Math.Round(p.Y));

    protected override PointD Constrain(PointD start, PointD end, bool shift)
    {
        var x = Math.Round(end.X);
        var y = Math.Round(end.Y);
        if (shift)
        {
            var size = Math.Max(Math.Abs(x - start.X), Math.Abs(y - start.Y));
            x = start.X + Math.Sign(x - start.X == 0 ? 1 : x - start.X) * size;
            y = start.Y + Math.Sign(y - start.Y == 0 ? 1 : y - start.Y) * size;
        }

        return new PointD(x, y);
    }

    protected override IntRect ShapeBounds(PointD start, PointD end) => PaintTarget.Bounds(start, end, 1);

    protected static (double Left, double Top, double Right, double Bottom) Box(PointD a, PointD b) =>
        (Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Max(a.X, b.X), Math.Max(a.Y, b.Y));
}

public sealed class RectangleTool(bool filled) : BoxShapeTool(filled)
{
    public override string Id => Filled ? "rectangleFilled" : "rectangle";

    public override string Glyph => Filled ? "■" : "□";

    public override string? Shortcut => Filled ? "Shift+R" : "R";

    protected override Func<PointD, double> ShapeCoverage(PointD start, PointD end)
    {
        var (l, t, r, b) = Box(start, end);
        var antialias = AntialiasOption.Value;
        if (Filled)
        {
            return p => Coverage.FromSignedDistance(Coverage.SignedDistanceToBox(p, l, t, r, b), antialias);
        }

        // Kontur innerhalb des Rahmens: äußeres minus inneres Rechteck
        var w = LineWidth.Value;
        var hasInner = r - l > 2 * w && b - t > 2 * w;
        return p =>
        {
            var outer = Coverage.FromSignedDistance(Coverage.SignedDistanceToBox(p, l, t, r, b), antialias);
            var inner = hasInner
                ? Coverage.FromSignedDistance(Coverage.SignedDistanceToBox(p, l + w, t + w, r - w, b - w), antialias)
                : 0;
            return Math.Max(0, outer - inner);
        };
    }
}

public sealed class EllipseTool(bool filled) : BoxShapeTool(filled)
{
    public override string Id => Filled ? "ellipseFilled" : "ellipse";

    public override string Glyph => Filled ? "●" : "○";

    public override string? Shortcut => Filled ? "Shift+O" : "O";

    protected override Func<PointD, double> ShapeCoverage(PointD start, PointD end)
    {
        var (l, t, r, b) = Box(start, end);
        var cx = (l + r) / 2;
        var cy = (t + b) / 2;
        var rx = (r - l) / 2;
        var ry = (b - t) / 2;
        var antialias = AntialiasOption.Value;
        if (Filled)
        {
            return p => Coverage.FromSignedDistance(Coverage.SignedDistanceToEllipse(p, cx, cy, rx, ry), antialias);
        }

        var w = LineWidth.Value;
        var hasInner = rx > w && ry > w;
        return p =>
        {
            var outer = Coverage.FromSignedDistance(Coverage.SignedDistanceToEllipse(p, cx, cy, rx, ry), antialias);
            var inner = hasInner
                ? Coverage.FromSignedDistance(Coverage.SignedDistanceToEllipse(p, cx, cy, rx - w, ry - w), antialias)
                : 0;
            return Math.Max(0, outer - inner);
        };
    }
}
