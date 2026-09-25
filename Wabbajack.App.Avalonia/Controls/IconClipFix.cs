using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.VisualTree;
using FluentIcons.Avalonia;

namespace Wabbajack.App.Avalonia.Controls;

/// <summary>
///     Stops FluentIcons cutting off the edges of glyphs that reach past their em box.
///     <para>
///         Some Fluent glyphs draw beyond the square their font size describes: the check-mark badge of
///         PersonAvailable overhangs it by about 8% of the size, two pixels at 24. FluentIcons 2.0 clips twice at
///         that square: its inner control pushes a clip of exactly its own bounds (the upstream bug fixed in 2.1.339,
///         which needs Avalonia 12), and the icon element itself clips its children to its bounds. So the badge
///         came out flat on its right and bottom where the WPF app drew it round.
///     </para>
///     <para>
///         Here the icon stops clipping, and its inner panel is arranged a quarter of the size beyond the icon on
///         every side (a negative margin, with its size and alignment bindings dropped so it fills that), which
///         moves the inner clip out of the glyph's way. The icon's minimum size is its font size, so the negative
///         margin does not shrink what it asks for. The glyph is centred in the larger box, so it lands where it
///         did; renders of glyphs that fit their box are unchanged.
///     </para>
/// </summary>
public static class IconClipFix
{
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

        var slack = Math.Max(2, Math.Ceiling(icon.FontSize / 4));
        panel.ClearValue(Layoutable.WidthProperty);
        panel.ClearValue(Layoutable.HeightProperty);
        panel.ClearValue(Layoutable.HorizontalAlignmentProperty);
        panel.ClearValue(Layoutable.VerticalAlignmentProperty);
        panel.Margin = new Thickness(-slack);

        icon.ClipToBounds = false;
        icon.MinWidth = icon.FontSize;
        icon.MinHeight = icon.FontSize;
    }
}
