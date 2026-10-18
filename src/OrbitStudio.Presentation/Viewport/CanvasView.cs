using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using OrbitStudio.Domain.Imaging;
using OrbitStudio.Presentation.Tooling;
using OrbitStudio.Presentation.ViewModels;
using OrbitStudio.Tools;

namespace OrbitStudio.Presentation.Viewport;

/// <summary>
/// Zeichenfläche eines Dokuments. Zeichnet Schachbrett, Bild, Pixelraster, Auswahlkontur und Werkzeug-Überlagerungen
/// gemäß der <see cref="OrbitStudio.Rendering.ViewTransform"/> des Dokuments. Überlagerungen sind reine Anzeige und
/// werden nie in Pixel übernommen. Mausrad = Zoom, mittlere Maustaste/Leertaste = Verschieben, linke/rechte Maustaste
/// gehen an das aktive Werkzeug.
/// </summary>
public sealed class CanvasView : FrameworkElement
{
    /// <summary>Ab diesem Zoom wird das Pixelraster gezeichnet (800 %).</summary>
    public const double PixelGridMinZoom = 8.0;

    public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register(
        nameof(ViewModel), typeof(DocumentViewModel), typeof(CanvasView),
        new PropertyMetadata(null, (d, e) => ((CanvasView)d).OnViewModelChanged(
            e.OldValue as DocumentViewModel, e.NewValue as DocumentViewModel)));

    public static readonly DependencyProperty ShowPixelGridProperty = DependencyProperty.Register(
        nameof(ShowPixelGrid), typeof(bool), typeof(CanvasView),
        new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ToolsProperty = DependencyProperty.Register(
        nameof(Tools), typeof(ToolController), typeof(CanvasView),
        new PropertyMetadata(null, (d, e) => ((CanvasView)d).OnToolsChanged(
            e.OldValue as ToolController, e.NewValue as ToolController)));

    private static readonly Pen OutlineDark = CreatePen(Colors.Black, null);
    private static readonly Pen OutlineLight = CreatePen(Colors.White, new DashStyle([4, 4], 0));

    private Point? _panLast;
    private bool _spaceDown;
    private bool _toolDragging;
    private PointerButton _toolButton;

    public CanvasView()
    {
        Focusable = true;
        FocusVisualStyle = null;
        ClipToBounds = true;
        SnapsToDevicePixels = true;
    }

    public DocumentViewModel? ViewModel
    {
        get => (DocumentViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    public bool ShowPixelGrid
    {
        get => (bool)GetValue(ShowPixelGridProperty);
        set => SetValue(ShowPixelGridProperty, value);
    }

    public ToolController? Tools
    {
        get => (ToolController?)GetValue(ToolsProperty);
        set => SetValue(ToolsProperty, value);
    }

    public bool IsPanning => _panLast is not null;

    private double DpiScale => VisualTreeHelper.GetDpi(this).DpiScaleX;

    private static Pen CreatePen(Color color, DashStyle? dash)
    {
        var pen = new Pen(new SolidColorBrush(color), 1) { DashStyle = dash ?? DashStyles.Solid };
        pen.Freeze();
        return pen;
    }

    private void OnViewModelChanged(DocumentViewModel? oldValue, DocumentViewModel? newValue)
    {
        if (oldValue is not null)
        {
            oldValue.ViewChanged -= OnViewChanged;
            oldValue.ClearCursor();
        }

        if (newValue is not null)
        {
            newValue.ViewChanged += OnViewChanged;
        }

        EndPan();
        EndToolDrag();
        UpdateViewport();
        UpdateScalingMode();
        InvalidateVisual();
    }

    private void OnToolsChanged(ToolController? oldValue, ToolController? newValue)
    {
        if (oldValue is not null)
        {
            oldValue.OverlayChanged -= OnOverlayChanged;
            oldValue.PropertyChanged -= OnToolsPropertyChanged;
        }

        if (newValue is not null)
        {
            newValue.OverlayChanged += OnOverlayChanged;
            newValue.PropertyChanged += OnToolsPropertyChanged;
        }

        UpdateCursor();
    }

    private void OnToolsPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ToolController.ActiveTool))
        {
            UpdateCursor();
        }
    }

    private void OnOverlayChanged(object? sender, EventArgs e) => InvalidateVisual();

