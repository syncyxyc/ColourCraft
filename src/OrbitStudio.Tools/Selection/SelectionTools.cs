using OrbitStudio.Domain.Documents;
using OrbitStudio.Domain.Imaging;
using OrbitStudio.Domain.Selection;
using OrbitStudio.History;
using OrbitStudio.Rendering;

namespace OrbitStudio.Tools.Selection;

/// <summary>Auswahländerungen mit Historie (Werkzeuge und Menübefehle).</summary>
public static class SelectionEditing
{
    /// <summary>Setzt die Auswahl und legt einen Historieneintrag an (nur bei tatsächlicher Änderung).</summary>
    public static void Apply(ImageDocument document, DocumentHistory history, SelectionMask? selection, string nameKey)
    {
        var before = document.Selection;
        var after = selection is null || selection.IsEmpty ? null : selection;
        if (before is null && after is null)
        {
            return;
        }

        if (before is not null && after is not null && before.ContentEquals(after))
        {
            return;
        }

        document.SetSelection(after);
        history.Push(new SelectionEntry(nameKey, before, after));
    }

    public static void SelectAll(ImageDocument d, DocumentHistory h) =>
        Apply(d, h, SelectionMask.CreateFull(d.Width, d.Height), "History.SelectAll");

    public static void Deselect(ImageDocument d, DocumentHistory h) => Apply(d, h, null, "History.Deselect");

    /// <summary>Umkehren; ohne Auswahl entspricht das „nichts ausgewählt" → alles ausgewählt wird nicht erzeugt.</summary>
    public static void Invert(ImageDocument d, DocumentHistory h)
    {
        var mask = d.Selection?.Clone() ?? SelectionMask.CreateEmpty(d.Width, d.Height);
        mask.Invert();
        Apply(d, h, mask, "History.InvertSelection");
    }

    public static void Grow(ImageDocument d, DocumentHistory h, double radius)
    {
        if (d.Selection is { } s)
        {
            Apply(d, h, SelectionShapes.GrowOrShrink(s, Math.Abs(radius)), "History.GrowSelection");
        }
    }

    public static void Shrink(ImageDocument d, DocumentHistory h, double radius)
    {
        if (d.Selection is { } s)
        {
            Apply(d, h, SelectionShapes.GrowOrShrink(s, -Math.Abs(radius)), "History.ShrinkSelection");
        }
    }

    public static void FromLayerAlpha(ImageDocument d, DocumentHistory h)
    {
        if (d.ActivePixelLayer is { } layer)
        {
            Apply(d, h, SelectionShapes.FromAlpha(layer.Pixels), "History.SelectionFromAlpha");
        }
    }
}

/// <summary>Basis der Auswahlwerkzeuge: Verknüpfungsmodus aus Option oder Tasten (Umschalt = hinzufügen,
/// Alt = abziehen, Umschalt+Alt = Schnittmenge).</summary>
public abstract class SelectionToolBase : ToolBase
{
    public ChoiceOption Mode { get; } = new("Opt.SelectionMode",
        ["Choice.Replace", "Choice.Add", "Choice.Subtract", "Choice.Intersect"]);

    public override ToolCategory Category => ToolCategory.Selection;

    protected SelectionCombineMode CombineMode(ToolModifiers modifiers) =>
        (modifiers & (ToolModifiers.Shift | ToolModifiers.Alt)) switch
        {
            ToolModifiers.Shift | ToolModifiers.Alt => SelectionCombineMode.Intersect,
            ToolModifiers.Shift => SelectionCombineMode.Add,
            ToolModifiers.Alt => SelectionCombineMode.Subtract,
            _ => (SelectionCombineMode)Mode.Index,
        };

    protected void ApplyShape(ToolContext context, SelectionMask shape, SelectionCombineMode mode) =>
        SelectionEditing.Apply(context.Document, context.History,
            SelectionMask.Combine(context.Document.Selection, shape, mode), NameKey);
}

