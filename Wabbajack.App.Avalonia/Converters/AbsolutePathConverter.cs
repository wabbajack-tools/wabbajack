using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Wabbajack.Paths;

namespace Wabbajack.Converters;

/// <summary>
/// Two-way converter between <see cref="AbsolutePath"/> (a struct) and its string form.
/// Used by the FilePicker control's TextBox so a bound <c>AbsolutePath</c> can be edited as text.
/// </summary>
public sealed class AbsolutePathConverter : IValueConverter
{
    public static readonly AbsolutePathConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is AbsolutePath path ? path.ToString() : string.Empty;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is string s && !string.IsNullOrWhiteSpace(s)
            ? AbsolutePath.ConvertNoFailure(s)
            : default(AbsolutePath);
}
