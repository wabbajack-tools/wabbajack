using System.Reactive.Linq;
using System.Windows;
using ReactiveUI;
using System.Reactive.Disposables;

namespace Wabbajack;

/// <summary>
/// Interaction logic for TopProgressView.xaml
/// </summary>
public partial class TopProgressView : UserControlRx<ViewModel>
{
    public double ProgressPercent
    {
        get => (double)GetValue(ProgressPercentProperty);
        set => SetValue(ProgressPercentProperty, value);
    }
    public static readonly DependencyProperty ProgressPercentProperty = DependencyProperty.Register(nameof(ProgressPercent), typeof(double), typeof(TopProgressView),
         new FrameworkPropertyMetadata(default(double), WireNotifyPropertyChanged));

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(nameof(Title), typeof(string), typeof(TopProgressView),
         new FrameworkPropertyMetadata(default(string), WireNotifyPropertyChanged));

    public string StatePrefixTitle
    {
        get => (string)GetValue(StatePrefixTitleProperty);
        set => SetValue(StatePrefixTitleProperty, value);
    }
    public static readonly DependencyProperty StatePrefixTitleProperty = DependencyProperty.Register(nameof(StatePrefixTitle), typeof(string), typeof(TopProgressView),
         new FrameworkPropertyMetadata(default(string), WireNotifyPropertyChanged));

    public bool OverhangShadow
    {
        get => (bool)GetValue(OverhangShadowProperty);
        set => SetValue(OverhangShadowProperty, value);
    }
    public static readonly DependencyProperty OverhangShadowProperty = DependencyProperty.Register(nameof(OverhangShadow), typeof(bool), typeof(TopProgressView),
         new FrameworkPropertyMetadata(true, WireNotifyPropertyChanged));

    public bool ShadowMargin
    {
        get => (bool)GetValue(ShadowMarginProperty);
        set => SetValue(ShadowMarginProperty, value);
    }
    public static readonly DependencyProperty ShadowMarginProperty = DependencyProperty.Register(nameof(ShadowMargin), typeof(bool), typeof(TopProgressView),
         new FrameworkPropertyMetadata(true, WireNotifyPropertyChanged));

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

            this.WhenAny(x => x.OverhangShadow)
                .Select(x => x ? Visibility.Visible : Visibility.Collapsed)
                .BindToStrict(this, x => x.OverhangShadowRect.Visibility)
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
                .Select(x => x == null ? Visibility.Visible : Visibility.Collapsed)
                .BindToStrict(this, x => x.PrefixSpacerRect.Visibility)
                .DisposeWith(dispose);
            this.WhenAny(x => x.StatePrefixTitle)
                .Select(x => x == null ? Visibility.Collapsed : Visibility.Visible)
                .BindToStrict(this, x => x.StatePrefixText.Visibility)
                .DisposeWith(dispose);
            this.WhenAny(x => x.StatePrefixTitle)
                .BindToStrict(this, x => x.StatePrefixText.Text)
                .DisposeWith(dispose);
        });
    }
}
