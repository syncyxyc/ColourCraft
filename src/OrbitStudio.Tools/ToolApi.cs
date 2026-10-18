using OrbitStudio.Domain.Documents;
using OrbitStudio.Domain.Imaging;
using OrbitStudio.History;
using OrbitStudio.Rendering;

namespace OrbitStudio.Tools;

public enum ToolCategory
{
    Selection,
    Transform,
    Draw,
    Fill,
    View,
}

public enum ToolCursor
{
    Arrow,
    Cross,
    Move,
    Hand,
    ZoomIn,
    /// <summary>Kein Systemcursor; das Werkzeug zeichnet seine Spitze als Überlagerung (z. B. Pinselkreis).</summary>
    None,
}

public enum PointerButton
{
    Primary,
    Secondary,
}

[Flags]
public enum ToolModifiers
{
    None = 0,
    Shift = 1,
    Ctrl = 2,
    Alt = 4,
}

public enum ToolKey
{
    Escape,
    Enter,
    Left,
    Right,
    Up,
    Down,
}

/// <summary>Grund, aus dem ein Werkzeug eine Eingabe nicht ausführen kann. Die Oberfläche zeigt eine deutsche Meldung.</summary>
public enum ToolProblem
{
    NoPixelLayer,
    LayerLocked,
    NoMask,
    NothingToMove,
}

/// <summary>Zeigereingabe in Dokument- und Viewport-Koordinaten.</summary>
public sealed record ToolPointerEvent(
    PointD Position,
    PointD ViewportPosition,
    PointerButton Button,
    ToolModifiers Modifiers,
    int ClickCount = 1)
{
    public bool Shift => Modifiers.HasFlag(ToolModifiers.Shift);

    public bool Alt => Modifiers.HasFlag(ToolModifiers.Alt);

    public bool Ctrl => Modifiers.HasFlag(ToolModifiers.Ctrl);
}

/// <summary>Vorder- und Hintergrundfarbe (global für alle Dokumente).</summary>
public sealed class ToolColors
{
    private ColorRgba _primary = ColorRgba.Black;
    private ColorRgba _secondary = ColorRgba.White;