/// <summary>Rechteck- und Ellipsenauswahl durch Ziehen. Ein Klick ohne Ziehen im Modus „Ersetzen" hebt die Auswahl auf.</summary>
public abstract class DragSelectionTool : SelectionToolBase
{
    private PointD? _start;
    private SelectionCombineMode _mode;

    public override IReadOnlyList<ToolOption> Options => [Mode];

    public override bool IsActiveOperation => _start is not null;

    public override void PointerDown(ToolContext context, ToolPointerEvent e)
    {
        _start = e.Position;
        _mode = CombineMode(e.Modifiers);
    }

    public override void PointerMove(ToolContext context, ToolPointerEvent e)
    {
        if (_start is { } start)
        {
            SetOverlay([OverlayFor(Snap(start), Snap(e.Position))]);
        }
    }

    public override void PointerUp(ToolContext context, ToolPointerEvent e)
    {
        if (_start is not { } start)
        {
            return;
        }

        _start = null;
        ClearOverlay();
        var a = Snap(start);
        var b = Snap(e.Position);
        if (a.X == b.X || a.Y == b.Y)
        {
            if (_mode == SelectionCombineMode.Replace)
            {
                SelectionEditing.Deselect(context.Document, context.History);
            }

            return;
        }

        ApplyShape(context, CreateShape(context.Document.Width, context.Document.Height, a, b), _mode);
    }

    public override void Cancel(ToolContext context)
    {
        _start = null;
        ClearOverlay();
    }

    private static PointD Snap(PointD p) => new(Math.Round(p.X), Math.Round(p.Y));

    protected abstract OverlayShape OverlayFor(PointD a, PointD b);

    protected abstract SelectionMask CreateShape(int width, int height, PointD a, PointD b);
}

public sealed class RectangleSelectTool : DragSelectionTool
{
    public override string Id => "selectRectangle";

    public override string Glyph => "⬚";

    public override string? Shortcut => "M";

    protected override OverlayShape OverlayFor(PointD a, PointD b) => new OverlayRect(a, b);

    protected override SelectionMask CreateShape(int width, int height, PointD a, PointD b) =>
        SelectionShapes.Rectangle(width, height, a, b);
}

public sealed class EllipseSelectTool : DragSelectionTool
{
    public override string Id => "selectEllipse";

    public override string Glyph => "◌";

    public override string? Shortcut => "Shift+M";

    protected override OverlayShape OverlayFor(PointD a, PointD b) => new OverlayEllipse(a, b);

    protected override SelectionMask CreateShape(int width, int height, PointD a, PointD b) =>
        SelectionShapes.Ellipse(width, height, a, b);
}

/// <summary>Freihandauswahl: Umriss beim Ziehen, beim Loslassen geschlossen.</summary>
public sealed class LassoSelectTool : SelectionToolBase
{
    private List<PointD>? _points;
    private SelectionCombineMode _mode;

    public override string Id => "selectLasso";

    public override string Glyph => "➰";

    public override string? Shortcut => "S";

    public override IReadOnlyList<ToolOption> Options => [Mode];

    public override bool IsActiveOperation => _points is not null;

    public override void PointerDown(ToolContext context, ToolPointerEvent e)
    {
        _points = [e.Position];
        _mode = CombineMode(e.Modifiers);
    }

    public override void PointerMove(ToolContext context, ToolPointerEvent e)
    {
        if (_points is null)
        {
            return;
        }

        if ((e.Position - _points[^1]).Length >= 1)
        {
            _points.Add(e.Position);
            SetOverlay([new OverlayPolyline(_points.ToArray(), false)]);
        }
    }

    public override void PointerUp(ToolContext context, ToolPointerEvent e)
    {
        if (_points is null)
        {
            return;
        }

        var points = _points;
        _points = null;
        ClearOverlay();
        if (points.Count >= 3)
        {
            ApplyShape(context, SelectionShapes.Polygon(context.Document.Width, context.Document.Height, points), _mode);
        }
        else if (_mode == SelectionCombineMode.Replace)
        {
            SelectionEditing.Deselect(context.Document, context.History);
        }
    }

