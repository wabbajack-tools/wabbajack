using System.Reactive.Linq;
using Avalonia;
using Avalonia.ReactiveUI;
using ReactiveUI;
using System.Reactive.Disposables;

namespace Wabbajack;

/// <summary>
/// Interaction logic for TopProgressView.axaml
/// </summary>
public partial class TopProgressView : ReactiveUserControl<ViewModel>
{
    public static readonly StyledProperty<double> ProgressPercentProperty =
        AvaloniaProperty.Register<TopProgressView, double>(nameof(ProgressPercent), defaultValue: default(double));

    public double ProgressPercent
    {
        get => GetValue(ProgressPercentProperty);
        set => SetValue(ProgressPercentProperty, value);
    }

    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<TopProgressView, string>(nameof(Title), defaultValue: default(string));

    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public static readonly StyledProperty<string> StatePrefixTitleProperty =
        AvaloniaProperty.Register<TopProgressView, string>(nameof(StatePrefixTitle), defaultValue: default(string));

    public string StatePrefixTitle
    {
        get => GetValue(StatePrefixTitleProperty);
        set => SetValue(StatePrefixTitleProperty, value);
    }

    public static readonly StyledProperty<bool> OverhangShadowProperty =
        AvaloniaProperty.Register<TopProgressView, bool>(nameof(OverhangShadow), defaultValue: true);

    public bool OverhangShadow
    {
        get => GetValue(OverhangShadowProperty);
        set => SetValue(OverhangShadowProperty, value);
    }

    public static readonly StyledProperty<bool> ShadowMarginProperty =
        AvaloniaProperty.Register<TopProgressView, bool>(nameof(ShadowMargin), defaultValue: true);

    public bool ShadowMargin
    {
        get => GetValue(ShadowMarginProperty);
        set => SetValue(ShadowMarginProperty, value);
    }

    public TopProgressView()
    {
        InitializeComponent();
        this.WhenActivated(dispose =>
        {
            this.WhenAny(x => x.ProgressPercent)
                .Select(x => 0.3 + x * 0.7)
                .BindToStrict(this, x => x.LargeProgressBar.Opacity)
                .DisposeWith(dispose);
            this.WhenAny(x => x.ProgressPercent)
                .BindToStrict(this, x => x.LargeProgressBar.Value)
                .DisposeWith(dispose);
            this.WhenAny(x => x.ProgressPercent)
                .BindToStrict(this, x => x.BottomProgressBarDarkGlow.Value)
                .DisposeWith(dispose);
            this.WhenAny(x => x.ProgressPercent)
                .BindToStrict(this, x => x.LargeProgressBarTopGlow.Value)
                .DisposeWith(dispose);
            this.WhenAny(x => x.ProgressPercent)
                .BindToStrict(this, x => x.BottomProgressBarBrightGlow1.Value)
                .DisposeWith(dispose);
            this.WhenAny(x => x.ProgressPercent)
                .BindToStrict(this, x => x.BottomProgressBarBrightGlow2.Value)
                .DisposeWith(dispose);
            this.WhenAny(x => x.ProgressPercent)
                .BindToStrict(this, x => x.BottomProgressBar.Value)
                .DisposeWith(dispose);
            this.WhenAny(x => x.ProgressPercent)
                .BindToStrict(this, x => x.BottomProgressBarHighlight.Value)
                .DisposeWith(dispose);

            // WPF bound Visibility (Visible/Collapsed) through a bool -> Avalonia binds
            // IsVisible (bool) directly, so the Visibility enum Select is dropped.
            this.WhenAny(x => x.OverhangShadow)
                .BindToStrict(this, x => x.OverhangShadowRect.IsVisible)
                .DisposeWith(dispose);
            this.WhenAny(x => x.ShadowMargin)
                .DistinctUntilChanged()
                .Select(x => x ? new Thickness(6, 0, 6, 0) : new Thickness(0))
                .BindToStrict(this, x => x.OverhangShadowRect.Margin)
                .DisposeWith(dispose);
            this.WhenAny(x => x.Title)
                .BindToStrict(this, x => x.TitleText.Text)
                .DisposeWith(dispose);
            this.WhenAny(x => x.StatePrefixTitle)
                .Select(x => x == null)
                .BindToStrict(this, x => x.PrefixSpacerRect.IsVisible)
                .DisposeWith(dispose);
            this.WhenAny(x => x.StatePrefixTitle)
                .Select(x => x != null)
                .BindToStrict(this, x => x.StatePrefixText.IsVisible)
                .DisposeWith(dispose);
            this.WhenAny(x => x.StatePrefixTitle)
                .BindToStrict(this, x => x.StatePrefixText.Text)
                .DisposeWith(dispose);
        });
    }
}