    public ColorRgba Primary
    {
        get => _primary;
        set
        {
            _primary = value;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public ColorRgba Secondary
    {
        get => _secondary;
        set
        {
            _secondary = value;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public event EventHandler? Changed;

    public ColorRgba For(PointerButton button) => button == PointerButton.Primary ? Primary : Secondary;

    public void Swap() => (Primary, Secondary) = (_secondary, _primary);

    public void Reset()
    {
        _primary = ColorRgba.Black;
        Secondary = ColorRgba.White;
    }
}

/// <summary>Alles, was ein Werkzeug für eine Eingabe braucht: Dokument, Historie, Farben, Ansicht.</summary>
public sealed class ToolContext(ImageDocument document, DocumentHistory history, ToolColors colors, ViewTransform? view)
{
    public ImageDocument Document { get; } = document;

    public DocumentHistory History { get; } = history;

    public ToolColors Colors { get; } = colors;

    /// <summary>Ansicht (für Hand- und Zoomwerkzeug); in Tests ohne Oberfläche ggf. null.</summary>
    public ViewTransform? View { get; } = view;

    /// <summary>True: Malwerkzeuge bearbeiten die Maske der aktiven Ebene statt ihrer Pixel.</summary>
    public bool EditMask { get; init; }

    public event EventHandler<ToolProblem>? Problem;

    public void Report(ToolProblem problem) => Problem?.Invoke(this, problem);
}

// --- Überlagerungen (nie Teil der Pixel) -------------------------------------------------------

/// <summary>Form, die die Leinwand über dem Bild zeichnet (Dokumentkoordinaten). Wird nie in Pixel übernommen.</summary>
public abstract record OverlayShape;

public sealed record OverlayLine(PointD A, PointD B) : OverlayShape;

public sealed record OverlayRect(PointD A, PointD B) : OverlayShape;

public sealed record OverlayEllipse(PointD A, PointD B) : OverlayShape;

public sealed record OverlayPolyline(IReadOnlyList<PointD> Points, bool Closed) : OverlayShape;

/// <summary>Kreis mit Radius in Dokumentpixeln (z. B. Pinselspitze).</summary>
public sealed record OverlayCircle(PointD Center, double Radius) : OverlayShape;

/// <summary>Anfasser mit fester Bildschirmgröße (z. B. Kontrollpunkt).</summary>
public sealed record OverlayHandle(PointD Center, bool Highlighted) : OverlayShape;

/// <summary>
/// Temporäre Vorschau-Ebene (vormultipliertes BGRA), wird über dem Bild angezeigt und nie in Pixel übernommen.
/// Pixel (0, 0) liegt bei <paramref name="Origin"/>; ein Pixel ist 1 / <paramref name="Scale"/> Dokumentpixel groß
/// (Scale &lt; 1: Vorschau in reduzierter Auflösung).
/// </summary>
public sealed record OverlayRaster(PointD Origin, double Scale, PixelBuffer Pixels) : OverlayShape;

// --- Werkzeug ---------------------------------------------------------------------------------

/// <summary>Einheitliche Werkzeug-API (Spezifikation Abschnitt 7).</summary>
public interface ITool
{
    string Id { get; }

    /// <summary>Textschlüssel für Name und Tooltip („Tool.&lt;Id&gt;").</summary>
    string NameKey { get; }

    ToolCategory Category { get; }

    /// <summary>Kurzes Symbol (Unicode) für die Werkzeugleiste, bis eigene Vektor-Icons vorliegen.</summary>
    string Glyph { get; }

    /// <summary>Tastenkürzel ohne Strg (z. B. "B" oder "Shift+M").</summary>
    string? Shortcut { get; }

    IReadOnlyList<ToolOption> Options { get; }

    ToolCursor Cursor { get; }

    /// <summary>Überlagerungen (Vorschau, Kontrollpunkte, Pinselspitze) in Dokumentkoordinaten.</summary>
    IReadOnlyList<OverlayShape> Overlay { get; }

    /// <summary>True, solange eine Bearbeitung läuft (Zug, Form, Verschieben, Polygon).</summary>
    bool IsActiveOperation { get; }

    event EventHandler? OverlayChanged;

    void PointerDown(ToolContext context, ToolPointerEvent e);

    void PointerMove(ToolContext context, ToolPointerEvent e);

    void PointerUp(ToolContext context, ToolPointerEvent e);

    /// <summary>Mausbewegung ohne gedrückte Taste (z. B. Pinselspitze anzeigen).</summary>
    void Hover(ToolContext context, PointD position);

    /// <summary>
    /// Ein Ziehen endet ohne <see cref="PointerUp"/> (z. B. Mausfang verloren). Standard: laufende Bearbeitung übernehmen.
    /// </summary>
    void PointerLost(ToolContext context);

    /// <summary>Zoom, Verschiebung oder Fenstergröße des Dokuments haben sich geändert (z. B. Vorschau neu berechnen).</summary>
    void ViewChanged(ToolContext context);

    /// <summary>Tastatureingabe. Liefert true, wenn das Werkzeug sie verarbeitet hat.</summary>
    bool Key(ToolContext context, ToolKey key, ToolModifiers modifiers);

    /// <summary>Laufende Bearbeitung übernehmen (z. B. vor Werkzeugwechsel, Rückgängig, Speichern).</summary>
    void Commit(ToolContext context);

    /// <summary>Laufende Bearbeitung verwerfen; der Ausgangszustand bleibt unverändert.</summary>
    void Cancel(ToolContext context);
}

/// <summary>Basisklasse mit leeren Standardimplementierungen.</summary>
public abstract class ToolBase : ITool
{
    private IReadOnlyList<OverlayShape> _overlay = [];

    public abstract string Id { get; }

    public string NameKey => "Tool." + Id;

    public abstract ToolCategory Category { get; }

    public abstract string Glyph { get; }

    public abstract string? Shortcut { get; }

    public virtual IReadOnlyList<ToolOption> Options => [];

    public virtual ToolCursor Cursor => ToolCursor.Cross;

    public IReadOnlyList<OverlayShape> Overlay => _overlay;

    public virtual bool IsActiveOperation => false;

    public event EventHandler? OverlayChanged;

    public virtual void PointerDown(ToolContext context, ToolPointerEvent e)
    {
    }

    public virtual void PointerMove(ToolContext context, ToolPointerEvent e)
    {
    }

    public virtual void PointerUp(ToolContext context, ToolPointerEvent e)
    {
    }

    public virtual void Hover(ToolContext context, PointD position)
    {
    }

    public virtual void PointerLost(ToolContext context) => Commit(context);

    public virtual void ViewChanged(ToolContext context)
    {
    }

    public virtual bool Key(ToolContext context, ToolKey key, ToolModifiers modifiers)
    {
        if (key == ToolKey.Escape && IsActiveOperation)
        {
            Cancel(context);
            return true;
        }

        if (key == ToolKey.Enter && IsActiveOperation)
        {
            Commit(context);
            return true;
        }

        return false;
    }

    public virtual void Commit(ToolContext context)
    {
    }

    public virtual void Cancel(ToolContext context)
    {
    }

    protected void SetOverlay(IReadOnlyList<OverlayShape> shapes)
    {
        _overlay = shapes;
        OverlayChanged?.Invoke(this, EventArgs.Empty);
    }

    protected void ClearOverlay()
    {
        if (_overlay.Count > 0)
        {
            SetOverlay([]);
        }
    }
}
