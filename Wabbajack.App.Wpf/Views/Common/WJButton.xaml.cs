using FluentIcons.Common;
using ReactiveUI;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Windows;
using System;
using Wabbajack.RateLimiter;
using System.Windows.Media;
using System.Windows.Controls;
using System.ComponentModel;

namespace Wabbajack;

/// <summary>
/// Interaction logic for WJButton.xaml
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

public partial class WJButton : Button, IViewFor<WJButtonVM>, IReactiveObject
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
            RaisePropertyChanged(new PropertyChangedEventArgs(nameof(Content)));
        }
    }
    public Symbol Icon
    {
        get => (Symbol)GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }
    public static readonly DependencyProperty IconProperty = DependencyProperty.Register(nameof(Icon), typeof(Symbol), typeof(WJButton), new FrameworkPropertyMetadata(default(Symbol), FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, WireNotifyPropertyChanged));

    public IconVariant? IconVariant
    {
        get => (IconVariant?)GetValue(IconVariantProperty);
        set => SetValue(IconVariantProperty, value);
    }
    public static readonly DependencyProperty IconVariantProperty = DependencyProperty.Register(nameof(IconVariant), typeof(IconVariant?), typeof(WJButton), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, WireNotifyPropertyChanged));

    public double IconSize
    {
        get => (double)GetValue(IconSizeProperty);
        set => SetValue(IconSizeProperty, value);
    }
    public static readonly DependencyProperty IconSizeProperty = DependencyProperty.Register(nameof(IconSize), typeof(double), typeof(WJButton), new FrameworkPropertyMetadata(24D, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, WireNotifyPropertyChanged));
    public FlowDirection Direction
    {
        get => (FlowDirection)GetValue(DirectionProperty);
        set => SetValue(DirectionProperty, value);
    }
    public static readonly DependencyProperty DirectionProperty = DependencyProperty.Register(nameof(Direction), typeof(FlowDirection), typeof(WJButton), new FrameworkPropertyMetadata(default(FlowDirection), FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, WireNotifyPropertyChanged));
    public ButtonStyle ButtonStyle
    {
        get => (ButtonStyle)GetValue(ButtonStyleProperty);
        set => SetValue(ButtonStyleProperty, value);
    }
    public static readonly DependencyProperty ButtonStyleProperty = DependencyProperty.Register(nameof(ButtonStyle), typeof(ButtonStyle), typeof(WJButton), new FrameworkPropertyMetadata(default(ButtonStyle), FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, WireNotifyPropertyChanged));

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

            this.WhenAnyValue(x => x.ButtonStyle)
                .Subscribe(x => Style = x switch
                {
                    ButtonStyle.Mono => (Style)Application.Current.Resources["WJButtonStyle"],
                    ButtonStyle.Color => (Style)Application.Current.Resources["WJColorButtonStyle"],
                    ButtonStyle.Danger => (Style)Application.Current.Resources["WJDangerButtonStyle"],
                    ButtonStyle.Progress => (Style)Application.Current.Resources["WJColorButtonStyle"],
                    ButtonStyle.Transparent => (Style)Application.Current.Resources["TransparentButtonStyle"],
                    ButtonStyle.SemiTransparent => (Style)Application.Current.Resources["WJSemiTransparentButtonStyle"],
                    _ => (Style)Application.Current.Resources["WJButtonStyle"],
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
                    ButtonTextBlock.ClearValue(ForegroundProperty);
                    ButtonSymbolIcon.ClearValue(ForegroundProperty);
                    Style = (Style)Application.Current.Resources["WJColorButtonStyle"];
                }
                else
                {
                    var bgBrush = new LinearGradientBrush();

                    bgBrush.StartPoint = new Point(0, 0);
                    bgBrush.EndPoint = new Point(1, 0);
                    bgBrush.GradientStops.Add(new GradientStop((Color)Application.Current.Resources["Primary"], 0.0));
                    bgBrush.GradientStops.Add(new GradientStop((Color)Application.Current.Resources["Primary"], percent.Value));
                    bgBrush.GradientStops.Add(new GradientStop((Color)Application.Current.Resources["ComplementaryPrimary08"], percent.Value + 0.001));
                    bgBrush.GradientStops.Add(new GradientStop((Color)Application.Current.Resources["ComplementaryPrimary08"], 1.0));
                    Background = bgBrush;

                    var textBrush = new LinearGradientBrush();
                    var textStartPercent = 1 - (ActualWidth - ButtonTextBlock.Margin.Left) / ActualWidth;
                    var textModifier = ActualWidth / (ActualWidth - ButtonTextBlock.Margin.Left); 
                    var textPercent = percent.Value < textStartPercent ? 0 : (percent.Value - textStartPercent) * textModifier;
                    // Since the text has a smaller width compared to the background of the whole button, we need to scale the gradient to the same bounds
                    textBrush.RelativeTransform = new ScaleTransform(ActualWidth / ButtonTextBlock.ActualWidth, 1);
                    textBrush.StartPoint = new Point(0, 0);
                    textBrush.EndPoint = new Point(1, 0);
                    textBrush.GradientStops.Add(new GradientStop((Color)Application.Current.Resources["BackgroundColor"], 0.0));
                    textBrush.GradientStops.Add(new GradientStop((Color)Application.Current.Resources["BackgroundColor"], textPercent));
                    textBrush.GradientStops.Add(new GradientStop((Color)Application.Current.Resources["DisabledForegroundColor"], textPercent + 0.001));
                    textBrush.GradientStops.Add(new GradientStop((Color)Application.Current.Resources["DisabledForegroundColor"], 1.0));
                    ButtonTextBlock.Foreground = textBrush;

                    var iconBrush = new LinearGradientBrush();
                    var iconStartPercent = (ActualWidth - ButtonSymbolIcon.ActualWidth - ButtonSymbolIcon.Margin.Right) / ActualWidth;
                    var iconModifier = ActualWidth / (ActualWidth - ButtonSymbolIcon.ActualWidth - ButtonSymbolIcon.Margin.Right); 
                    var iconPercent = percent.Value < iconStartPercent ? 0 : (percent.Value - iconStartPercent) * iconModifier;
                    iconBrush.RelativeTransform = new ScaleTransform(ActualWidth / ButtonSymbolIcon.ActualWidth, 1);
                    iconBrush.StartPoint = new Point(0, 0);
                    iconBrush.EndPoint = new Point(1, 0);
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
    private static void WireNotifyPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (Equals(e.OldValue, e.NewValue)) return;
        ((WJButton)d).RaisePropertyChanged(e.Property.Name);
    }
}
