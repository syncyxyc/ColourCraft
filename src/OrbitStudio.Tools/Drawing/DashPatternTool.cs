using OrbitStudio.Domain.Imaging;
using OrbitStudio.Tools.Painting;

namespace OrbitStudio.Tools.Drawing;

/// <summary>
/// Interaktives Linien- und Strichmuster-Werkzeug (Spezifikation 1.1, erweitert um gebogene Linien).
/// <list type="bullet">
/// <item>Aufziehen legt Anfangs- und Endpunkt fest. Jeder Punkt ist ziehbar; ab drei Punkten verläuft die Linie als Kurve
/// durch alle Punkte. Klick auf die Linie fügt dort einen Punkt ein, Rechtsklick auf einen inneren Punkt entfernt ihn
/// (höchstens <see cref="DashPath.MaxControlPoints"/> Punkte). Ziehen auf der Linie verschiebt alle Punkte.</item>
/// <item>Umschalt rastet den Winkel in 15°-Schritten ein, Pfeiltasten verschieben um 1 px (Umschalt: 10 px).</item>
/// <item>Die Vorschau ist eine <see cref="OverlayRaster"/>-Überlagerung: Die Ebene bleibt bis „Anwenden" unverändert.
/// Sie wird auf den sichtbaren Ausschnitt begrenzt und unter 100 % Zoom in reduzierter Auflösung berechnet.</item>
/// <item>Anwenden (Enter) überträgt das Muster auf die aktive Ebene (bzw. Maske), begrenzt auf Auswahl und Bildgrenzen,
/// als ein Rückgängig-Schritt. Abbrechen (Esc) verwirft die Bearbeitung, Zurücksetzen stellt die Standardparameter her.</item>
/// </list>
/// </summary>
public sealed class DashPatternTool : ToolBase
{
    /// <summary>Grenze für Koordinaten (Dokumentpixel); Punkte dürfen außerhalb des Bildes liegen.</summary>
    public const double CoordinateLimit = 100_000;

    /// <summary>Fangradius der Kontrollpunkte und der Linie (geräteunabhängige Pixel am Bildschirm).</summary>
    public const double HitRadiusDip = 8;

    /// <summary>Ab dieser Bewegung (Gerätepixel) gilt ein Ziehen auf der Linie als Verschieben, nicht als Klick.</summary>
    private const double MoveThresholdViewport = 4;

