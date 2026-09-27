using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using FluentIcons.Avalonia;
using FluentIcons.Common;

namespace Wabbajack.App.Avalonia.Controls;

/// <summary>
/// The WPF app's BigButton: a large main button with a title, an icon at the right of the title and a line
/// of description under both, 16px in from the edge. <see cref="ButtonStyle" /> Mono is the plain main
/// button; Color is the primary-filled one Home's Get Started uses. The looks are the "main big" classes.
/// </summary>
public class BigButton : Button
{
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<BigButton, string?>(nameof(Title));

    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<BigButton, string?>(nameof(Description));

    public static readonly StyledProperty<Symbol> IconProperty =
        AvaloniaProperty.Register<BigButton, Symbol>(nameof(Icon), Symbol.ImageTable);

    public static readonly StyledProperty<ButtonStyle> ButtonStyleProperty =
        AvaloniaProperty.Register<BigButton, ButtonStyle>(nameof(ButtonStyle));

    private readonly TextBlock _title = new()
    {
        VerticalAlignment = VerticalAlignment.Center,
        FontWeight = FontWeight.DemiBold,
        FontSize = 24,
        Margin = new Thickness(0, 0, 0, 4)
    };

    private readonly SymbolIcon _icon = new() { VerticalAlignment = VerticalAlignment.Center, FontSize = 28 };

    private readonly TextBlock _description = new() { VerticalAlignment = VerticalAlignment.Center, FontSize = 13 };

    protected override Type StyleKeyOverride => typeof(Button);

    public BigButton()
    {
        Classes.Add("main");
        Classes.Add("big");
        ClipToBounds = true;

        // WPF's LineHeight 20 on the description; see the wpfLineHeight20 style.
        _description.Classes.Add("wpfLineHeight20");
        _icon.Symbol = Icon;

        Grid.SetColumn(_icon, 1);
        Grid.SetRow(_description, 1);
        Grid.SetColumnSpan(_description, 2);
        Content = new Grid
        {
            Margin = new Thickness(16),
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            RowDefinitions = new RowDefinitions("Auto,Auto"),
            Children = { _title, _icon, _description }
        };
    }

    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string? Description
    {
        get => GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    public Symbol Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public ButtonStyle ButtonStyle
    {
        get => GetValue(ButtonStyleProperty);
        set => SetValue(ButtonStyleProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == TitleProperty) _title.Text = Title;
        else if (change.Property == DescriptionProperty) _description.Text = Description;
        else if (change.Property == IconProperty) _icon.Symbol = Icon;
        else if (change.Property == ButtonStyleProperty) Classes.Set("color", ButtonStyle == ButtonStyle.Color);
    }
}
