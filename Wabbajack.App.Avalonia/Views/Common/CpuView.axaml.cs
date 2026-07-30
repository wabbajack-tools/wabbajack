using Avalonia.ReactiveUI;
using System.Reactive.Disposables;
using ReactiveUI;
using System.Reactive.Linq;
using Wabbajack.RateLimiter;

namespace Wabbajack;

/// <summary>
/// Interaction logic for CpuView.axaml
/// </summary>
public partial class CpuView : ReactiveUserControl<ICpuStatusVM>
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
