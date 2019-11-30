using System.Reactive.Linq;
using System.Windows;
using System.Windows.Controls;
using ReactiveUI;

namespace Wabbajack
{
    /// <summary>
    /// Interaction logic for TopProgressView.xaml
    /// </summary>
    public partial class TopProgressView : UserControlRx
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
             new FrameworkPropertyMetadata(default(string)));

        public string StatePrefixTitle
        {
            get => (string)GetValue(StatePrefixTitleProperty);
            set => SetValue(StatePrefixTitleProperty, value);
        }
        public static readonly DependencyProperty StatePrefixTitleProperty = DependencyProperty.Register(nameof(StatePrefixTitle), typeof(string), typeof(TopProgressView),
             new FrameworkPropertyMetadata(default(string)));

        public bool OverhangShadow
        {
            get => (bool)GetValue(OverhangShadowProperty);
            set => SetValue(OverhangShadowProperty, value);
        }
        public static readonly DependencyProperty OverhangShadowProperty = DependencyProperty.Register(nameof(OverhangShadow), typeof(bool), typeof(TopProgressView),
             new FrameworkPropertyMetadata(true));

        public bool ShadowMargin
        {
            get => (bool)GetValue(ShadowMarginProperty);
            set => SetValue(ShadowMarginProperty, value);
        }
        public static readonly DependencyProperty ShadowMarginProperty = DependencyProperty.Register(nameof(ShadowMargin), typeof(bool), typeof(TopProgressView),
             new FrameworkPropertyMetadata(true));

        private readonly ObservableAsPropertyHelper<double> _ProgressOpacityPercent;
        public double ProgressOpacityPercent => _ProgressOpacityPercent.Value;

        public TopProgressView()
        {
            InitializeComponent();
            _ProgressOpacityPercent = this.WhenAny(x => x.ProgressPercent)
                .Select(x =>
                {
                    return 0.3 + x * 0.7;
                })
                .ToProperty(this, nameof(ProgressOpacityPercent));
        }
    }
}
