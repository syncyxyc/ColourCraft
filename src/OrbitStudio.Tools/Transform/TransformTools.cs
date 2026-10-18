using OrbitStudio.Domain.Imaging;
using OrbitStudio.Domain.Layers;
using OrbitStudio.Domain.Selection;
using OrbitStudio.History;

namespace OrbitStudio.Tools.Transform;

/// <summary>
/// Verschiebt die ausgewählten Pixel der aktiven Ebene (ohne Auswahl: die ganze Ebene). Teilweise ausgewählte
/// Pixel werden anteilig verschoben. Die Auswahl wandert mit. Pfeiltasten verschieben um 1 Pixel (Umschalt: 10).
/// Escape während des Ziehens stellt den Ausgangszustand exakt wieder her.
/// </summary>
public sealed class MoveTool : ToolBase
{
    private MoveSession? _session;
    private PointD _start;

    public override string Id => "move";

    public override ToolCategory Category => ToolCategory.Transform;

    public override string Glyph => "✥";

    public override string? Shortcut => "V";

    public override ToolCursor Cursor => ToolCursor.Move;

    public override bool IsActiveOperation => _session is not null;

    public override void PointerDown(ToolContext context, ToolPointerEvent e)
    {
        Commit(context);
        _session = MoveSession.Start(context);
        _start = e.Position;
    }

    public override void PointerMove(ToolContext context, ToolPointerEvent e)
    {
        if (_session is null)
        {
            return;
        }

        var dx = (int)Math.Round(e.Position.X - _start.X);
        var dy = (int)Math.Round(e.Position.Y - _start.Y);
        if (e.Shift)
        {
            // Umschalt: nur waagerecht oder senkrecht
            if (Math.Abs(dx) >= Math.Abs(dy))
            {
                dy = 0;
            }
            else
            {
                dx = 0;
            }
        }

        _session.MoveTo(dx, dy);
        var b = _session.MovedBounds;
        SetOverlay([new OverlayRect(new PointD(b.X, b.Y), new PointD(b.Right, b.Bottom))]);
    }

    public override void PointerUp(ToolContext context, ToolPointerEvent e)
    {
        PointerMove(context, e);
        Commit(context);
    }

