using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Wabbajack.Common;
using Wabbajack.DTOs.DownloadStates;

namespace Wabbajack.App.Avalonia.Converters;

/// <summary>A byte count as the details pane's Size column shows it.</summary>
public class FileSizeConverter : IValueConverter
{
    public static readonly FileSizeConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is long size ? size.ToFileSizeString() : "";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>The mod name of a Nexus archive's state, and nothing for any other source.</summary>
public class NexusModNameConverter : IValueConverter
{
    public static readonly NexusModNameConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is Nexus nexus ? nexus.Name ?? "" : "";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
