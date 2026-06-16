using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.VisualTree;
using FluentIcons.Common;

namespace Wabbajack;

// Shared ButtonStyle enum used by both BigButton and WJButton.
// Extended with WJButton's variants (Progress/Transparent/SemiTransparent)
// so the two controls share a single ButtonStyle type in the Wabbajack namespace
// (avoids a duplicate-type clash).
public enum ButtonStyle { Mono, Color, Danger, Progress, Transparent, SemiTransparent }

public partial class BigButton : UserControl
{
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<BigButton, string?>(nameof(Title));
    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<BigButton, string?>(nameof(Description));
    public static readonly StyledProperty<Symbol> IconProperty =
        AvaloniaProperty.Register<BigButton, Symbol>(nameof(Icon));
    public static readonly StyledProperty<ICommand?> CommandProperty =
        AvaloniaProperty.Register<BigButton, ICommand?>(nameof(Command));
    public static readonly StyledProperty<ButtonStyle> ButtonStyleProperty =
        AvaloniaProperty.Register<BigButton, ButtonStyle>(nameof(ButtonStyle), ButtonStyle.Mono);

    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }
    public Symbol Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }
    public ICommand? Command { get => GetValue(CommandProperty); set => SetValue(CommandProperty, value); }
    public ButtonStyle ButtonStyle { get => GetValue(ButtonStyleProperty); set => SetValue(ButtonStyleProperty, value); }

    public BigButton() => InitializeComponent();
    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    protected override void OnApplyTemplate(Avalonia.Controls.Primitives.TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        ApplyButtonStyle();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        ApplyButtonStyle();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ButtonStyleProperty) ApplyButtonStyle();
    }

    // Mirrors the WPF BigButtonStyle / BigColorButtonStyle: Color = lavender fill with dark
    // text/icon; Mono = dark fill with light text + lavender icon; Danger = error-red fill.
    private void ApplyButtonStyle()
    {
        var btn = this.FindControl<Button>("PART_Button");
        if (btn is null) return;
        var title = this.FindControl<TextBlock>("PART_Title");
        var desc = this.FindControl<TextBlock>("PART_Desc");
        var icon = this.FindControl<FluentIcons.Avalonia.SymbolIcon>("PART_Icon");

        IBrush? bg, fg, iconFg;
        switch (ButtonStyle)
        {
            case ButtonStyle.Color:
            case ButtonStyle.Progress:
                bg = Brush("PrimaryBrush"); fg = Brush("BackgroundBrush"); iconFg = Brush("BackgroundBrush");
                break;
            case ButtonStyle.Danger:
                bg = Brush("ErrorBrush"); fg = Brush("ForegroundBrush"); iconFg = Brush("ForegroundBrush");
                break;
            default: // Mono / Transparent / SemiTransparent
                bg = Brush("ComplementaryPrimary08Brush"); fg = Brush("ForegroundBrush"); iconFg = Brush("PrimaryBrush");
                break;
        }

        if (bg is not null) btn.Background = bg;
        if (title is not null && fg is not null) title.Foreground = fg;
        if (desc is not null && fg is not null) desc.Foreground = fg;
        if (icon is not null && iconFg is not null) icon.Foreground = iconFg;
    }

    private IBrush? Brush(string key) => this.TryFindResource(key, out var v) ? v as IBrush : null;
}
