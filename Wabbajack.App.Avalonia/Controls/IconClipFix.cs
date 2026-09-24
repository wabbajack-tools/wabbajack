using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.VisualTree;
using FluentIcons.Avalonia;

namespace Wabbajack.App.Avalonia.Controls;

/// <summary>
///     Stops FluentIcons cutting the right edge off its glyphs at fractional display scales.
///     <para>
///         A <see cref="SymbolIcon" /> draws its glyph in an inner control sized to exactly FontSize, clipped to
///         that box, with the glyph placed at (width - size) / 2. At 125% or 175% layout rounding can leave the box
///         a fraction of a device pixel short of the glyph, the glyph's origin then snaps a pixel to the right, and
///         its right edge falls outside the clip. At 100% the box and glyph match exactly, which is why it never
///         showed in the comparisons against WPF.
///     </para>
///     <para>
///         The fix gives the inner panel a pixel of slack on every side without changing what the icon asks for:
///         the panel is arranged a pixel beyond the icon all round (negative margin), its own size bindings are
///         dropped so it fills that, and the icon's minimum size is its font size so the negative margin does not
///         shrink the icon either. The glyph is centred in the larger box, so it lands exactly where it did; only
///         the clip moves out of its way.
///     </para>
/// </summary>
public static class IconClipFix
{
    private const double Slack = 1;

    public static void Register()
    {
        Control.LoadedEvent.AddClassHandler<SymbolIcon>((icon, _) => Apply(icon));
        SymbolIcon.FontSizeProperty.Changed.AddClassHandler<SymbolIcon>((icon, _) =>
        {
            if (icon.IsLoaded) Apply(icon);
        });
    }

    private static void Apply(SymbolIcon icon)
    {
        if (icon.GetVisualChildren().FirstOrDefault() is not Panel panel) return;

        panel.ClearValue(Layoutable.WidthProperty);
        panel.ClearValue(Layoutable.HeightProperty);
        panel.ClearValue(Layoutable.HorizontalAlignmentProperty);
        panel.ClearValue(Layoutable.VerticalAlignmentProperty);
        panel.Margin = new Thickness(-Slack);

        icon.MinWidth = icon.FontSize;
        icon.MinHeight = icon.FontSize;
    }
}
