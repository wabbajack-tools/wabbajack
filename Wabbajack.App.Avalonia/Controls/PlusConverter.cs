using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Wabbajack.App.Avalonia.Controls;

/// <summary>A number plus a fixed amount; WPF's MathConverter "x+16", which sized the tile's pills to their text.</summary>
public class PlusConverter(double amount) : IValueConverter
{
    public static readonly PlusConverter Sixteen = new(16);

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is double d ? d + amount : value;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
