using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Wabbajack.Converters;

/// <summary>
/// Returns true when the bound integer count is zero. Used to show empty-state UI
/// (e.g. the gallery's "No modlists found" message) only when a collection is empty.
/// Bind to a collection's <c>.Count</c> so the binding re-evaluates on collection changes.
/// </summary>
public sealed class IsZeroConverter : IValueConverter
{
    public static readonly IsZeroConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is int i && i == 0;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
