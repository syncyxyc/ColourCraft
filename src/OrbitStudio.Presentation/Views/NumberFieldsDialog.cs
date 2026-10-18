using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using OrbitStudio.Presentation.Localization;
using OrbitStudio.Presentation.Services;

namespace OrbitStudio.Presentation.Views;

/// <summary>
/// Einfacher Eingabedialog für eine oder mehrere Zahlen mit Grenzen. Optional werden die ersten beiden Felder
/// über „Seitenverhältnis beibehalten" gekoppelt (Bildgröße). Ungültige Eingaben werden mit Meldung abgewiesen.
/// </summary>
public sealed class NumberFieldsDialog : Window
{
    private readonly IReadOnlyList<NumberField> _fields;
    private readonly List<TextBox> _boxes = [];
    private readonly TextBlock _error;
    private readonly CheckBox? _keepRatio;
    private readonly double _ratio;
    private bool _syncing;

    public NumberFieldsDialog(string titleKey, IReadOnlyList<NumberField> fields, bool linkFirstTwo)
    {
        _fields = fields;
        Title = Loc.Get(titleKey);
        SizeToContent = SizeToContent.WidthAndHeight;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;
        SetResourceReference(BackgroundProperty, "Brush.Window.Background");
        SetResourceReference(ForegroundProperty, "Brush.Text");

        var grid = new Grid { Margin = new Thickness(16), MinWidth = 300 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var row = 0;
        foreach (var field in fields)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var box = new TextBox { Margin = new Thickness(0, 2, 0, 2), MinWidth = 120, Text = Format(field.Value, field) };
            var label = new Label { Content = Loc.Get(field.LabelKey), Target = box };
            Grid.SetRow(label, row);
            Grid.SetRow(box, row);
            Grid.SetColumn(box, 1);
            grid.Children.Add(label);
            grid.Children.Add(box);
            _boxes.Add(box);
            row++;
        }

        if (linkFirstTwo && fields.Count >= 2 && fields[1].Value != 0)
        {
            _ratio = fields[0].Value / fields[1].Value;
            _keepRatio = new CheckBox { Content = Loc.Get("Dlg.Size.KeepRatio"), IsChecked = true, Margin = new Thickness(0, 6, 0, 0) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Grid.SetRow(_keepRatio, row++);
            Grid.SetColumn(_keepRatio, 1);
            grid.Children.Add(_keepRatio);
            _boxes[0].TextChanged += (_, _) => Couple(0, 1, v => v / _ratio);
            _boxes[1].TextChanged += (_, _) => Couple(1, 0, v => v * _ratio);
        }

        _error = new TextBlock { Margin = new Thickness(0, 8, 0, 0), TextWrapping = TextWrapping.Wrap, MaxWidth = 360, Visibility = Visibility.Collapsed };
        _error.SetResourceReference(TextBlock.ForegroundProperty, "Brush.Error");
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Grid.SetRow(_error, row++);
        Grid.SetColumnSpan(_error, 2);
        grid.Children.Add(_error);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
        var ok = new Button { Content = Loc.Get("Common.Ok"), MinWidth = 88, IsDefault = true };
        ok.Click += OnOk;
        buttons.Children.Add(ok);
        buttons.Children.Add(new Button { Content = Loc.Get("Common.Cancel"), MinWidth = 88, Margin = new Thickness(8, 0, 0, 0), IsCancel = true });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Grid.SetRow(buttons, row);
        Grid.SetColumnSpan(buttons, 2);
        grid.Children.Add(buttons);

        Content = grid;
        Loaded += (_, _) =>
        {
            _boxes[0].Focus();
            _boxes[0].SelectAll();
        };
    }

    public double[]? Result { get; private set; }

    /// <summary>Prüft Texte gegen die Felder; liefert Werte oder eine deutsche Fehlermeldung.</summary>
    public static (double[]? Values, string? Error) Validate(IReadOnlyList<NumberField> fields, IReadOnlyList<string?> texts)
    {
        var values = new double[fields.Count];
        for (var i = 0; i < fields.Count; i++)
        {
            var f = fields[i];
            if (!double.TryParse(texts[i]?.Trim().TrimEnd('%', '°').Trim(), NumberStyles.Float, Loc.Culture, out var v)
                || !double.IsFinite(v) || v < f.Minimum || v > f.Maximum)
            {
                return (null, Loc.Format("Dlg.Number.Error", Format(f.Minimum, f), Format(f.Maximum, f)));
            }

            values[i] = f.Decimals == 0 ? Math.Round(v) : v;
        }

        return (values, null);
    }

    private static string Format(double value, NumberField field) =>
        value.ToString(field.Decimals == 0 ? "0" : "0.##", Loc.Culture);

    private void Couple(int from, int to, Func<double, double> convert)
    {
        if (_syncing || _keepRatio?.IsChecked != true)
        {
            return;
        }

        if (double.TryParse(_boxes[from].Text, NumberStyles.Float, Loc.Culture, out var v) && v > 0)
        {
            _syncing = true;
            _boxes[to].Text = Format(Math.Max(1, Math.Round(convert(v))), _fields[to]);
            _syncing = false;
        }
    }

    private void OnOk(object sender, RoutedEventArgs e)
    {
        var (values, error) = Validate(_fields, _boxes.Select(b => b.Text).ToList());
        if (values is null)
        {
            _error.Text = error;
            _error.Visibility = Visibility.Visible;
            return;
        }

        Result = values;
        DialogResult = true;
    }
}
