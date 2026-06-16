using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
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
}
