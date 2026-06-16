using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Wabbajack.Converters;

/// <summary>
/// Converts a byte count to a "<c>{n} GB</c>" string (rounded to one decimal), mirroring the WPF
/// gallery's size-slider labels (<c>MathConverter 'Round(x,1)'</c> + <c>StringFormat '{0} GB'</c>).
/// </summary>
public sealed class BytesToGbConverter : IValueConverter
{
    public static readonly BytesToGbConverter Instance = new();

    private const double BytesPerGb = 1024d * 1024d * 1024d;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var bytes = value switch
        {
            double d => d,
            long l => l,
            _ => 0d
        };
        var gb = Math.Round(bytes / BytesPerGb, 1);
        return $"{gb} GB";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