    public override bool Key(ToolContext context, ToolKey key, ToolModifiers modifiers)
    {
        var step = modifiers.HasFlag(ToolModifiers.Shift) ? 10 : 1;
        (int X, int Y)? delta = key switch
        {
            ToolKey.Left => (-step, 0),
            ToolKey.Right => (step, 0),
            ToolKey.Up => (0, -step),
            ToolKey.Down => (0, step),
            _ => null,
        };
        if (delta is { } d && _session is null)
        {
            var session = MoveSession.Start(context);
            if (session is not null)
            {
                session.MoveTo(d.X, d.Y);
                session.Commit(context, NameKey);
            }

            return true;
        }

        return base.Key(context, key, modifiers);
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

    /// <summary>Verschiebebearbeitung auf Basis einer Sicherung der Ebene.</summary>
    private sealed class MoveSession
    {
        private readonly ToolContext _context;
        private readonly PixelLayer _layer;
        private readonly PixelBuffer _backup;
        private readonly SelectionMask? _selection;
        private readonly IntRect _source;
        private int _dx;
        private int _dy;
        private IntRect _touched = IntRect.Empty;

        private MoveSession(ToolContext context, PixelLayer layer, SelectionMask? selection, IntRect source)
        {
            _context = context;
            _layer = layer;
            _backup = layer.Pixels.Clone();
            _selection = selection;
            _source = source;
        }

        public IntRect MovedBounds => new(_source.X + _dx, _source.Y + _dy, _source.Width, _source.Height);

        public static MoveSession? Start(ToolContext context)
        {
            var document = context.Document;
            if (document.ActivePixelLayer is not { } layer)
            {
                context.Report(ToolProblem.NoPixelLayer);
                return null;
            }

            if (layer.IsEffectivelyLocked)
            {
                context.Report(ToolProblem.LayerLocked);
                return null;
            }

            var source = document.Selection?.Bounds() ?? document.Bounds;
            if (source.IsEmpty)
            {
                context.Report(ToolProblem.NothingToMove);
                return null;
            }

            return new MoveSession(context, layer, document.Selection, source);
        }

        private double Amount(int x, int y) => _selection is null ? 1.0 : _selection.Get(x, y) / 255.0;

        public void MoveTo(int dx, int dy)
        {
            _dx = dx;
            _dy = dy;
            var bounds = _context.Document.Bounds;
            var affected = _source.Union(MovedBounds).Intersect(bounds);
            var region = _touched.Union(affected).Intersect(bounds);

            for (var y = region.Y; y < region.Bottom; y++)
            {
                var target = _layer.Pixels.GetRow(y);
                var backupRow = _backup.GetRow(y);
                for (var x = region.X; x < region.Right; x++)
                {
                    var i = x * 4;
                    // Grundlage: Original, an der Quellstelle um den ausgewählten Anteil ausgeschnitten
                    var keep = _source.Contains(x, y) ? 1 - Amount(x, y) : 1;
                    double b = backupRow[i] * keep, g = backupRow[i + 1] * keep, r = backupRow[i + 2] * keep,
                        a = backupRow[i + 3] * keep;

                    // Verschobener Inhalt darüber (Source-over)
                    var sx = x - dx;
                    var sy = y - dy;
                    if (_source.Contains(sx, sy))
                    {
                        var amount = Amount(sx, sy);
                        var p = _backup.GetPixel(sx, sy);
                        var sa = p.A * amount;
                        var rest = 1 - sa / 255.0;
                        b = p.B * amount + b * rest;
                        g = p.G * amount + g * rest;
                        r = p.R * amount + r * rest;
                        a = sa + a * rest;
                    }

                    var alpha = ToByte(a);
                    target[i] = Math.Min(ToByte(b), alpha);
                    target[i + 1] = Math.Min(ToByte(g), alpha);
                    target[i + 2] = Math.Min(ToByte(r), alpha);
                    target[i + 3] = alpha;
                }
            }

            _touched = region;
            _context.Document.CommitPixelChange(region);
        }

        public void Commit(ToolContext context, string nameKey)
        {
            var entries = new List<IHistoryEntry>();
            if (PixelRegionEntry.FromBackup(nameKey, _layer, _backup, _touched) is { } pixels)
            {
                entries.Add(pixels);
            }

            if (_selection is not null && (_dx != 0 || _dy != 0))
            {
                var moved = Shift(_selection, _dx, _dy);
                context.Document.SetSelection(moved);
                entries.Add(new SelectionEntry(nameKey, _selection, context.Document.Selection));
            }

            if (entries.Count > 0)
            {
                context.History.Push(new CompositeEntry(nameKey, entries));
            }
        }

        public void Cancel()
        {
            for (var y = _touched.Y; y < _touched.Bottom; y++)
            {
                _backup.GetRow(y).Slice(_touched.X * 4, _touched.Width * 4).CopyTo(_layer.Pixels.GetRow(y).Slice(_touched.X * 4));
            }

            _context.Document.CommitPixelChange(_touched);
            _touched = IntRect.Empty;
        }

        private static SelectionMask Shift(SelectionMask source, int dx, int dy)
        {
            var result = SelectionMask.CreateEmpty(source.Width, source.Height);
            for (var y = 0; y < source.Height; y++)
            {
                var sy = y - dy;
                if (sy < 0 || sy >= source.Height)
                {
                    continue;
                }

                var from = Math.Max(0, dx);
                var to = Math.Min(source.Width, source.Width + dx);
                if (to > from)
                {
                    source.GetRow(sy).Slice(from - dx, to - from).CopyTo(result.GetRow(y).Slice(from));
                }
            }

            return result;
        }

        private static byte ToByte(double v)
        {
            var rounded = Math.Round(v, MidpointRounding.AwayFromZero);
            return rounded <= 0 ? (byte)0 : rounded >= 255 ? (byte)255 : (byte)rounded;
        }
    }
}

/// <summary>Zuschneiden: Rechteck aufziehen, mit Enter oder Doppelklick übernehmen, Escape bricht ab.</summary>
public sealed class CropTool : ToolBase
{
    private PointD? _start;
    private IntRect? _rect;

    public override string Id => "crop";

    public override ToolCategory Category => ToolCategory.Transform;

    public override string Glyph => "⛶";

    public override string? Shortcut => "C";

    public override bool IsActiveOperation => _rect is not null || _start is not null;

    public IntRect? PendingRect => _rect;

    public override void PointerDown(ToolContext context, ToolPointerEvent e)
    {
        if (e.ClickCount >= 2 && _rect is not null)
        {
            Commit(context);
            return;
        }

        _start = new PointD(Math.Round(e.Position.X), Math.Round(e.Position.Y));
        _rect = null;
    }

    public override void PointerMove(ToolContext context, ToolPointerEvent e)
    {
        if (_start is not { } start)
        {
            return;
        }

        var end = new PointD(Math.Round(e.Position.X), Math.Round(e.Position.Y));
        var left = (int)Math.Min(start.X, end.X);
        var top = (int)Math.Min(start.Y, end.Y);
        _rect = new IntRect(left, top, (int)Math.Abs(end.X - start.X), (int)Math.Abs(end.Y - start.Y))
            .Intersect(context.Document.Bounds);
        if (_rect.Value.IsEmpty)
        {
            _rect = null;
            ClearOverlay();
            return;
        }

        var r = _rect.Value;
        SetOverlay([new OverlayRect(new PointD(r.X, r.Y), new PointD(r.Right, r.Bottom))]);
    }

    public override void PointerUp(ToolContext context, ToolPointerEvent e)
    {
        PointerMove(context, e);
        _start = null;
    }

    public override void Commit(ToolContext context)
    {
        _start = null;
        if (_rect is { } r)
        {
            _rect = null;
            ClearOverlay();
            DocumentTransforms.Crop(context.Document, context.History, r);
        }
    }

    public override void Cancel(ToolContext context)
    {
        _start = null;
        _rect = null;
        ClearOverlay();
    }
}
