using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using FluentIcons.Common;
using Wabbajack.RateLimiter;

namespace Wabbajack;

// NOTE: The ButtonStyle enum is shared with BigButton (declared in BigButton.axaml.cs,
// namespace Wabbajack). It was extended there to include WJButton's variants
// (Mono, Color, Danger, Progress, Transparent, SemiTransparent) so we reuse it here
// rather than declaring a second, clashing ButtonStyle type.

public partial class WJButton : UserControl
{
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<WJButton, string?>(nameof(Text));
    public static readonly StyledProperty<Symbol> IconProperty =
        AvaloniaProperty.Register<WJButton, Symbol>(nameof(Icon));
    public static readonly StyledProperty<IconVariant> IconVariantProperty =
        AvaloniaProperty.Register<WJButton, IconVariant>(nameof(IconVariant), IconVariant.Regular);
    public static readonly StyledProperty<double> IconSizeProperty =
        AvaloniaProperty.Register<WJButton, double>(nameof(IconSize), 18d);
    public static readonly StyledProperty<ICommand?> CommandProperty =
        AvaloniaProperty.Register<WJButton, ICommand?>(nameof(Command));
    public static readonly StyledProperty<ButtonStyle> ButtonStyleProperty =
        AvaloniaProperty.Register<WJButton, ButtonStyle>(nameof(ButtonStyle), ButtonStyle.Mono);

    // Included for API completeness with the WPF control. The per-frame gradient
    // progress fill is DEFERRED; ButtonStyle.Progress simply renders as a solid
    // Color-style button for now.
    public static readonly StyledProperty<Percent> ProgressPercentageProperty =
        AvaloniaProperty.Register<WJButton, Percent>(nameof(ProgressPercentage), Percent.One);

    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public Symbol Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }
    public IconVariant IconVariant { get => GetValue(IconVariantProperty); set => SetValue(IconVariantProperty, value); }
    public double IconSize { get => GetValue(IconSizeProperty); set => SetValue(IconSizeProperty, value); }
    public ICommand? Command { get => GetValue(CommandProperty); set => SetValue(CommandProperty, value); }
    public ButtonStyle ButtonStyle { get => GetValue(ButtonStyleProperty); set => SetValue(ButtonStyleProperty, value); }
    public Percent ProgressPercentage { get => GetValue(ProgressPercentageProperty); set => SetValue(ProgressPercentageProperty, value); }

    public WJButton()
    {
        InitializeComponent();
        UpdateStyleClass();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ButtonStyleProperty)
            UpdateStyleClass();
    }

    // Drive the visual variant via a pseudo-style class on the root control so the
    // ButtonStyle-keyed selectors in WJButton.axaml can pick the right background.
    private void UpdateStyleClass()
    {
        Classes.Set("mono", ButtonStyle == ButtonStyle.Mono);
        Classes.Set("color", ButtonStyle == ButtonStyle.Color || ButtonStyle == ButtonStyle.Progress);
        Classes.Set("danger", ButtonStyle == ButtonStyle.Danger);
        Classes.Set("transparent", ButtonStyle == ButtonStyle.Transparent);
        Classes.Set("semitransparent", ButtonStyle == ButtonStyle.SemiTransparent);
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
