using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using OrbitStudio.Presentation.Localization;
using OrbitStudio.Presentation.Services;
using OrbitStudio.Tools.Transform;

namespace OrbitStudio.Presentation.Views;

/// <summary>Leinwandgröße: neue Breite/Höhe und Ausrichtung des bisherigen Bildes (3 × 3 Raster).</summary>
public sealed class CanvasSizeDialog : Window
{
    private readonly TextBox _width;
    private readonly TextBox _height;
    private readonly RadioButton[] _anchors = new RadioButton[9];
    private readonly TextBlock _error;

    public CanvasSizeDialog(int width, int height)
    {
        Title = Loc.Get("Dlg.CanvasSize.Title");
        SizeToContent = SizeToContent.WidthAndHeight;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;
        SetResourceReference(BackgroundProperty, "Brush.Window.Background");
        SetResourceReference(ForegroundProperty, "Brush.Text");

        var panel = new StackPanel { Margin = new Thickness(16), MinWidth = 300 };
        _width = AddField(panel, "Dlg.Size.Width", width);
        _height = AddField(panel, "Dlg.Size.Height", height);
        panel.Children.Add(new Label { Content = Loc.Get("Dlg.Size.Anchor"), Margin = new Thickness(0, 8, 0, 0) });

        var grid = new UniformGrid3();
        for (var i = 0; i < 9; i++)
        {
            var anchor = (CanvasAnchor)i;
            _anchors[i] = new RadioButton
            {
                GroupName = "Anchor",
                Width = 28,
                Height = 28,
                Margin = new Thickness(2),
                IsChecked = anchor == CanvasAnchor.Center,
                ToolTip = Loc.Get("Anchor." + anchor),
            };
            AutomationProperties.SetName(_anchors[i], Loc.Get("Anchor." + anchor));
            grid.Children.Add(_anchors[i]);
        }

        panel.Children.Add(grid);
        _error = new TextBlock { Margin = new Thickness(0, 8, 0, 0), TextWrapping = TextWrapping.Wrap, Visibility = Visibility.Collapsed, MaxWidth = 340 };
        _error.SetResourceReference(TextBlock.ForegroundProperty, "Brush.Error");
        panel.Children.Add(_error);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
        var ok = new Button { Content = Loc.Get("Common.Ok"), MinWidth = 88, IsDefault = true };
        ok.Click += OnOk;
        buttons.Children.Add(ok);
        buttons.Children.Add(new Button { Content = Loc.Get("Common.Cancel"), MinWidth = 88, Margin = new Thickness(8, 0, 0, 0), IsCancel = true });
        panel.Children.Add(buttons);
        Content = panel;
        Loaded += (_, _) => _width.Focus();
    }

    public (int Width, int Height, CanvasAnchor Anchor)? Result { get; private set; }

    private static TextBox AddField(Panel panel, string labelKey, int value)
    {
        var row = new DockPanel { Margin = new Thickness(0, 2, 0, 2) };
        var box = new TextBox { Text = value.ToString(Loc.Culture), MinWidth = 120 };
        var label = new Label { Content = Loc.Get(labelKey), Target = box, MinWidth = 100 };
        DockPanel.SetDock(label, Dock.Left);
        row.Children.Add(label);
        row.Children.Add(box);
        panel.Children.Add(row);
        return box;
    }

    private void OnOk(object sender, RoutedEventArgs e)
    {
        var fields = new[]
        {
            new NumberField("Dlg.Size.Width", 0, 1, Domain.Documents.DocumentLimits.MaxDimension),
            new NumberField("Dlg.Size.Height", 0, 1, Domain.Documents.DocumentLimits.MaxDimension),
        };
        var (values, error) = NumberFieldsDialog.Validate(fields, [_width.Text, _height.Text]);
        if (values is null)
        {
            _error.Text = error;
            _error.Visibility = Visibility.Visible;
            return;
        }

        var anchor = (CanvasAnchor)Array.FindIndex(_anchors, a => a.IsChecked == true);
        Result = ((int)values[0], (int)values[1], anchor);
        DialogResult = true;
    }

    /// <summary>3 × 3 gleich große Zellen.</summary>
    private sealed class UniformGrid3 : System.Windows.Controls.Primitives.UniformGrid
    {
        public UniformGrid3()
        {
            Rows = 3;
            Columns = 3;
            HorizontalAlignment = HorizontalAlignment.Left;
        }
    }
}
