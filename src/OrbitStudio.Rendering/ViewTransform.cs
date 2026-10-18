using OrbitStudio.Domain.Imaging;

namespace OrbitStudio.Rendering;

/// <summary>
/// Zentrale Koordinatentransformation einer Dokumentansicht.
/// <list type="bullet">
/// <item><b>Dokumentkoordinaten</b>: Bildpixel, Ursprung oben links (double, unabhängig vom Bildschirm).</item>
/// <item><b>Viewport-Koordinaten</b>: physische Gerätepixel innerhalb der Arbeitsfläche, Ursprung oben links.</item>
/// <item><b>DIP</b>: geräteunabhängige WPF-Einheiten (1/96 Zoll) = Viewport / DpiScale.</item>
/// </list>
/// Zoom 1.0 (100 %) bedeutet: 1 Dokumentpixel = 1 Gerätepixel, unabhängig von der Windows-Skalierung.
/// Es gilt: viewport = dokument · Zoom + Offset.
/// Die Transformation liest und verändert niemals Pixeldaten.
/// </summary>
public sealed class ViewTransform
{
    public const double MinZoom = 0.01;
    public const double MaxZoom = 32.0;

    /// <summary>Rand um das Dokument in DIP, damit die Bildkanten beim Scrollen erreichbar bleiben.</summary>
    public const double MarginDip = 32.0;

    /// <summary>Zoomstufen für Vergrößern/Verkleinern und das Mausrad.</summary>
    public static IReadOnlyList<double> ZoomPresets { get; } =
    [
        0.01, 0.02, 0.03, 0.05, 1.0 / 12, 0.125, 1.0 / 6, 0.25, 1.0 / 3, 0.5, 2.0 / 3,
        1.0, 1.5, 2, 3, 4, 5, 6, 8, 12, 16, 24, 32,
    ];

    public ViewTransform(int documentWidth, int documentHeight)
    {
        SetDocumentSize(documentWidth, documentHeight);
    }

    public int DocumentWidth { get; private set; }

    public int DocumentHeight { get; private set; }

    public double Zoom { get; private set; } = 1.0;

    /// <summary>Position des Dokumentursprungs im Viewport (Gerätepixel).</summary>
    public double OffsetX { get; private set; }

    public double OffsetY { get; private set; }

    /// <summary>Viewportgröße in Gerätepixeln.</summary>
    public double ViewportWidth { get; private set; }

    public double ViewportHeight { get; private set; }

    /// <summary>Gerätepixel je DIP (1.0 bei 100 % Windows-Skalierung, 1.5 bei 150 %).</summary>
    public double DpiScale { get; private set; } = 1.0;

    public bool HasViewport => ViewportWidth > 0 && ViewportHeight > 0;

    public double MarginDevice => MarginDip * DpiScale;

    /// <summary>Wird bei jeder Änderung von Zoom, Verschiebung, Viewport oder DPI ausgelöst.</summary>
    public event EventHandler? Changed;

    public void SetDocumentSize(int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        DocumentWidth = width;
        DocumentHeight = height;
        ConstrainAndNotify();
    }

    /// <summary>Setzt Viewportgröße (in DIP) und DPI-Skalierung. Der Dokumentpunkt in der Mitte bleibt erhalten.</summary>
    public void SetViewport(double widthDip, double heightDip, double dpiScale)
    {
        if (!double.IsFinite(dpiScale) || dpiScale <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(dpiScale));
        }

        var hadViewport = HasViewport;
        var center = hadViewport ? ViewportToDocument(new PointD(ViewportWidth / 2, ViewportHeight / 2)) : default;

        DpiScale = dpiScale;
        ViewportWidth = Math.Max(0, widthDip * dpiScale);
        ViewportHeight = Math.Max(0, heightDip * dpiScale);

        if (hadViewport && HasViewport)
        {
            OffsetX = ViewportWidth / 2 - center.X * Zoom;
            OffsetY = ViewportHeight / 2 - center.Y * Zoom;
        }

