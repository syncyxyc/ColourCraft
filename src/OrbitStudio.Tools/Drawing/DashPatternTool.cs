using OrbitStudio.Domain.Imaging;
using OrbitStudio.Tools.Painting;

namespace OrbitStudio.Tools.Drawing;

/// <summary>
/// Interaktives Linien- und Strichmuster-Werkzeug (Spezifikation 1.1).
/// <list type="bullet">
/// <item>Aufziehen legt Start- und Endpunkt fest; beide bleiben danach als Kontrollpunkte ziehbar, die Linie selbst
/// verschiebt beide Punkte. Umschalt rastet den Winkel in 15°-Schritten ein, Pfeiltasten verschieben um 1 px (Umschalt: 10 px).</item>
/// <item>Die Vorschau ist eine <see cref="OverlayRaster"/>-Überlagerung: Die Ebene bleibt bis „Anwenden" unverändert.
/// Sie wird auf den sichtbaren Ausschnitt begrenzt und unter 100 % Zoom in reduzierter Auflösung berechnet.</item>
/// <item>Anwenden (Enter) überträgt das Muster auf die aktive Ebene (bzw. Maske), begrenzt auf Auswahl und Bildgrenzen,
/// als ein Rückgängig-Schritt. Abbrechen (Esc) verwirft die Bearbeitung, Zurücksetzen stellt die Standardparameter her.</item>
/// <item>Ein Klick neben Linie und Kontrollpunkte übernimmt die laufende Linie und beginnt eine neue.</item>
/// </list>
/// </summary>
public sealed class DashPatternTool : ToolBase
{
    /// <summary>Grenze für Koordinaten (Dokumentpixel); Punkte dürfen außerhalb des Bildes liegen.</summary>
    public const double CoordinateLimit = 100_000;

    /// <summary>Fangradius der Kontrollpunkte und der Linie (geräteunabhängige Pixel am Bildschirm).</summary>
    public const double HitRadiusDip = 8;

    private PointD? _start;
    private PointD? _end;
    private ToolContext? _context;
    private Part _drag;
    private Part _hover;
    private PointD _dragOrigin;
    private PointD _dragStart;
    private PointD _dragEnd;
    private bool _syncing;

    public DashPatternTool()
    {
        X1 = Coordinate("Opt.Dash.X1");
        Y1 = Coordinate("Opt.Dash.Y1");
        X2 = Coordinate("Opt.Dash.X2");
        Y2 = Coordinate("Opt.Dash.Y2");
        ApplyAction = new ActionOption("Opt.Dash.Apply", c =>
        {
            if (c is not null)
            {
                Commit(c);
            }
        });
        CancelAction = new ActionOption("Opt.Dash.Cancel", c =>
        {
            if (c is not null)
            {
                Cancel(c);
            }
        });
        ResetAction = new ActionOption("Opt.Dash.Reset", _ => ResetParameters());

        foreach (var option in new ToolOption[] { Thickness, DashLength, GapLength, Color, Opacity, AntialiasOption })
        {
            option.Changed += (_, _) => Refresh();
        }

        foreach (var option in new[] { X1, Y1, X2, Y2 })
        {
            option.Changed += (_, _) => OnCoordinateEdited();
        }

        UpdateInfo();
        UpdateEnabled();
    }

    private enum Part
    {
        None,
        Create,
        Start,
        End,
        Line,
    }

    public NumberOption Thickness { get; } = new("Opt.Dash.Thickness", 1, 500, 4);

    public NumberOption DashLength { get; } = new("Opt.Dash.DashLength", 1, 5000, 10);

    public NumberOption GapLength { get; } = new("Opt.Dash.GapLength", 0, 5000, 5);

    public ColorOption Color { get; } = new("Opt.Dash.Color", ColorRgba.Black);

    public NumberOption Opacity { get; } = new("Opt.Opacity", 0, 100, 100, unitKey: "Unit.Percent");

    public ToggleOption AntialiasOption { get; } = new("Opt.Antialias", true);

    public NumberOption X1 { get; }

    public NumberOption Y1 { get; }

    public NumberOption X2 { get; }

    public NumberOption Y2 { get; }

    /// <summary>Länge, Winkel, Richtung, Mittelpunkt und Strichanzahl der aktuellen Linie.</summary>
    public InfoOption Info { get; } = new("Opt.Dash.Info", "Opt.Dash.Info.Empty");

    public ActionOption ApplyAction { get; }

