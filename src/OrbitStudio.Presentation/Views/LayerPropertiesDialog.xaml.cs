using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using OrbitStudio.Presentation.Localization;
using OrbitStudio.Presentation.Services;
using OrbitStudio.Presentation.ViewModels;

namespace OrbitStudio.Presentation.Views;

public partial class LayerPropertiesDialog : Window
{
    private bool _syncing;

    public LayerPropertiesDialog(LayerProperties initial, LayerPropertyFocus focus)
    {
        InitializeComponent();
        NameBox.Text = initial.Name;
        VisibleBox.IsChecked = initial.IsVisible;
        LockedBox.IsChecked = initial.IsLocked;
        OpacitySlider.Value = initial.OpacityPercent;
        BlendBox.ItemsSource = BlendModeOption.All;
        BlendBox.SelectedItem = BlendModeOption.All.First(o => o.Mode == initial.BlendMode);

        Loaded += (_, _) =>
        {
            Control target = focus switch
            {
                LayerPropertyFocus.Opacity => OpacityBox,
                LayerPropertyFocus.BlendMode => BlendBox,
                _ => NameBox,
            };
            target.Focus();
            if (target is TextBox box)
            {
                box.SelectAll();
            }
        };
    }

    public LayerProperties? Result { get; private set; }

    private void OnSliderChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_syncing || OpacityBox is null)
        {
            return;
        }

        _syncing = true;
        OpacityBox.Text = Math.Round(e.NewValue).ToString(Loc.Culture);
        _syncing = false;
    }

    private void OnOpacityTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_syncing)
        {
            return;
        }

        if (double.TryParse(OpacityBox.Text.Trim().TrimEnd('%'), NumberStyles.Float, Loc.Culture, out var value)
            && value is >= 0 and <= 100)
        {
            _syncing = true;
            OpacitySlider.Value = value;
            _syncing = false;
        }
    }

    private void OnOk(object sender, RoutedEventArgs e)
    {
        var mode = (BlendBox.SelectedItem as BlendModeOption)?.Mode ?? Domain.Layers.BlendMode.Normal;
        var (result, error) = LayerPropertiesValidator.Validate(NameBox.Text, OpacityBox.Text,
            VisibleBox.IsChecked == true, LockedBox.IsChecked == true, mode);
        if (result is null)
        {
            ErrorText.Text = error;
            ErrorText.Visibility = Visibility.Visible;
            return;
        }

        Result = result;
        DialogResult = true;
    }
}