        ConstrainAndNotify();
    }

    public PointD DocumentToViewport(PointD document) => new(document.X * Zoom + OffsetX, document.Y * Zoom + OffsetY);

    public PointD ViewportToDocument(PointD viewport) => new((viewport.X - OffsetX) / Zoom, (viewport.Y - OffsetY) / Zoom);

    public PointD DipToDocument(PointD dip) => ViewportToDocument(new PointD(dip.X * DpiScale, dip.Y * DpiScale));

    public PointD DocumentToDip(PointD document)
    {
        var v = DocumentToViewport(document);
        return new PointD(v.X / DpiScale, v.Y / DpiScale);
    }

    /// <summary>Ganzzahliges Dokumentpixel unter einem Viewportpunkt oder null, wenn außerhalb des Bildes.</summary>
    public (int X, int Y)? PixelAtViewport(PointD viewport)
    {
        var d = ViewportToDocument(viewport);
        var x = (int)Math.Floor(d.X);
        var y = (int)Math.Floor(d.Y);
        return x >= 0 && y >= 0 && x < DocumentWidth && y < DocumentHeight ? (x, y) : null;
    }

    /// <summary>
    /// Setzt den Zoom. Der Dokumentpunkt unter <paramref name="anchorViewport"/> bleibt an seiner Stelle
    /// (ohne Anker: Viewportmitte).
    /// </summary>
    public void SetZoom(double zoom, PointD? anchorViewport = null)
    {
        if (!double.IsFinite(zoom))
        {
            throw new ArgumentOutOfRangeException(nameof(zoom));
        }

        var anchor = anchorViewport ?? new PointD(ViewportWidth / 2, ViewportHeight / 2);
        var documentPoint = ViewportToDocument(anchor);
        Zoom = Math.Clamp(zoom, MinZoom, MaxZoom);
        OffsetX = anchor.X - documentPoint.X * Zoom;
        OffsetY = anchor.Y - documentPoint.Y * Zoom;
        ConstrainAndNotify();
    }

    public void ZoomIn(PointD? anchorViewport = null) => SetZoom(NextPreset(Zoom), anchorViewport);

    public void ZoomOut(PointD? anchorViewport = null) => SetZoom(PreviousPreset(Zoom), anchorViewport);

    /// <summary>100 %: 1 Dokumentpixel = 1 Gerätepixel.</summary>
    public void ActualSize() => SetZoom(1.0);

    /// <summary>Größter Zoom, bei dem das ganze Bild samt Rand sichtbar ist. Optional höchstens 100 %.</summary>
    public void FitToViewport(bool allowUpscale = true)
    {
        if (!HasViewport)
        {
            return;
        }

        var availableWidth = Math.Max(1, ViewportWidth - 2 * MarginDevice);
        var availableHeight = Math.Max(1, ViewportHeight - 2 * MarginDevice);
        var zoom = Math.Min(availableWidth / DocumentWidth, availableHeight / DocumentHeight);
        if (!allowUpscale)
        {
            zoom = Math.Min(zoom, 1.0);
        }

        Zoom = Math.Clamp(zoom, MinZoom, MaxZoom);
        ConstrainAndNotify(); // passt vollständig hinein → wird zentriert
    }

    /// <summary>Verschiebt die Ansicht um die angegebenen Gerätepixel.</summary>
    public void PanBy(double deltaX, double deltaY)
    {
        OffsetX += deltaX;
        OffsetY += deltaY;
        ConstrainAndNotify();
    }

    public static double NextPreset(double zoom) =>
        ZoomPresets.FirstOrDefault(p => p > zoom * (1 + 1e-9), MaxZoom);

    public static double PreviousPreset(double zoom) =>
        ZoomPresets.LastOrDefault(p => p < zoom * (1 - 1e-9), MinZoom);

    // --- Scrollleisten (Einheiten: Gerätepixel) -------------------------------------------------

    public ScrollRange HorizontalScroll => GetScroll(DocumentWidth, ViewportWidth, OffsetX);

    public ScrollRange VerticalScroll => GetScroll(DocumentHeight, ViewportHeight, OffsetY);

    public void SetHorizontalScroll(double value)
    {
        OffsetX = MarginDevice - value;
        ConstrainAndNotify();
    }

    public void SetVerticalScroll(double value)
    {
        OffsetY = MarginDevice - value;
        ConstrainAndNotify();
    }

    /// <summary>Sichtbarer Bereich in Dokumentpixeln (auf das Bild begrenzt, äußere Pixel eingeschlossen).</summary>
    public IntRect VisibleDocumentRect()
    {
        if (!HasViewport)
        {
            return IntRect.Empty;
        }

        var topLeft = ViewportToDocument(new PointD(0, 0));
        var bottomRight = ViewportToDocument(new PointD(ViewportWidth, ViewportHeight));
        var left = (int)Math.Floor(topLeft.X);
        var top = (int)Math.Floor(topLeft.Y);
        var right = (int)Math.Ceiling(bottomRight.X);
        var bottom = (int)Math.Ceiling(bottomRight.Y);
        return new IntRect(left, top, right - left, bottom - top).Intersect(new IntRect(0, 0, DocumentWidth, DocumentHeight));
    }

    private ScrollRange GetScroll(int documentSize, double viewportSize, double offset)
    {
        var extent = documentSize * Zoom + 2 * MarginDevice;
        var maximum = Math.Max(0, extent - viewportSize);
        return new ScrollRange(Math.Clamp(MarginDevice - offset, 0, maximum), maximum, viewportSize);
    }

    /// <summary>
    /// Passt das Bild hinein, wird es zentriert. Sonst darf es nur so weit verschoben werden,
    /// dass höchstens der Rand neben der Bildkante sichtbar ist.
    /// </summary>
    private void ConstrainAndNotify()
    {
        if (HasViewport)
        {
            OffsetX = ConstrainAxis(OffsetX, DocumentWidth * Zoom, ViewportWidth);
            OffsetY = ConstrainAxis(OffsetY, DocumentHeight * Zoom, ViewportHeight);
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    private double ConstrainAxis(double offset, double scaledSize, double viewportSize)
    {
        var margin = MarginDevice;
        if (scaledSize + 2 * margin <= viewportSize)
        {
            return (viewportSize - scaledSize) / 2;
        }

        return Math.Clamp(offset, viewportSize - scaledSize - margin, margin);
    }
}

/// <summary>Werte für eine Scrollleiste: aktuelle Position, Maximum (Minimum ist 0) und sichtbare Größe.</summary>
public readonly record struct ScrollRange(double Value, double Maximum, double ViewportSize);
