using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Wabbajack.App.Avalonia.Converters;

/// <summary>True when the bound value's text is the parameter, ordinally; stands in for WPF's DataTrigger on a string.</summary>
public class TextIsConverter : IValueConverter
{
    public static readonly TextIsConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => string.Equals(value?.ToString(), parameter?.ToString(), StringComparison.Ordinal);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
