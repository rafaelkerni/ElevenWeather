using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ElevenWeather.Converters;

/// <summary>Visible when the string is empty — used to simulate a placeholder in a TextBox.</summary>
public sealed class EmptyStringToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => string.IsNullOrEmpty(value as string) ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