    public ActionOption CancelAction { get; }

    public ActionOption ResetAction { get; }

    public override string Id => "dashPattern";

    public override ToolCategory Category => ToolCategory.Draw;

    public override string Glyph => "┅";

    public override string? Shortcut => "Shift+L";

    public override IReadOnlyList<ToolOption> Options =>
    [
        Thickness, DashLength, GapLength, Color, Opacity, AntialiasOption,
        X1, Y1, X2, Y2, Info, ApplyAction, CancelAction, ResetAction,
    ];

    /// <summary>True, solange eine Linie bearbeitet wird (noch nicht angewendet oder verworfen).</summary>
    public override bool IsActiveOperation => _start is not null;

    public PointD? StartPoint => _start;

    public PointD? EndPoint => _end;

    /// <summary>Geometrie der aktuellen Linie (null ohne Bearbeitung).</summary>
    public DashLine? CurrentLine => _start is { } s && _end is { } e
        ? new DashLine(s, e, DashLength.Value, GapLength.Value)
        : null;

    /// <summary>Setzt beide Punkte (z. B. aus Eingabefeldern oder Tests) und beginnt bei Bedarf eine Bearbeitung.</summary>
    public void SetPoints(ToolContext context, PointD start, PointD end)
    {
        if (_start is null)
        {
            if (!PaintTarget.CanPaint(context))
            {
                return;
            }

            _context = context;
        }

        _start = ClampPoint(start);
        _end = ClampPoint(end);
        Refresh();
    }

    public override void PointerDown(ToolContext context, ToolPointerEvent e)
    {
        if (_start is { } s && _end is { } en)
        {
            var hit = HitTest(context, e, s, en);
            if (hit != Part.None)
            {
                _drag = hit;
                _dragOrigin = e.Position;
                _dragStart = s;
                _dragEnd = en;
                return;
            }

            Commit(context);
        }

        if (!PaintTarget.CanPaint(context))
        {
            return;
        }

        _context = context;
        var p = ClampPoint(e.Position);
        _start = p;
        _end = p;
        _drag = Part.Create;
        Refresh();
    }

    public override void PointerMove(ToolContext context, ToolPointerEvent e)
    {
        if (_drag == Part.None || _start is not { } s || _end is not { } en)
        {
            return;
        }

        // Gegriffene Punkte behalten ihren Versatz zum Mauszeiger (kein Springen beim Greifen knapp daneben).
        var delta = e.Position - _dragOrigin;
        switch (_drag)
        {
            case Part.Create:
                _end = ClampPoint(Constrain(s, e.Position, e.Shift));
                break;
            case Part.End:
                _end = ClampPoint(Constrain(s, _dragEnd + delta, e.Shift));
                break;
            case Part.Start:
                _start = ClampPoint(Constrain(en, _dragStart + delta, e.Shift));
                break;
            case Part.Line:
                _start = ClampPoint(_dragStart + delta);
                _end = ClampPoint(_dragEnd + delta);
                break;
        }

        Refresh();
    }

    public override void PointerUp(ToolContext context, ToolPointerEvent e)
    {
        if (_drag == Part.None)
        {
            return;
        }

        PointerMove(context, e);
        var created = _drag == Part.Create;
        _drag = Part.None;

        // Klick ohne Ziehen erzeugt keine Linie.
        if (created && CurrentLine is { IsPoint: true })
        {
            Cancel(context);
        }
    }

    /// <summary>Mausfang verloren: nur das Ziehen beenden, die Linie bleibt bearbeitbar.</summary>
    public override void PointerLost(ToolContext context) => _drag = Part.None;

    public override void Hover(ToolContext context, PointD position)
    {
        if (_start is not { } s || _end is not { } e || _drag != Part.None)
        {
            return;
        }

        var viewport = context.View?.DocumentToViewport(position) ?? position;
        var hover = HitTest(context, new ToolPointerEvent(position, viewport, PointerButton.Primary, ToolModifiers.None), s, e);
        if (hover != _hover)
        {
            _hover = hover;
            Refresh();
        }
    }

    public override void ViewChanged(ToolContext context)
    {
        if (_context is not null && context.Document == _context.Document)
        {
            Refresh();
        }
    }

