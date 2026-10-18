using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using OrbitStudio.Domain.Imaging;
using OrbitStudio.Presentation.ViewModels;

namespace OrbitStudio.Presentation.Viewport;

/// <summary>
/// Zeichenfläche eines Dokuments. Zeichnet Schachbrett, Bild und Pixelraster gemäß der
/// <see cref="OrbitStudio.Rendering.ViewTransform"/> des Dokuments und setzt Mausrad/Ziehen in Zoom und
/// Verschiebung um. Liest Pixel nur – ein Zoomwechsel verändert niemals Bilddaten.
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

    private Point? _panLast;
    private bool _spaceDown;

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

    public bool IsPanning => _panLast is not null;

    private double DpiScale => VisualTreeHelper.GetDpi(this).DpiScaleX;

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
        UpdateViewport();
        UpdateScalingMode();
        InvalidateVisual();
    }

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
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var position = e.GetPosition(this);
        if (_panLast is { } last && ViewModel is { } vm)
        {
            var dpi = DpiScale;
            vm.View.PanBy((position.X - last.X) * dpi, (position.Y - last.Y) * dpi);
            _panLast = position;
        }

        ViewModel?.UpdateCursorFromViewport(ToViewport(position));
    }

    protected override void OnMouseUp(MouseButtonEventArgs e)
    {
        base.OnMouseUp(e);
        if (IsPanning)
        {
            EndPan();
            e.Handled = true;
        }
    }

    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        base.OnLostMouseCapture(e);
        _panLast = null;
        Cursor = _spaceDown ? Cursors.Hand : null;
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        if (!IsPanning)
        {
            ViewModel?.ClearCursor();
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Space)
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
        }
    }

    private void EndPan()
    {
        if (IsMouseCaptured)
        {
            ReleaseMouseCapture();
        }

        _panLast = null;
        Cursor = _spaceDown ? Cursors.Hand : null;
    }
}
