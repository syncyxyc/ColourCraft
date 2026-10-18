using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace OrbitStudio.Presentation.Converters;

/// <summary>true → Visible, false → Collapsed. Mit <see cref="Invert"/> umgekehrt.</summary>
public sealed class BoolToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var flag = value is true;
        return flag != Invert ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