    private List<PointD>? _points;
    private ToolContext? _context;
    private Part _part;
    private int _partIndex;
    private Part _hover;
    private int _hoverIndex = -1;
    private PointD _dragOrigin;
    private PointD _downViewport;
    private List<PointD> _dragPoints = [];
    private bool _moved;
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
        Control,
        Line,
    }

    public NumberOption Thickness { get; } = new("Opt.Dash.Thickness", 1, 500, 4);

    public NumberOption DashLength { get; } = new("Opt.Dash.DashLength", 1, 5000, 10);

    public NumberOption GapLength { get; } = new("Opt.Dash.GapLength", 0, 5000, 5);

    public ColorOption Color { get; } = new("Opt.Dash.Color", ColorRgba.Black);

    public NumberOption Opacity { get; } = new("Opt.Opacity", 0, 100, 100, unitKey: "Unit.Percent");

    public ToggleOption AntialiasOption { get; } = new("Opt.Antialias", true);

    /// <summary>X und Y des Anfangspunkts.</summary>
    public NumberOption X1 { get; }

    public NumberOption Y1 { get; }

    /// <summary>X und Y des Endpunkts (bei gebogenen Linien der letzte Punkt).</summary>
    public NumberOption X2 { get; }

    public NumberOption Y2 { get; }

    /// <summary>Länge der Linie, Winkel, Richtung und Mitte der Verbindung von Anfang zu Ende, Anzahl der Striche.</summary>
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
    public override bool IsActiveOperation => _points is not null;

    public PointD? StartPoint => _points?[0];

    public PointD? EndPoint => _points?[^1];

    /// <summary>Alle Kontrollpunkte in Reihenfolge (null ohne Bearbeitung).</summary>
    public IReadOnlyList<PointD>? ControlPoints => _points;

    /// <summary>Verbindungsgerade von Anfang zu Ende (für Winkel und Richtung in der Anzeige).</summary>
    public DashLine? CurrentLine => _points is { Count: >= 2 } p
        ? new DashLine(p[0], p[^1], DashLength.Value, GapLength.Value)
        : null;

    /// <summary>Geometrie der Linie mit allen Kontrollpunkten (null ohne Bearbeitung).</summary>
    public DashPath? CurrentPath => _points is { Count: >= 2 } p
        ? new DashPath(p, DashLength.Value, GapLength.Value)
        : null;

    /// <summary>Setzt Anfangs- und Endpunkt (z. B. für Tests) und beginnt bei Bedarf eine Bearbeitung.</summary>
    public void SetPoints(ToolContext context, PointD start, PointD end)
    {
        if (_points is null)
        {
            if (!PaintTarget.CanPaint(context))
            {
                return;
            }

            _context = context;
        }

        _points = [ClampPoint(start), ClampPoint(end)];
        Refresh();
    }

    public override void PointerDown(ToolContext context, ToolPointerEvent e)
    {
        if (_points is { } points)
        {
            var (part, index) = HitTest(context, e, points);
            if (part != Part.None)
            {
                if (e.Button == PointerButton.Secondary)
                {
                    // Rechtsklick auf einen inneren Kontrollpunkt entfernt ihn; Anfang und Ende bleiben.
                    if (part == Part.Control && index > 0 && index < points.Count - 1 && points.Count > 2)
                    {
                        points.RemoveAt(index);
                        _hover = Part.None;
                        _hoverIndex = -1;
                        Refresh();
                    }

                    return;
                }

                _part = part;
                _partIndex = index;
                _dragOrigin = e.Position;
                _downViewport = e.ViewportPosition;
                _dragPoints = [.. points];
                _moved = false;
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
        _points = [p, p];
        _part = Part.Create;
        _partIndex = 1;
        _dragOrigin = e.Position;
        _dragPoints = [p, p];
        Refresh();
    }

    public override void PointerMove(ToolContext context, ToolPointerEvent e)
    {
        if (_part == Part.None || _points is not { } points)
        {
            return;
        }

        // Gegriffene Punkte behalten ihren Versatz zum Mauszeiger (kein Springen beim Greifen knapp daneben).
        var delta = e.Position - _dragOrigin;
        if (_part == Part.Line)
        {
            _moved |= (e.ViewportPosition - _downViewport).Length > MoveThresholdViewport;
            for (var i = 0; i < points.Count; i++)
            {
                points[i] = ClampPoint(_dragPoints[i] + delta);
            }
        }
        else
        {
            // Umschalt rastet den Winkel zum Nachbarn ein (Anfang: zum zweiten Punkt, sonst zum vorherigen).
            var anchor = _partIndex > 0 ? points[_partIndex - 1] : points[Math.Min(1, points.Count - 1)];
            points[_partIndex] = ClampPoint(Constrain(anchor, _dragPoints[_partIndex] + delta, e.Shift));
        }

        Refresh();
    }

    public override void PointerUp(ToolContext context, ToolPointerEvent e)
    {
        if (_part == Part.None)
        {
            return;
        }

        PointerMove(context, e);
        var part = _part;
        _part = Part.None;

        // Klick ohne Ziehen erzeugt keine Linie.
        if (part == Part.Create && CurrentLine is { IsPoint: true })
        {
            Cancel(context);
            return;
        }

        // Klick auf die Linie (ohne Ziehen) fügt dort einen Kontrollpunkt ein.
        if (part == Part.Line && !_moved && _points is { } points)
        {
            InsertPoint(e.Position, points);
            Refresh();
        }
    }

    /// <summary>Mausfang verloren: nur das Ziehen beenden, die Linie bleibt bearbeitbar.</summary>
    public override void PointerLost(ToolContext context) => _part = Part.None;

    public override void Hover(ToolContext context, PointD position)
    {
        if (_points is not { } points || _part != Part.None)
        {
            return;
        }

        var viewport = context.View?.DocumentToViewport(position) ?? position;
        var (part, index) = HitTest(context, new ToolPointerEvent(position, viewport, PointerButton.Primary, ToolModifiers.None), points);
        if (part != _hover || index != _hoverIndex)
        {
            _hover = part;
            _hoverIndex = index;
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
        if (_points is { } points && key is ToolKey.Left or ToolKey.Right or ToolKey.Up or ToolKey.Down)
        {
            var step = modifiers.HasFlag(ToolModifiers.Shift) ? 10 : 1;
            var delta = key switch
            {
                ToolKey.Left => new PointD(-step, 0),
                ToolKey.Right => new PointD(step, 0),
                ToolKey.Up => new PointD(0, -step),
                _ => new PointD(0, step),
            };
            for (var i = 0; i < points.Count; i++)
            {
                points[i] = ClampPoint(points[i] + delta);
            }

            Refresh();
            return true;
        }

        return base.Key(context, key, modifiers);
    }

    /// <summary>Anwenden: Muster auf die aktive Ebene übertragen (ein Rückgängig-Schritt).</summary>
    public override void Commit(ToolContext context)
    {
        var path = CurrentPath;
        EndEdit();
        if (path is null || path.Count == 0 || Opacity.Value <= 0 || Color.Value.A == 0)
        {
            return;
        }

        var session = PaintTarget.Start(context, PaintMode.Paint, Color.Value, Opacity.Value / 100);
        if (session is null)
        {
            return;
        }

        var bounds = path.Bounds(Thickness.Value / 2 + 1).Intersect(context.Document.Bounds);
        DashPathRasterizer.Rasterize(path, Thickness.Value, AntialiasOption.Value, bounds, 1,
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
        if (CurrentPath is not { Count: > 0 } path || _context is not { } context || Opacity.Value <= 0 || Color.Value.A == 0)
        {
            return null;
        }

        var document = context.Document;
        var region = path.Bounds(Thickness.Value / 2 + 1).Intersect(document.Bounds);
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

        var pixels = PixelBuffer.Create(DashPathRasterizer.GridSize(region.Width, scale), DashPathRasterizer.GridSize(region.Height, scale));
        var color = Color.Value.ToPremultiplied();
        var opacity = Opacity.Value / 100;
        var selection = document.Selection;
        DashPathRasterizer.Rasterize(path, Thickness.Value, AntialiasOption.Value, region, scale, (i, j, coverage) =>
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

    /// <summary>
    /// Trefferprüfung in Bildschirmkoordinaten, damit der Fangradius bei jedem Zoom und jeder DPI gleich groß ist.
    /// Kontrollpunkte haben Vorrang (der nächstliegende, bei Gleichstand der spätere); dann die Linie.
    /// </summary>
    private (Part Part, int Index) HitTest(ToolContext context, ToolPointerEvent e, IReadOnlyList<PointD> points)
    {
        PointD Screen(PointD p) => context.View?.DocumentToViewport(p) ?? p;
        var radius = HitRadiusDip * (context.View?.DpiScale ?? 1);
        var pointer = context.View is null ? e.Position : e.ViewportPosition;

        var best = -1;
        var bestDistance = radius;
        for (var i = 0; i < points.Count; i++)
        {
            var distance = (pointer - Screen(points[i])).Length;
            if (distance <= bestDistance)
            {
                best = i;
                bestDistance = distance;
            }
        }

        if (best >= 0)
        {
            return (Part.Control, best);
        }

        var path = new DashPath(points, DashLength.Value, GapLength.Value);
        foreach (var piece in path.Pieces)
        {
            if (Coverage.DistanceToSegment(pointer, Screen(piece.A), Screen(piece.B)) <= radius)
            {
                return (Part.Line, -1);
            }
        }

        return (Part.None, -1);
    }

    /// <summary>Neuen Punkt auf der Linie einfügen: hinter dem Anfang des Segments, dessen Strecke dem Klick am nächsten liegt.</summary>
    private static void InsertPoint(PointD position, List<PointD> points)
    {
        if (points.Count >= DashPath.MaxControlPoints)
        {
            return;
        }

        var segment = 0;
        var bestDistance = double.PositiveInfinity;
        for (var i = 0; i < points.Count - 1; i++)
        {
            var distance = Coverage.DistanceToSegment(position, points[i], points[i + 1]);
            if (distance < bestDistance)
            {
                segment = i;
                bestDistance = distance;
            }
        }

        points.Insert(segment + 1, ClampPoint(position));
    }

    private void OnCoordinateEdited()
    {
        if (_syncing || _points is not { } points)
        {
            return;
        }

        points[0] = new PointD(X1.Value, Y1.Value);
        points[^1] = new PointD(X2.Value, Y2.Value);
        Refresh();
    }

    private void EndEdit()
    {
        _points = null;
        _context = null;
        _part = Part.None;
        _hover = Part.None;
        _hoverIndex = -1;
        Refresh();
    }

    /// <summary>Eingabefelder, Anzeige, Schaltflächen und Überlagerung an den aktuellen Zustand anpassen.</summary>
    private void Refresh()
    {
        if (_points is { } points)
        {
            _syncing = true;
            try
            {
                X1.Value = points[0].X;
                Y1.Value = points[0].Y;
                X2.Value = points[^1].X;
                Y2.Value = points[^1].Y;
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
        if (_points is not { } points)
        {
            ClearOverlay();
            return;
        }

        var shapes = new List<OverlayShape>(points.Count + 3);
        if (RenderPreview() is { } preview)
        {
            shapes.Add(preview);
        }

        // Linie und Kontrollpunkte: reine Anzeige, nie Teil der Pixel.
        var path = CurrentPath;
        var outline = path is { Pieces.Count: > 0 }
            ? path.Pieces.Select(p => p.A).Append(path.Pieces[^1].B).ToList()
            : points.ToList();
        shapes.Add(new OverlayPolyline(outline, Closed: false));
        for (var i = 0; i < points.Count; i++)
        {
            var highlighted = (_hover == Part.Control && _hoverIndex == i)
                              || (_part == Part.Control && _partIndex == i)
                              || (_part == Part.Create && i == _partIndex);
            shapes.Add(new OverlayHandle(points[i], highlighted));
        }

        SetOverlay(shapes);
    }

    private void UpdateInfo()
    {
        if (CurrentLine is not { } line || CurrentPath is not { } path)
        {
            Info.Set("Opt.Dash.Info.Empty");
            return;
        }

        var mid = line.Midpoint;
        Info.Set("Opt.Dash.Info.Line", path.Length, line.AngleDegrees, line.Direction.X, line.Direction.Y, mid.X, mid.Y,
            path.Count);
    }

    private void UpdateEnabled()
    {
        var editing = _points is not null;
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
