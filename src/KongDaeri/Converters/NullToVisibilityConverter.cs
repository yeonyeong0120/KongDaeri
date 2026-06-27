using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace KongDaeri.Converters;

/// <summary>
/// null 이면 Collapsed, 값이 있으면 Visible. ConverterParameter="invert" 면 반대.
/// </summary>
public sealed class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var hasValue = value is not null;
        if (parameter as string == "invert") hasValue = !hasValue;
        return hasValue ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
