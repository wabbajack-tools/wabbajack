using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using FluentIcons.Common;
using ReactiveUI;
using System;
using System.ComponentModel;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Wabbajack.RateLimiter;

namespace Wabbajack;

/// <summary>
/// Interaction logic for WJButton.axaml
/// </summary>
public enum ButtonStyle
{
    Mono,
    Color,
    Danger,
    Progress,
    Transparent,
    SemiTransparent
}
public partial class WJButtonVM : ViewModel
{
}

public partial class WJButton : Button, IViewFor<WJButtonVM>, IReactiveObject, IActivatableView
{
    private string _text;

    public event PropertyChangedEventHandler PropertyChanged;
    public event PropertyChangingEventHandler PropertyChanging;

    public string Text
    {
        get => _text;
        set
        {
            this.RaiseAndSetIfChanged(ref _text, value);
            // NOTE: kept verbatim from the WPF original, which raises a change notification
            // for "Content" here rather than "Text". This looks like a leftover from an
            // earlier implementation that bound directly to Button.Content; preserved as-is
            // for a faithful port.
            RaisePropertyChanged(new PropertyChangedEventArgs(nameof(Content)));
        }
    }

    public Symbol Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }
    public static readonly StyledProperty<Symbol> IconProperty =
        AvaloniaProperty.Register<WJButton, Symbol>(nameof(Icon), defaultBindingMode: BindingMode.TwoWay);

    public IconVariant? IconVariant
    {
        get => GetValue(IconVariantProperty);
        set => SetValue(IconVariantProperty, value);
    }
    public static readonly StyledProperty<IconVariant?> IconVariantProperty =
        AvaloniaProperty.Register<WJButton, IconVariant?>(nameof(IconVariant), defaultBindingMode: BindingMode.TwoWay);

    public double IconSize
    {
        get => GetValue(IconSizeProperty);
        set => SetValue(IconSizeProperty, value);
    }
    public static readonly StyledProperty<double> IconSizeProperty =
        AvaloniaProperty.Register<WJButton, double>(nameof(IconSize), 24D, defaultBindingMode: BindingMode.TwoWay);

    public FlowDirection Direction
    {
        get => GetValue(DirectionProperty);
        set => SetValue(DirectionProperty, value);
    }
    public static readonly StyledProperty<FlowDirection> DirectionProperty =
        AvaloniaProperty.Register<WJButton, FlowDirection>(nameof(Direction), defaultBindingMode: BindingMode.TwoWay);

    public ButtonStyle ButtonStyle
    {
        get => GetValue(ButtonStyleProperty);
        set => SetValue(ButtonStyleProperty, value);
    }
    public static readonly StyledProperty<ButtonStyle> ButtonStyleProperty =
        AvaloniaProperty.Register<WJButton, ButtonStyle>(nameof(ButtonStyle), defaultBindingMode: BindingMode.TwoWay);

    private Percent _progressPercentage = Percent.One;
    public Percent ProgressPercentage
    {
        get => _progressPercentage;
        set
        {
            this.RaiseAndSetIfChanged(ref _progressPercentage, value);
        }
    }

    public WJButtonVM ViewModel { get; set; }
    object IViewFor.ViewModel { get => ViewModel; set => ViewModel = (WJButtonVM)value; }

    public WJButton()
    {
        InitializeComponent();
        this.WhenActivated(dispose =>
        {
            this.WhenAnyValue(x => x.Text)
                .BindToStrict(this, x => x.ButtonTextBlock.Text)
                .DisposeWith(dispose);

            this.WhenAnyValue(x => x.Icon)
                .BindToStrict(this, x => x.ButtonSymbolIcon.Symbol)
                .DisposeWith(dispose);

            this.WhenAnyValue(x => x.IconVariant)
                .ObserveOnGuiThread()
                .Subscribe((variant) =>
                {
                    if(variant != null)
                    {
                        ButtonSymbolIcon.IconVariant = (IconVariant)variant;
                    }
                })
                .DisposeWith(dispose);

            this.WhenAnyValue(x => x.Direction)
                .Subscribe(x => SetDirection(x))
                .DisposeWith(dispose);

            this.WhenAnyValue(x => x.IconSize)
                .BindToStrict(this, x => x.ButtonSymbolIcon.FontSize)
                .DisposeWith(dispose);

            // WPF's per-control Style property has no direct Avalonia equivalent on Control;
            // Avalonia's closest analog is the Theme property (a ControlTheme resource applied
            // to a single control instance), so the resource lookups below are cast to
            // ControlTheme instead of Style. The resource keys themselves are unchanged.
            this.WhenAnyValue(x => x.ButtonStyle)
                .Subscribe(x => Theme = x switch
                {
                    ButtonStyle.Mono => (ControlTheme)Application.Current.Resources["WJButtonStyle"],
                    ButtonStyle.Color => (ControlTheme)Application.Current.Resources["WJColorButtonStyle"],
                    ButtonStyle.Danger => (ControlTheme)Application.Current.Resources["WJDangerButtonStyle"],
                    ButtonStyle.Progress => (ControlTheme)Application.Current.Resources["WJColorButtonStyle"],
                    ButtonStyle.Transparent => (ControlTheme)Application.Current.Resources["TransparentButtonStyle"],
                    ButtonStyle.SemiTransparent => (ControlTheme)Application.Current.Resources["WJSemiTransparentButtonStyle"],
                    _ => (ControlTheme)Application.Current.Resources["WJButtonStyle"],
                })
                .DisposeWith(dispose);

            this.WhenAnyValue(x => x.ProgressPercentage)
            .ObserveOnGuiThread()
            .Subscribe(percent =>
            {
                if (ButtonStyle != ButtonStyle.Progress) return;
                if (percent == Percent.One)
                {
                    this.ClearValue(BackgroundProperty);
                    // TextBlock declares its own Foreground property distinct from the
                    // TemplatedControl.Foreground that WJButton/ButtonSymbolIcon share, so it
                    // must be qualified explicitly here (bare ForegroundProperty would resolve
                    // to TemplatedControl's, which TextBlock does not use).
                    ButtonTextBlock.ClearValue(TextBlock.ForegroundProperty);
                    ButtonSymbolIcon.ClearValue(ForegroundProperty);
                    Theme = (ControlTheme)Application.Current.Resources["WJColorButtonStyle"];
                }
                else
                {
                    var bgBrush = new LinearGradientBrush();

                    // WPF Point(0,0)/(1,0) here relied on GradientBrush's default
                    // RelativeToBoundingBox mapping mode; Avalonia's StartPoint/EndPoint are
                    // RelativePoint, so the same 0..1 mapping is expressed with RelativeUnit.Relative.
                    bgBrush.StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative);
                    bgBrush.EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative);
                    bgBrush.GradientStops.Add(new GradientStop((Color)Application.Current.Resources["Primary"], 0.0));
                    bgBrush.GradientStops.Add(new GradientStop((Color)Application.Current.Resources["Primary"], percent.Value));
                    bgBrush.GradientStops.Add(new GradientStop((Color)Application.Current.Resources["ComplementaryPrimary08"], percent.Value + 0.001));
                    bgBrush.GradientStops.Add(new GradientStop((Color)Application.Current.Resources["ComplementaryPrimary08"], 1.0));
                    Background = bgBrush;

                    var textBrush = new LinearGradientBrush();
                    var textStartPercent = 1 - (Bounds.Width - ButtonTextBlock.Margin.Left) / Bounds.Width;
                    var textModifier = Bounds.Width / (Bounds.Width - ButtonTextBlock.Margin.Left);
                    var textPercent = percent.Value < textStartPercent ? 0 : (percent.Value - textStartPercent) * textModifier;
                    // Since the text has a smaller width compared to the background of the whole button, we need to scale the gradient to the same bounds
                    // TODO: WPF's Brush.RelativeTransform scales the brush's 0..1 relative gradient
                    // coordinate space before it is mapped onto the element's render bounds.
                    // Avalonia's Brush exposes only an absolute Transform (no RelativeTransform),
                    // which is the closest available analog used here, but its render-space
                    // semantics may not exactly match WPF's - verify the gradient scaling
                    // visually against the WPF version and adjust if it looks off.
                    textBrush.Transform = new ScaleTransform(Bounds.Width / ButtonTextBlock.Bounds.Width, 1);
                    textBrush.StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative);
                    textBrush.EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative);
                    textBrush.GradientStops.Add(new GradientStop((Color)Application.Current.Resources["BackgroundColor"], 0.0));
                    textBrush.GradientStops.Add(new GradientStop((Color)Application.Current.Resources["BackgroundColor"], textPercent));
                    textBrush.GradientStops.Add(new GradientStop((Color)Application.Current.Resources["DisabledForegroundColor"], textPercent + 0.001));
                    textBrush.GradientStops.Add(new GradientStop((Color)Application.Current.Resources["DisabledForegroundColor"], 1.0));
                    ButtonTextBlock.Foreground = textBrush;

                    var iconBrush = new LinearGradientBrush();
                    var iconStartPercent = (Bounds.Width - ButtonSymbolIcon.Bounds.Width - ButtonSymbolIcon.Margin.Right) / Bounds.Width;
                    var iconModifier = Bounds.Width / (Bounds.Width - ButtonSymbolIcon.Bounds.Width - ButtonSymbolIcon.Margin.Right);
                    var iconPercent = percent.Value < iconStartPercent ? 0 : (percent.Value - iconStartPercent) * iconModifier;
                    // TODO: see textBrush.Transform note above - same RelativeTransform -> Transform caveat applies.
                    iconBrush.Transform = new ScaleTransform(Bounds.Width / ButtonSymbolIcon.Bounds.Width, 1);
                    iconBrush.StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative);
                    iconBrush.EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative);
                    iconBrush.GradientStops.Add(new GradientStop((Color)Application.Current.Resources["BackgroundColor"], 0.0));
                    iconBrush.GradientStops.Add(new GradientStop((Color)Application.Current.Resources["BackgroundColor"], iconPercent));
                    iconBrush.GradientStops.Add(new GradientStop((Color)Application.Current.Resources["DisabledForegroundColor"], iconPercent + 0.001));
                    iconBrush.GradientStops.Add(new GradientStop((Color)Application.Current.Resources["DisabledForegroundColor"], 1.0));
                    ButtonSymbolIcon.Foreground = iconBrush;
                }
            }).DisposeWith(dispose);
        });

    }

    private void SetDirection(FlowDirection direction)
    {
        if (direction == FlowDirection.LeftToRight)
        {
            ButtonTextBlock.Margin = new Thickness(16, 0, 0, 0);
            ButtonTextBlock.HorizontalAlignment = HorizontalAlignment.Left;
            ButtonSymbolIcon.Margin = new Thickness(0, 0, 16, 0);
            ButtonSymbolIcon.HorizontalAlignment = HorizontalAlignment.Right;
        }
        else
        {
            ButtonTextBlock.Margin = new Thickness(0, 0, 16, 0);
            ButtonTextBlock.HorizontalAlignment = HorizontalAlignment.Right;
            ButtonSymbolIcon.Margin = new Thickness(16, 0, 0, 0);
            ButtonSymbolIcon.HorizontalAlignment = HorizontalAlignment.Left;
        }
    }

    public void RaisePropertyChanging(PropertyChangingEventArgs args)
    {
        PropertyChanging?.Invoke(this, args);
    }

    public void RaisePropertyChanged(PropertyChangedEventArgs args)
    {
        PropertyChanged?.Invoke(this, args);
    }

    // Replaces the WPF WireNotifyPropertyChanged FrameworkPropertyMetadata callback: Avalonia's
    // AvaloniaProperty.Register has no per-property changed-callback parameter, so the same
    // "notify IReactiveObject subscribers when a styled property changes" behavior is
    // centralized here via the OnPropertyChanged override instead.
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IconProperty
            || change.Property == IconVariantProperty
            || change.Property == IconSizeProperty
            || change.Property == DirectionProperty
            || change.Property == ButtonStyleProperty)
        {
            if (Equals(change.OldValue, change.NewValue)) return;
            RaisePropertyChanged(new PropertyChangedEventArgs(change.Property.Name));
        }
    }
}
