using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Wabbajack.App.Avalonia.Converters;

/// <summary>
/// The height WPF gives one line of text, for a TextBlock's LineHeight.
///
/// Both frameworks start from the font's line spacing (ascent + descent + gap), which for Gabarito is
/// 1.2 em, but WPF rounds the resulting size to the nearest pixel and Avalonia rounds it up. At 12px
/// that is 14 against 15, at 87px 104 against 105, so every such line stood a pixel taller than in the
/// WPF app and everything below it moved down. Setting LineHeight to WPF's rounded value removes that.
/// The values are for this app's own font family; anything else falls back to 1.2 em.
/// </summary>
public class WpfLineHeightConverter : IMultiValueConverter
{
    public static readonly WpfLineHeightConverter Instance = new();

    private readonly Dictionary<FontFamily, double> _spacing = new();

    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count < 2 || values[0] is not double size || values[1] is not FontFamily family)
            return double.NaN;

        return Math.Round(size * LineSpacing(family), MidpointRounding.AwayFromZero);
    }

    private double LineSpacing(FontFamily family)
    {
        if (_spacing.TryGetValue(family, out var cached))
            return cached;

        var spacing = 1.2;
        if (FontManager.Current.TryGetGlyphTypeface(new Typeface(family), out var glyphs))
        {
            var m = glyphs.Metrics;
            spacing = (m.Descent - m.Ascent + m.LineGap) / (double)m.DesignEmHeight;
        }

        _spacing[family] = spacing;
        return spacing;
    }
}
