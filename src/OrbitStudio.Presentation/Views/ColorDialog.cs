using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using OrbitStudio.Domain.Imaging;
using OrbitStudio.Presentation.Localization;

namespace OrbitStudio.Presentation.Views;

/// <summary>
/// Farbwahl mit Reglern für Rot, Grün, Blau und Deckkraft (0–255), Hex-Feld und Vorschau auf Schachbrett.
/// Regler, Zahlenfelder und Hex-Feld sind synchron; ungültige Eingaben werden abgewiesen.
/// </summary>
public sealed class ColorDialog : Window
{
    private readonly Slider[] _sliders = new Slider[4];
    private readonly TextBox[] _boxes = new TextBox[4];
    private readonly TextBox _hex;
    private readonly Border _preview;
    private readonly TextBlock _error;
    private bool _syncing;

    public ColorDialog(ColorRgba initial)
    {
        Title = Loc.Get("Dlg.Color.Title");
        SizeToContent = SizeToContent.WidthAndHeight;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;
        SetResourceReference(BackgroundProperty, "Brush.Window.Background");
        SetResourceReference(ForegroundProperty, "Brush.Text");

        var grid = new Grid { Margin = new Thickness(16) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(200) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(56) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var keys = new[] { "Dlg.Color.Red", "Dlg.Color.Green", "Dlg.Color.Blue", "Dlg.Color.Alpha" };
        var values = new[] { initial.R, initial.G, initial.B, initial.A };
        for (var i = 0; i < 4; i++)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var slider = new Slider { Minimum = 0, Maximum = 255, SmallChange = 1, LargeChange = 16, IsSnapToTickEnabled = true, TickFrequency = 1, Value = values[i], VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 2, 8, 2) };
            var box = new TextBox { Text = values[i].ToString(CultureInfo.InvariantCulture), Margin = new Thickness(0, 2, 0, 2) };
            var label = new Label { Content = Loc.Get(keys[i]), Target = box };
            AutomationProperties.SetName(slider, Loc.WithoutAccessKey(Loc.Get(keys[i])));
            Grid.SetRow(label, i);
            Grid.SetRow(slider, i);
            Grid.SetColumn(slider, 1);
            Grid.SetRow(box, i);
            Grid.SetColumn(box, 2);
            grid.Children.Add(label);
            grid.Children.Add(slider);
            grid.Children.Add(box);
            _sliders[i] = slider;
            _boxes[i] = box;
            slider.ValueChanged += (_, _) => FromSliders();
            box.TextChanged += (_, _) => FromBoxes();
        }

        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _hex = new TextBox { Text = initial.ToHex(), Margin = new Thickness(0, 6, 0, 2) };
        _hex.TextChanged += (_, _) => FromHex();
        var hexLabel = new Label { Content = Loc.Get("Dlg.Color.Hex"), Target = _hex, Margin = new Thickness(0, 4, 0, 0) };
        Grid.SetRow(hexLabel, 4);
        Grid.SetRow(_hex, 4);
        Grid.SetColumn(_hex, 1);
        grid.Children.Add(hexLabel);
        grid.Children.Add(_hex);

        // Vorschau: Farbe über Schachbrett (zeigt die Deckkraft)
        var checker = new Border { Width = 64, Height = 64, Margin = new Thickness(12, 2, 0, 0), BorderThickness = new Thickness(1), VerticalAlignment = VerticalAlignment.Top };
        checker.SetResourceReference(Border.BackgroundProperty, "Brush.Checkerboard");
        checker.SetResourceReference(Border.BorderBrushProperty, "Brush.Border");
        _preview = new Border();
        checker.Child = _preview;
        Grid.SetColumn(checker, 3);
        Grid.SetRowSpan(checker, 5);
        grid.Children.Add(checker);

        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _error = new TextBlock { TextWrapping = TextWrapping.Wrap, MaxWidth = 380, Margin = new Thickness(0, 6, 0, 0), Visibility = Visibility.Collapsed };
        _error.SetResourceReference(TextBlock.ForegroundProperty, "Brush.Error");
        Grid.SetRow(_error, 5);
        Grid.SetColumnSpan(_error, 4);
        grid.Children.Add(_error);

        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
        var ok = new Button { Content = Loc.Get("Common.Ok"), MinWidth = 88, IsDefault = true };
        ok.Click += OnOk;
        buttons.Children.Add(ok);
        buttons.Children.Add(new Button { Content = Loc.Get("Common.Cancel"), MinWidth = 88, Margin = new Thickness(8, 0, 0, 0), IsCancel = true });
        Grid.SetRow(buttons, 6);
        Grid.SetColumnSpan(buttons, 4);
        grid.Children.Add(buttons);

        Content = grid;
        Current = initial;
        UpdatePreview();
    }

    public ColorRgba Current { get; private set; }

    public ColorRgba? Result { get; private set; }

    private void FromSliders()
    {
        if (_syncing)
        {
            return;
        }

        Current = new ColorRgba((byte)_sliders[0].Value, (byte)_sliders[1].Value, (byte)_sliders[2].Value, (byte)_sliders[3].Value);
        Sync(updateSliders: false, updateBoxes: true, updateHex: true);
    }

    private void FromBoxes()
    {
        if (_syncing)
        {
            return;
        }

        var parsed = new byte[4];
        for (var i = 0; i < 4; i++)
        {
            if (!byte.TryParse(_boxes[i].Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed[i]))
            {
                ShowError();
                return;
            }
        }

        Current = new ColorRgba(parsed[0], parsed[1], parsed[2], parsed[3]);
        Sync(updateSliders: true, updateBoxes: false, updateHex: true);
    }

    private void FromHex()
    {
        if (_syncing)
        {
            return;
        }

        if (!ColorRgba.TryParseHex(_hex.Text, out var color))
        {
            ShowError();
            return;
        }

        Current = color;
        Sync(updateSliders: true, updateBoxes: true, updateHex: false);
    }

    private void Sync(bool updateSliders, bool updateBoxes, bool updateHex)
    {
        _syncing = true;
        var values = new[] { Current.R, Current.G, Current.B, Current.A };
        for (var i = 0; i < 4; i++)
        {
            if (updateSliders)
            {
                _sliders[i].Value = values[i];
            }

            if (updateBoxes)
            {
                _boxes[i].Text = values[i].ToString(CultureInfo.InvariantCulture);
            }
        }

        if (updateHex)
        {
            _hex.Text = Current.ToHex();
        }

        _syncing = false;
        _error.Visibility = Visibility.Collapsed;
        UpdatePreview();
    }

    private void ShowError()
    {
        _error.Text = Loc.Get("Dlg.Color.Error");
        _error.Visibility = Visibility.Visible;
    }

    private void UpdatePreview() =>
        _preview.Background = new SolidColorBrush(Color.FromArgb(Current.A, Current.R, Current.G, Current.B));

    private void OnOk(object sender, RoutedEventArgs e)
    {
        if (_error.Visibility == Visibility.Visible)
        {
            return;
        }

        Result = Current;
        DialogResult = true;
    }
}
