using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using FluentIcons.Avalonia;
using FluentIcons.Common;

namespace Wabbajack.App.Avalonia.Controls;

public enum ButtonStyle
{
    Mono,
    Color,
    Danger,
    Progress,
    Transparent,
    SemiTransparent
}

/// <summary>
/// The WPF app's WJButton: a 200x38 button with its text on one side and an icon on the other, in one of a
/// handful of looks. The looks are style classes (see Controls.axaml); <see cref="ButtonStyle" /> picks one.
/// <para>
/// WPF sized the content grid to the button's whole width, though the content area inside the 1px padding is
/// 2px narrower, so the grid overran by 2px on the right and the icon sat that much further right. The
/// negative margin on the grid keeps that.
/// </para>
/// </summary>
public class WJButton : Button
{
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<WJButton, string?>(nameof(Text));

    public static readonly StyledProperty<Symbol> IconProperty =
        AvaloniaProperty.Register<WJButton, Symbol>(nameof(Icon));

    public static readonly StyledProperty<IconVariant?> IconVariantProperty =
        AvaloniaProperty.Register<WJButton, IconVariant?>(nameof(IconVariant));

    public static readonly StyledProperty<double> IconSizeProperty =
        AvaloniaProperty.Register<WJButton, double>(nameof(IconSize), 24);

    public static readonly StyledProperty<FlowDirection> DirectionProperty =
        AvaloniaProperty.Register<WJButton, FlowDirection>(nameof(Direction));

    public static readonly StyledProperty<ButtonStyle> ButtonStyleProperty =
        AvaloniaProperty.Register<WJButton, ButtonStyle>(nameof(ButtonStyle));

    private readonly TextBlock _text = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly SymbolIcon _icon = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly Grid _grid;

    protected override Type StyleKeyOverride => typeof(Button);

    public WJButton()
    {
        Width = 200;
        Height = 38;
        ClipToBounds = true;
        Classes.Add("wj");

        _grid = new Grid
        {
            Margin = new Thickness(0, 0, -2, 0),
            Children = { _text, _icon }
        };
        Content = _grid;

        ApplyDirection();
        ApplyStyle();
        _icon.FontSize = IconSize;
    }

    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public Symbol Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public IconVariant? IconVariant
    {
        get => GetValue(IconVariantProperty);
        set => SetValue(IconVariantProperty, value);
    }

    public double IconSize
    {
        get => GetValue(IconSizeProperty);
        set => SetValue(IconSizeProperty, value);
    }

    public FlowDirection Direction
    {
        get => GetValue(DirectionProperty);
        set => SetValue(DirectionProperty, value);
    }

    public ButtonStyle ButtonStyle
    {
        get => GetValue(ButtonStyleProperty);
        set => SetValue(ButtonStyleProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        // WPF bound the grid's width to the button's, so the text and the icon sit at its two ends.
        if (change.Property == BoundsProperty)
            _grid.Width = Bounds.Width;
        else if (change.Property == TextProperty)
            _text.Text = Text;
        else if (change.Property == IconProperty)
            _icon.Symbol = Icon;
        else if (change.Property == IconVariantProperty)
        {
            // Unset means "whatever the style says", which is how WPF left it.
            if (IconVariant is { } variant) _icon.IconVariant = variant;
            else _icon.ClearValue(SymbolIcon.IconVariantProperty);
        }
        else if (change.Property == IconSizeProperty)
            _icon.FontSize = IconSize;
        else if (change.Property == DirectionProperty)
            ApplyDirection();
        else if (change.Property == ButtonStyleProperty)
            ApplyStyle();
    }

    private void ApplyDirection()
    {
        var ltr = Direction == FlowDirection.LeftToRight;
        _text.Margin = ltr ? new Thickness(16, 0, 0, 0) : new Thickness(0, 0, 16, 0);
        _text.HorizontalAlignment = ltr ? HorizontalAlignment.Left : HorizontalAlignment.Right;
        _icon.Margin = ltr ? new Thickness(0, 0, 16, 0) : new Thickness(16, 0, 0, 0);
        _icon.HorizontalAlignment = ltr ? HorizontalAlignment.Right : HorizontalAlignment.Left;
    }

    private void ApplyStyle()
    {
        Classes.Set("wjColor", ButtonStyle is ButtonStyle.Color or ButtonStyle.Progress);
        Classes.Set("wjDanger", ButtonStyle == ButtonStyle.Danger);
        Classes.Set("wjSemi", ButtonStyle == ButtonStyle.SemiTransparent);
        Classes.Set("wjTransparent", ButtonStyle == ButtonStyle.Transparent);
    }
}
