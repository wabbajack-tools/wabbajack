using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Wabbajack.Converters;

/// <summary>
/// Returns true when the bound enum value's string form equals the ConverterParameter.
/// Used to drive state-based <c>IsVisible</c> sections (e.g. binding to
/// <c>InstallState</c> with <c>ConverterParameter=Configuration</c>).
/// </summary>
public sealed class EnumEqualsConverter : IValueConverter
{
    public static readonly EnumEqualsConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value?.ToString() == parameter?.ToString();

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
