using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace KongDaeri.Converters;

/// <summary>
/// true → Visible, false → Collapsed. ConverterParameter="invert" 면 반대.
/// (내장 BooleanToVisibilityConverter 는 invert 파라미터를 무시하므로 별도 구현)
/// </summary>
public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var b = value is true;
        if (parameter as string == "invert") b = !b;
        return b ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
