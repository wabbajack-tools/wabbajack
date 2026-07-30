using System;
using System.Data;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;

namespace Wabbajack;

/// <summary>
/// Avalonia replacement for the WPF-only hexinnovation MathConverter. Evaluates the arithmetic
/// expression passed as ConverterParameter with "x" substituted by the bound value, e.g.
/// ConverterParameter="x*9/16" or "Round(x*1.5)".
/// </summary>
public class MathConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null || parameter is not string expression)
            return AvaloniaProperty.UnsetValue;

        if (!TryToDouble(value, out var x))
            return AvaloniaProperty.UnsetValue;

        try
        {
            // Round(...) isn't supported by DataTable.Compute; fold it away first.
            var expr = expression.Replace("x", x.ToString(CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase);
            var rounded = false;
            if (expr.StartsWith("Round(", StringComparison.OrdinalIgnoreCase) && expr.EndsWith(")", StringComparison.Ordinal))
            {
                rounded = true;
                expr = expr["Round(".Length..^1];
            }

            var result = new DataTable().Compute(expr, null);
            if (result is null || result == DBNull.Value) return AvaloniaProperty.UnsetValue;

            var d = System.Convert.ToDouble(result, CultureInfo.InvariantCulture);
            if (rounded) d = Math.Round(d);
            return targetType == typeof(int) ? (int)d : d;
        }
        catch
        {
            return AvaloniaProperty.UnsetValue;
        }
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    private static bool TryToDouble(object value, out double result)
    {
        switch (value)
        {
            case double d: result = d; return true;
            case int i: result = i; return true;
            case float f: result = f; return true;
            case decimal m: result = (double)m; return true;
            default:
                return double.TryParse(value.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out result);
        }
    }
}
