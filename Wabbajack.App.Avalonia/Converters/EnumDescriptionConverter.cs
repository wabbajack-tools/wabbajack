using System;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using Avalonia.Data.Converters;

namespace Wabbajack;

/// <summary>
/// Renders an enum value using its [Description] attribute, falling back to the member name.
/// WPF reached these through the EnumToItemsSource markup extension's DisplayName; this covers the
/// display half of that for templates which only need to show the value.
/// </summary>
public class EnumDescriptionConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not Enum e) return value?.ToString() ?? string.Empty;

        var name = e.ToString();
        var description = e.GetType().GetField(name)?.GetCustomAttribute<DescriptionAttribute>()?.Description;
        return string.IsNullOrWhiteSpace(description) ? name : description;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