    public override bool Key(ToolContext context, ToolKey key, ToolModifiers modifiers)
    {
        if (_start is { } s && _end is { } e && key is ToolKey.Left or ToolKey.Right or ToolKey.Up or ToolKey.Down)
        {
            var step = modifiers.HasFlag(ToolModifiers.Shift) ? 10 : 1;
            var delta = key switch
            {
                ToolKey.Left => new PointD(-step, 0),
                ToolKey.Right => new PointD(step, 0),
                ToolKey.Up => new PointD(0, -step),
                _ => new PointD(0, step),
            };
            _start = ClampPoint(s + delta);
            _end = ClampPoint(e + delta);
            Refresh();
            return true;
        }

        return base.Key(context, key, modifiers);
    }

    /// <summary>Anwenden: Muster auf die aktive Ebene übertragen (ein Rückgängig-Schritt).</summary>
    public override void Commit(ToolContext context)
    {
        var line = CurrentLine;
        EndEdit();
        if (line is null || line.Count == 0 || Opacity.Value <= 0 || Color.Value.A == 0)
        {
            return;
        }

        var session = PaintTarget.Start(context, PaintMode.Paint, Color.Value, Opacity.Value / 100);
        if (session is null)
        {
            return;
        }

        var bounds = PaintTarget.Bounds(line.Start, line.End, Thickness.Value / 2 + 1).Intersect(context.Document.Bounds);
        DashRasterizer.Rasterize(line, Thickness.Value, AntialiasOption.Value, bounds, 1,
            (i, j, coverage) => session.AddCoverage(bounds.X + i, bounds.Y + j, coverage));
        session.Apply(bounds);
        session.Commit(context, NameKey);
    }

    /// <summary>Abbrechen: Bearbeitung vollständig verwerfen (die Ebene wurde nie verändert).</summary>
    public override void Cancel(ToolContext context) => EndEdit();

    /// <summary>Zurücksetzen: alle Musterparameter auf die Standardwerte; die Punkte bleiben erhalten.</summary>
    public void ResetParameters()
    {
        Thickness.Reset();
        DashLength.Reset();
        GapLength.Reset();
        Color.Reset();
        Opacity.Reset();
        AntialiasOption.Reset();
    }

    /// <summary>
    /// Berechnet die Vorschau: Musterfarbe · Abdeckung · Deckkraft · Auswahl, genau wie beim Anwenden
    /// (bei voller Auflösung identisch zu dem, was auf eine leere Ebene geschrieben würde).
    /// </summary>
    public OverlayRaster? RenderPreview()
    {
        if (CurrentLine is not { Count: > 0 } line || _context is not { } context || Opacity.Value <= 0 || Color.Value.A == 0)
        {
            return null;
        }

        var document = context.Document;
        var region = PaintTarget.Bounds(line.Start, line.End, Thickness.Value / 2 + 1).Intersect(document.Bounds);
        var scale = 1.0;
        if (context.View is { HasViewport: true } view)
        {
            region = region.Intersect(view.VisibleDocumentRect());
            scale = Math.Min(1, view.Zoom);
        }

        if (region.IsEmpty)
        {
            return null;
        }

        var pixels = PixelBuffer.Create(DashRasterizer.GridSize(region.Width, scale), DashRasterizer.GridSize(region.Height, scale));
        var color = Color.Value.ToPremultiplied();
        var opacity = Opacity.Value / 100;
        var selection = document.Selection;
        DashRasterizer.Rasterize(line, Thickness.Value, AntialiasOption.Value, region, scale, (i, j, coverage) =>
        {
            // Gleiche Rechnung wie PaintSession (Abdeckung als float gespeichert, kaufmännisch gerundet).
            var amount = (float)Math.Min(1, coverage) * opacity;
            if (selection is not null)
            {
                var x = Math.Min(document.Width - 1, region.X + (int)((i + 0.5) / scale));
                var y = Math.Min(document.Height - 1, region.Y + (int)((j + 0.5) / scale));
                amount *= selection.Get(x, y) / 255.0;
            }

            if (amount <= 0)
            {
                return;
            }

            var alpha = ToByte(color.A * amount);
            pixels.SetPixel(i, j, new Bgra32(
                Math.Min(ToByte(color.B * amount), alpha),
                Math.Min(ToByte(color.G * amount), alpha),
                Math.Min(ToByte(color.R * amount), alpha),
                alpha));
        });
        return new OverlayRaster(new PointD(region.X, region.Y), scale, pixels);
    }

    private static NumberOption Coordinate(string labelKey) =>
        new(labelKey, -CoordinateLimit, CoordinateLimit, 0, 1, showSlider: false);

