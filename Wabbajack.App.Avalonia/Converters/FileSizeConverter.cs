using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Wabbajack.Common;

namespace Wabbajack.Converters;

/// <summary>
/// Converts a byte count (a <see cref="long"/>, e.g. <c>Archive.Size</c>) into a
/// human-readable file size string via the <c>ToFileSizeString()</c> extension in
/// <see cref="Wabbajack.Common.NumberExtensions"/>.
/// </summary>
public sealed class FileSizeConverter : IValueConverter
{
    public static readonly FileSizeConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value switch
        {
            long l => l.ToFileSizeString(),
            // Size range sliders bind double values (MinModlistSize/MaxModlistSize, in bytes).
            double d => ((long)d).ToFileSizeString(),
            _ => ""
        };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
