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
    public CpuView()
    {
        InitializeComponent();
        this.WhenActivated(disposable =>
        {
            this.WhenAny(x => x.ViewModel.StatusList)
                .BindToStrict(this, x => x.CpuListControl.ItemsSource)
                .DisposeWith(disposable);
        });
    }
}
