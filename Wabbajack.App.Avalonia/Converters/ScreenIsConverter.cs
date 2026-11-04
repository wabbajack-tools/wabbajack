using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Wabbajack.App.Avalonia.Services;

namespace Wabbajack.App.Avalonia.Converters;

/// <summary>True when the bound screen belongs to the nav item named in the parameter; drives the rail's active class.</summary>
public class ScreenIsConverter : IValueConverter
{
    public static readonly ScreenIsConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is ScreenType screen && parameter is ScreenType wanted && Navigator.NavItemFor(screen) == wanted;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
