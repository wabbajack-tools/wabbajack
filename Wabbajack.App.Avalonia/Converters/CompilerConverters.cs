using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Wabbajack.App.Avalonia.ViewModels.Compiler;

namespace Wabbajack.App.Avalonia.Converters;

public static class CompilerConverters
{
    /// <summary>
    ///     LeftMarginMultiplierConverter with Length 16: a file tree row is indented 16px for each folder it sits in.
    /// </summary>
    public static readonly FuncValueConverter<int, Thickness> TreeIndent = new(level => new Thickness(16 * level, 0, 0, 0));

    /// <summary>
    ///     The file tree row's background from its item's contained states and its own, in the order the WPF
    ///     template's triggers ran, so the last that applies wins as it did there. That order is kept as it was,
    ///     oddities included: Ignore + Always Enabled + No Match Include gets the Include pattern.
    ///     Values: ContainsNoMatchIncludes, ContainsIncludes, ContainsIgnores, ContainsAlwaysEnableds, CompilerFileState.
    /// </summary>
    public static readonly IMultiValueConverter RowBackground = new RowBackgroundConverter();

    private sealed class RowBackgroundConverter : IMultiValueConverter
    {
        public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        {
            if (values.Count < 5) return Brushes.Transparent;
            var noMatch = values[0] is true;
            var inc = values[1] is true;
            var ign = values[2] is true;
            var ae = values[3] is true;
            var hasState = values[4] is CompilerFileState;

            var rules = new (bool When, string Key)[]
            {
                (noMatch, "DiagonalStripeBrush"),
                (inc, "InverseDiagonalStripeBrush"),
                (ign, "VerticalStripeBrush"),
                (ae, "HorizontalStripeBrush"),
                (ign && ae, "VerticalHorizontalStripeBrush"),
                (ign && noMatch, "VerticalDiagonalStripeBrush"),
                (ign && inc, "VerticalInverseDiagonalStripeBrush"),
                (ae && noMatch, "HorizontalDiagonalStripeBrush"),
                (ae && inc, "HorizontalInverseDiagonalStripeBrush"),
                (noMatch && inc, "DiagonalInverseDiagonalStripeBrush"),
                (ae && noMatch && inc, "HorizontalDiagonalInverseDiagonalStripeBrush"),
                (ign && noMatch && inc, "VerticalDiagonalInverseDiagonalStripeBrush"),
                (ign && ae && inc, "VerticalHorizontalInverseDiagonalStripeBrush"),
                (ign && ae && noMatch, "VerticalHorizontalInverseDiagonalStripeBrush"),
                (ign && ae && noMatch && inc, "VerticalHorizontalDiagonalInverseDiagonalStripeBrush"),
                (hasState, "ComplementaryPrimary32Brush")
            };

            var key = rules.LastOrDefault(r => r.When).Key;
            return key == null ? Brushes.Transparent : PreflightConverters.Brush(key);
        }
    }
}
