using System.Reactive.Disposables;
using System.Windows;
using ReactiveUI;
using System.Reactive.Linq;
using Wabbajack.RateLimiter;

namespace Wabbajack;

/// <summary>
/// Interaction logic for CpuView.xaml
/// </summary>
public partial class CpuView : UserControlRx<ICpuStatusVM>
{
    public Percent ProgressPercent
    {
        get => (Percent)GetValue(ProgressPercentProperty);
        set => SetValue(ProgressPercentProperty, value);
    }
    public static readonly DependencyProperty ProgressPercentProperty = DependencyProperty.Register(nameof(ProgressPercent), typeof(Percent), typeof(CpuView),
         new FrameworkPropertyMetadata(default(Percent), WireNotifyPropertyChanged));

    public CpuView()
    {
        InitializeComponent();
        this.WhenActivated(disposable =>
        {

            this.WhenAny(x => x.ViewModel.StatusList)
                .BindToStrict(this, x => x.CpuListControl.ItemsSource)
                .DisposeWith(disposable);

            // Progress
            this.WhenAny(x => x.ProgressPercent)
                .Select(p => p.Value)
                .BindToStrict(this, x => x.HeatedBorderRect.Opacity)
                .DisposeWith(disposable);
        });
    }
}
