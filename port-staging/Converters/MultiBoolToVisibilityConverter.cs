using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia.Data.Converters;

namespace Wabbajack;

public class MultiBoolToVisibilityConverter : IMultiValueConverter
{
    public object Convert(IList<object> values, Type targetType, object parameter, CultureInfo culture)
    {
        // Ported from WPF Visibility to a bool, intended to be bound directly to IsVisible.
        if (values.All(v => v is bool b && b)) return true;
        return false;
    }
}