    private static PointD ClampPoint(PointD p) => new(
        Math.Clamp(double.IsFinite(p.X) ? p.X : 0, -CoordinateLimit, CoordinateLimit),
        Math.Clamp(double.IsFinite(p.Y) ? p.Y : 0, -CoordinateLimit, CoordinateLimit));

    /// <summary>Umschalt: Winkel rastet in 15°-Schritten ein (wie beim Linienwerkzeug).</summary>
    private static PointD Constrain(PointD anchor, PointD p, bool shift)
    {
        if (!shift)
        {
            return p;
        }

        var d = p - anchor;
        var length = d.Length;
        var angle = Math.Round(Math.Atan2(d.Y, d.X) / (Math.PI / 12)) * (Math.PI / 12);
        return new PointD(anchor.X + length * Math.Cos(angle), anchor.Y + length * Math.Sin(angle));
    }

    /// <summary>Trefferprüfung in Bildschirmkoordinaten, damit der Fangradius bei jedem Zoom und jeder DPI gleich groß ist.</summary>
    private static Part HitTest(ToolContext context, ToolPointerEvent e, PointD start, PointD end)
    {
        PointD Screen(PointD p) => context.View?.DocumentToViewport(p) ?? p;
        var radius = HitRadiusDip * (context.View?.DpiScale ?? 1);
        var pointer = context.View is null ? e.Position : e.ViewportPosition;
        var a = Screen(start);
        var b = Screen(end);
        var da = (pointer - a).Length;
        var db = (pointer - b).Length;

        // Bei überlappenden Punkten gewinnt der Endpunkt (so lässt sich eine kurze Linie wieder aufziehen).
        if (db <= radius && db <= da)
        {
            return Part.End;
        }

        if (da <= radius)
        {
            return Part.Start;
        }

        return Coverage.DistanceToSegment(pointer, a, b) <= radius ? Part.Line : Part.None;
    }

    private void OnCoordinateEdited()
    {
        if (_syncing || _start is null)
        {
            return;
        }

        _start = new PointD(X1.Value, Y1.Value);
        _end = new PointD(X2.Value, Y2.Value);
        Refresh();
    }

    private void EndEdit()
    {
        _start = null;
        _end = null;
        _context = null;
        _drag = Part.None;
        _hover = Part.None;
        Refresh();
    }

    /// <summary>Eingabefelder, Anzeige, Schaltflächen und Überlagerung an den aktuellen Zustand anpassen.</summary>
    private void Refresh()
    {
        if (_start is { } s && _end is { } e)
        {
            _syncing = true;
            try
            {
                X1.Value = s.X;
                Y1.Value = s.Y;
                X2.Value = e.X;
                Y2.Value = e.Y;
            }
            finally
            {
                _syncing = false;
            }
        }

        UpdateInfo();
        UpdateEnabled();
        UpdateOverlay();
    }

    private void UpdateOverlay()
    {
        if (_start is not { } s || _end is not { } e)
        {
            ClearOverlay();
            return;
        }

        var shapes = new List<OverlayShape>(4);
        if (RenderPreview() is { } preview)
        {
            shapes.Add(preview);
        }

        // Hilfslinie und Kontrollpunkte: reine Anzeige, nie Teil der Pixel.
        shapes.Add(new OverlayLine(s, e));
        shapes.Add(new OverlayHandle(s, _hover == Part.Start || _drag == Part.Start));
        shapes.Add(new OverlayHandle(e, _hover == Part.End || _drag is Part.End or Part.Create));
        SetOverlay(shapes);
    }

    private void UpdateInfo()
    {
        if (CurrentLine is not { } line)
        {
            Info.Set("Opt.Dash.Info.Empty");
            return;
        }

        var mid = line.Midpoint;
        Info.Set("Opt.Dash.Info.Line", line.Length, line.AngleDegrees, line.Direction.X, line.Direction.Y, mid.X, mid.Y,
            line.Count);
    }

    private void UpdateEnabled()
    {
        var editing = _start is not null;
        foreach (var option in new ToolOption[] { X1, Y1, X2, Y2, ApplyAction, CancelAction })
        {
            option.IsEnabled = editing;
        }
    }

    private static byte ToByte(double value)
    {
        var rounded = Math.Round(value, MidpointRounding.AwayFromZero);
        return rounded <= 0 ? (byte)0 : rounded >= 255 ? (byte)255 : (byte)rounded;
    }
}