    public override void Cancel(ToolContext context)
    {
        _points = null;
        ClearOverlay();
    }
}

/// <summary>Polygonauswahl: Klicks setzen Eckpunkte; Doppelklick, Enter oder Klick auf den ersten Punkt schließt.</summary>
public sealed class PolygonSelectTool : SelectionToolBase
{
    private List<PointD>? _points;
    private SelectionCombineMode _mode;
    private PointD _cursor;

    public override string Id => "selectPolygon";

    public override string Glyph => "⬠";

    public override string? Shortcut => "Shift+S";

    public override IReadOnlyList<ToolOption> Options => [Mode];

    public override bool IsActiveOperation => _points is not null;

    /// <summary>Abstand (Dokumentpixel), ab dem ein Klick als „auf den ersten Punkt" gilt.</summary>
    public double CloseDistance { get; set; } = 4;

    public override void PointerDown(ToolContext context, ToolPointerEvent e)
    {
        if (_points is null)
        {
            _points = [e.Position];
            _mode = CombineMode(e.Modifiers);
        }
        else if (e.ClickCount >= 2 || (_points.Count >= 3 && (e.Position - _points[0]).Length <= CloseDistance))
        {
            Commit(context);
            return;
        }
        else
        {
            _points.Add(e.Position);
        }

        UpdateOverlay();
    }

    public override void Hover(ToolContext context, PointD position)
    {
        _cursor = position;
        if (_points is not null)
        {
            UpdateOverlay();
        }
    }

    public override void PointerMove(ToolContext context, ToolPointerEvent e) => Hover(context, e.Position);

    public override void Commit(ToolContext context)
    {
        if (_points is null)
        {
            return;
        }

        var points = _points;
        _points = null;
        ClearOverlay();
        if (points.Count >= 3)
        {
            ApplyShape(context, SelectionShapes.Polygon(context.Document.Width, context.Document.Height, points), _mode);
        }
    }

    public override void Cancel(ToolContext context)
    {
        _points = null;
        ClearOverlay();
    }

    private void UpdateOverlay() =>
        SetOverlay([new OverlayPolyline([.. _points!, _cursor], false), new OverlayHandle(_points![0], false)]);
}

/// <summary>Zauberstab (zusammenhängend) bzw. Farbbereich (im ganzen Bild) nach Farbähnlichkeit.</summary>
public sealed class MagicWandTool(bool contiguous) : SelectionToolBase
{
    public NumberOption Tolerance { get; } = new("Opt.Tolerance", 0, 100, 10, unitKey: "Unit.Percent");

    public ChoiceOption SampleFrom { get; } = new("Opt.SampleFrom", ["Choice.Layer", "Choice.Image"]);

    public bool Contiguous { get; } = contiguous;

    public override string Id => Contiguous ? "magicWand" : "colorRange";

    public override string Glyph => Contiguous ? "✨" : "🎨";

    public override string? Shortcut => Contiguous ? "W" : "Shift+W";

    public override IReadOnlyList<ToolOption> Options => [Mode, Tolerance, SampleFrom];

    public override void PointerDown(ToolContext context, ToolPointerEvent e)
    {
        var x = (int)Math.Floor(e.Position.X);
        var y = (int)Math.Floor(e.Position.Y);
        if (!context.Document.Bounds.Contains(x, y))
        {
            return;
        }

        var source = SampleFrom.Index == 0 && context.Document.ActivePixelLayer is { } layer
            ? layer.Pixels
            : LayerCompositor.Composite(context.Document);
        var shape = SelectionShapes.SimilarColor(source, x, y, Tolerance.Value, Contiguous);
        ApplyShape(context, shape, CombineMode(e.Modifiers));
    }
}
