using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Data.Converters;

namespace Wabbajack;

/// <summary>
/// Avalonia replacement for the WPF-only hexinnovation MathConverter. Evaluates the arithmetic
/// expression passed as ConverterParameter with the bound value(s) substituted for the variables
/// x, y, z (in binding order), e.g. ConverterParameter="x*9/16", "Round(x*1.5)" or "(x-(y/1.5))".
/// Implements IMultiValueConverter as well as IValueConverter because the WPF original was used
/// from MultiBindings too (see InstallationView.axaml).
/// </summary>
public class MathConverter : IValueConverter, IMultiValueConverter
{
    private static readonly string[] Variables = ["x", "y", "z"];

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null || parameter is not string expression)
            return AvaloniaProperty.UnsetValue;

        if (!TryToDouble(value, out var x))
            return AvaloniaProperty.UnsetValue;

        return Evaluate(expression, [x], targetType);
    }

    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (parameter is not string expression || values is null || values.Count == 0)
            return AvaloniaProperty.UnsetValue;

        var numbers = new double[values.Count];
        for (var i = 0; i < values.Count; i++)
        {
            if (values[i] is null || !TryToDouble(values[i]!, out numbers[i]))
                return AvaloniaProperty.UnsetValue;
        }

        return Evaluate(expression, numbers, targetType);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    private static object? Evaluate(string expression, double[] values, Type targetType)
    {
        try
        {
            // Substituted values are numeric, so a later pass can never rewrite an earlier one.
            var expr = expression;
            foreach (var (name, index) in Variables.Select((n, i) => (n, i)))
            {
                if (index >= values.Length) break;
                expr = expr.Replace(name, values[index].ToString(CultureInfo.InvariantCulture),
                                    StringComparison.OrdinalIgnoreCase);
            }

            // Round(...) isn't supported by DataTable.Compute; fold it away first. The digits
            // argument is optional - the gallery's size labels use Round(x,1), and feeding
            // "123.456,1" straight to Compute throws, which blanked the label entirely.
            var rounded = false;
            var digits = 0;
            if (expr.StartsWith("Round(", StringComparison.OrdinalIgnoreCase) && expr.EndsWith(")", StringComparison.Ordinal))
            {
                rounded = true;
                expr = expr["Round(".Length..^1];

                var comma = expr.LastIndexOf(',');
                if (comma >= 0 && int.TryParse(expr[(comma + 1)..].Trim(), NumberStyles.Integer,
                                               CultureInfo.InvariantCulture, out var parsedDigits))
                {
                    digits = parsedDigits;
                    expr = expr[..comma];
                }
            }

            var result = new DataTable().Compute(expr, null);
            if (result is null || result == DBNull.Value) return AvaloniaProperty.UnsetValue;

            var d = System.Convert.ToDouble(result, CultureInfo.InvariantCulture);
            if (rounded) d = Math.Round(d, digits);
            return targetType == typeof(int) ? (int)d : d;
        }
        catch
        {
            return AvaloniaProperty.UnsetValue;
        }
    }

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