    private void OnViewChanged(object? sender, EventArgs e)
    {
        UpdateScalingMode();
        InvalidateVisual();
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        UpdateViewport();
    }

    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        UpdateViewport();
    }

    private void UpdateViewport()
    {
        if (ViewModel is not { } vm || ActualWidth <= 0 || ActualHeight <= 0)
        {
            return;
        }

        vm.View.SetViewport(ActualWidth, ActualHeight, DpiScale);
        if (!vm.IsInitialViewApplied && vm.View.HasViewport)
        {
            // Erste Ansicht: ganzes Bild sichtbar, aber nicht über 100 % vergrößern.
            vm.View.FitToViewport(allowUpscale: false);
            vm.IsInitialViewApplied = true;
        }
    }

    /// <summary>Ab 100 % scharfe Pixel (nächster Nachbar), darunter hochwertige Verkleinerung.</summary>
    private void UpdateScalingMode()
    {
        var mode = ViewModel?.View.Zoom >= 1.0 ? BitmapScalingMode.NearestNeighbor : BitmapScalingMode.HighQuality;
        if (RenderOptions.GetBitmapScalingMode(this) != mode)
        {
            RenderOptions.SetBitmapScalingMode(this, mode);
        }
    }

    private void UpdateCursor()
    {
        if (IsPanning || _spaceDown)
        {
            return;
        }

        Cursor = Tools?.ActiveTool.Cursor switch
        {
            ToolCursor.Arrow => Cursors.Arrow,
            ToolCursor.Move => Cursors.SizeAll,
            ToolCursor.Hand => Cursors.Hand,
            null => null,
            _ => Cursors.Cross,
        };
    }

    // --- Darstellung ------------------------------------------------------------------------------

    protected override void OnRender(DrawingContext dc)
    {
        dc.DrawRectangle(TryFindResource("Brush.Workspace.Background") as Brush ?? Brushes.DimGray, null,
            new Rect(RenderSize));
        if (ViewModel is not { } vm || !vm.View.HasViewport)
        {
            return;
        }

        var view = vm.View;
        var dpi = view.DpiScale;

        // Ab 100 % auf ganze Gerätepixel ausrichten, damit jedes Bildpixel scharf und gleich groß erscheint.
        var originX = view.Zoom >= 1 ? Math.Round(view.OffsetX) : view.OffsetX;
        var originY = view.Zoom >= 1 ? Math.Round(view.OffsetY) : view.OffsetY;
        var imageRect = new Rect(originX / dpi, originY / dpi, vm.Width * view.Zoom / dpi, vm.Height * view.Zoom / dpi);

        if (TryFindResource("Brush.Checkerboard") is Brush checker)
        {
            dc.DrawRectangle(checker, null, imageRect);
        }

        dc.DrawImage(vm.Bitmap, imageRect);

        if (ShowPixelGrid && view.Zoom >= PixelGridMinZoom)
        {
            DrawPixelGrid(dc, vm, originX, originY);
        }

        // Dokumentkoordinaten → DIP (gleiche Rundung wie das Bild, damit Kontur und Pixel deckungsgleich sind)
        Point ToDip(PointD p) => new((originX + p.X * view.Zoom) / dpi, (originY + p.Y * view.Zoom) / dpi);

        DrawSelectionOutline(dc, vm, ToDip);
        if (Tools?.ActiveTool.Overlay is { Count: > 0 } overlay)
        {
            DrawOverlay(dc, overlay, ToDip, view.Zoom / dpi);
        }
    }

    private void DrawSelectionOutline(DrawingContext dc, DocumentViewModel vm, Func<PointD, Point> toDip)
    {
        var segments = vm.SelectionOutline;
        if (segments.Count == 0)
        {
            return;
        }

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            foreach (var (a, b) in segments)
            {
                ctx.BeginFigure(Snap(toDip(a)), false, false);
                ctx.LineTo(Snap(toDip(b)), true, false);
            }
        }

        geometry.Freeze();
        DrawTwoTone(dc, geometry);
    }

    /// <summary>Linien auf die Mitte eines Gerätepixels legen, damit 1-px-Linien scharf sind.</summary>
    private Point Snap(Point dip)
    {
        var dpi = DpiScale;
        return new Point((Math.Round(dip.X * dpi) + 0.5) / dpi, (Math.Round(dip.Y * dpi) + 0.5) / dpi);
    }

    private void DrawTwoTone(DrawingContext dc, Geometry geometry)
    {
        var thickness = 1.0 / DpiScale;
        dc.DrawGeometry(null, ScaledPen(OutlineDark, thickness), geometry);
        dc.DrawGeometry(null, ScaledPen(OutlineLight, thickness), geometry);
    }

    private static Pen ScaledPen(Pen template, double thickness)
    {
        if (Math.Abs(template.Thickness - thickness) < 1e-9)
        {
            return template;
        }

        var pen = template.Clone();
        pen.Thickness = thickness;
        pen.Freeze();
        return pen;
    }

    private void DrawOverlay(DrawingContext dc, IReadOnlyList<OverlayShape> shapes, Func<PointD, Point> toDip, double scale)
    {
        var geometry = new GeometryGroup();
        foreach (var shape in shapes)
        {
            switch (shape)
            {
                case OverlayLine line:
                    geometry.Children.Add(new LineGeometry(toDip(line.A), toDip(line.B)));
                    break;
                case OverlayRect rect:
                    geometry.Children.Add(new RectangleGeometry(new Rect(Snap(toDip(rect.A)), Snap(toDip(rect.B)))));
                    break;
                case OverlayEllipse ellipse:
                    geometry.Children.Add(new EllipseGeometry(new Rect(toDip(ellipse.A), toDip(ellipse.B))));
                    break;
                case OverlayPolyline poly when poly.Points.Count >= 2:
                    var figure = new PathFigure { StartPoint = toDip(poly.Points[0]), IsClosed = poly.Closed };
                    foreach (var p in poly.Points.Skip(1))
                    {
                        figure.Segments.Add(new LineSegment(toDip(p), true));
                    }

                    geometry.Children.Add(new PathGeometry([figure]));
                    break;
                case OverlayCircle circle:
                    var radius = Math.Max(2 / DpiScale, circle.Radius * scale);
                    geometry.Children.Add(new EllipseGeometry(toDip(circle.Center), radius, radius));
                    break;
                case OverlayHandle handle:
                    var c = toDip(handle.Center);
                    var size = handle.Highlighted ? 6.0 : 4.5;
                    geometry.Children.Add(new RectangleGeometry(new Rect(c.X - size, c.Y - size, 2 * size, 2 * size)));
                    break;
            }
        }

        geometry.Freeze();
        DrawTwoTone(dc, geometry);
    }

    private void DrawPixelGrid(DrawingContext dc, DocumentViewModel vm, double originX, double originY)
    {
        var view = vm.View;
        var visible = view.VisibleDocumentRect();
        if (visible.IsEmpty)
        {
            return;
        }

        var dpi = view.DpiScale;
        var brush = TryFindResource("Brush.PixelGrid") as Brush ?? new SolidColorBrush(Color.FromArgb(64, 128, 128, 128));
        var pen = new Pen(brush, 1.0 / dpi); // genau 1 Gerätepixel
        pen.Freeze();

        var top = (originY + visible.Y * view.Zoom) / dpi;
        var bottom = (originY + visible.Bottom * view.Zoom) / dpi;
        var left = (originX + visible.X * view.Zoom) / dpi;
        var right = (originX + visible.Right * view.Zoom) / dpi;

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            for (var x = visible.X; x <= visible.Right; x++)
            {
                var px = (originX + x * view.Zoom + 0.5) / dpi;
                ctx.BeginFigure(new Point(px, top), false, false);
                ctx.LineTo(new Point(px, bottom), true, false);
            }

            for (var y = visible.Y; y <= visible.Bottom; y++)
            {
                var py = (originY + y * view.Zoom + 0.5) / dpi;
                ctx.BeginFigure(new Point(left, py), false, false);
                ctx.LineTo(new Point(right, py), true, false);
            }
        }

        geometry.Freeze();
        dc.DrawGeometry(null, pen, geometry);
    }

    // --- Eingabe ------------------------------------------------------------------------------

    private PointD ToViewport(Point dip)
    {
        var dpi = DpiScale;
        return new PointD(dip.X * dpi, dip.Y * dpi);
    }

    private static ToolModifiers CurrentModifiers()
    {
        var m = ToolModifiers.None;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            m |= ToolModifiers.Shift;
        }

        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            m |= ToolModifiers.Ctrl;
        }

        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt))
        {
            m |= ToolModifiers.Alt;
        }

        return m;
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);
        ViewModel?.ZoomWithWheel(e.Delta, ToViewport(e.GetPosition(this)));
        e.Handled = true;
    }

    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        base.OnMouseDown(e);
        Focus();
        var startPan = e.ChangedButton == MouseButton.Middle || (e.ChangedButton == MouseButton.Left && _spaceDown);
        if (startPan && CaptureMouse())
        {
            _panLast = e.GetPosition(this);
            Cursor = Cursors.SizeAll;
            e.Handled = true;
            return;
        }

        if (ViewModel is not { } vm || Tools is not { } tools || _toolDragging
            || e.ChangedButton is not (MouseButton.Left or MouseButton.Right))
        {
            return;
        }

        _toolButton = e.ChangedButton == MouseButton.Left ? PointerButton.Primary : PointerButton.Secondary;
        var viewport = ToViewport(e.GetPosition(this));
        _toolDragging = CaptureMouse();
        tools.PointerDown(vm, vm.View.ViewportToDocument(viewport), viewport, _toolButton, CurrentModifiers(), e.ClickCount);
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var position = e.GetPosition(this);
        if (_panLast is { } last && ViewModel is { } panVm)
        {
            var dpi = DpiScale;
            panVm.View.PanBy((position.X - last.X) * dpi, (position.Y - last.Y) * dpi);
            _panLast = position;
            return;
        }

        if (ViewModel is not { } vm)
        {
            return;
        }

        var viewport = ToViewport(position);
        var document = vm.View.ViewportToDocument(viewport);
        vm.UpdateCursorFromViewport(viewport);
        if (_toolDragging)
        {
            Tools?.PointerMove(vm, document, viewport, _toolButton, CurrentModifiers());
        }
        else
        {
            Tools?.Hover(vm, document);
        }
    }

    protected override void OnMouseUp(MouseButtonEventArgs e)
    {
        base.OnMouseUp(e);
        if (IsPanning)
        {
            EndPan();
            e.Handled = true;
            return;
        }

        if (_toolDragging && ViewModel is { } vm)
        {
            var released = e.ChangedButton == MouseButton.Left ? PointerButton.Primary : PointerButton.Secondary;
            if (released != _toolButton)
            {
                return;
            }

            var viewport = ToViewport(e.GetPosition(this));
            _toolDragging = false;
            ReleaseMouseCapture();
            Tools?.PointerUp(vm, vm.View.ViewportToDocument(viewport), viewport, _toolButton, CurrentModifiers());
            e.Handled = true;
        }
    }

    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        base.OnLostMouseCapture(e);
        _panLast = null;
        if (_toolDragging)
        {
            // Mausfang verloren (z. B. Fensterwechsel): laufende Bearbeitung übernehmen statt hängen lassen.
            _toolDragging = false;
            Tools?.CommitActive();
        }

        Cursor = null;
        UpdateCursor();
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        if (!IsPanning && !_toolDragging)
        {
            ViewModel?.ClearCursor();
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Space && !_toolDragging)
        {
            _spaceDown = true;
            if (!IsPanning)
            {
                Cursor = Cursors.Hand;
            }

            e.Handled = true;
        }
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);
        if (e.Key == Key.Space)
        {
            _spaceDown = false;
            if (!IsPanning)
            {
                Cursor = null;
                UpdateCursor();
            }

            e.Handled = true;
        }
    }

    protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        base.OnLostKeyboardFocus(e);
        _spaceDown = false;
        if (!IsPanning)
        {
            Cursor = null;
            UpdateCursor();
        }
    }

    private void EndPan()
    {
        if (IsPanning && IsMouseCaptured)
        {
            ReleaseMouseCapture();
        }

        _panLast = null;
        Cursor = _spaceDown ? Cursors.Hand : null;
        UpdateCursor();
    }

    private void EndToolDrag()
    {
        if (_toolDragging)
        {
            _toolDragging = false;
            if (IsMouseCaptured)
            {
                ReleaseMouseCapture();
            }

            Tools?.CommitActive();
        }
    }
}
