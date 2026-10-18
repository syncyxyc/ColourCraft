using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using OrbitStudio.Presentation.ViewModels;
using OrbitStudio.Rendering;

namespace OrbitStudio.Presentation.Views;

/// <summary>Verbindet die Scrollleisten mit der Ansichtstransformation des aktiven Dokuments.</summary>
public partial class DocumentView : UserControl
{
    private DocumentViewModel? _viewModel;
    private bool _updatingBars;

    public DocumentView()
    {
        InitializeComponent();
        DataContextChanged += (_, e) => Attach(e.NewValue as DocumentViewModel);
    }

    /// <summary>Leinwand dieses Tabs (für Tests und den Smoke-Test).</summary>
    public Viewport.CanvasView CanvasControl => Canvas;

    private void Attach(DocumentViewModel? viewModel)
    {
        if (_viewModel is not null)
        {
            _viewModel.ViewChanged -= OnViewChanged;
        }

        _viewModel = viewModel;
        if (_viewModel is not null)
        {
            _viewModel.ViewChanged += OnViewChanged;
            UpdateBars();
        }
    }

    private void OnViewChanged(object? sender, EventArgs e) => UpdateBars();

    private void UpdateBars()
    {
        if (_viewModel is null)
        {
            return;
        }

        _updatingBars = true;
        try
        {
            Apply(HorizontalBar, _viewModel.View.HorizontalScroll);
            Apply(VerticalBar, _viewModel.View.VerticalScroll);
        }
        finally
        {
            _updatingBars = false;
        }
    }

    private static void Apply(ScrollBar bar, ScrollRange range)
    {
        bar.Maximum = range.Maximum;
        bar.ViewportSize = range.ViewportSize;
        bar.LargeChange = Math.Max(1, range.ViewportSize * 0.9);
        bar.SmallChange = Math.Max(1, range.ViewportSize * 0.1);
        bar.Value = range.Value;
        bar.IsEnabled = range.Maximum > 0;
    }

    private void OnHorizontalScroll(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_updatingBars)
        {
            _viewModel?.View.SetHorizontalScroll(e.NewValue);
        }
    }

    private void OnVerticalScroll(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_updatingBars)
        {
            _viewModel?.View.SetVerticalScroll(e.NewValue);
        }
    }
}
