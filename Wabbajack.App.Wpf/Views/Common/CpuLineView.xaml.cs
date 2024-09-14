using System.Reactive.Disposables;
using System.Reactive.Linq;
using ReactiveUI;

namespace Wabbajack;

/// <summary>
/// Interaction logic for CpuLineView.xaml
/// </summary>
public partial class CpuLineView : ReactiveUserControl<CPUDisplayVM>
{
    public CpuLineView()
    {
        InitializeComponent();
        this.WhenActivated(dispose =>
        {
            this.WhenAny(x => x.ViewModel.ProgressPercent)
                .Select(x => x.Value)
                .BindToStrict(this, x => x.BackgroundProgressBar.Value)
                .DisposeWith(dispose);
            this.WhenAny(x => x.ViewModel.ProgressPercent)
                .Select(x => x.Value)
                .BindToStrict(this, x => x.BackgroundProgressBar.Opacity)
                .DisposeWith(dispose);
            this.WhenAny(x => x.ViewModel.ProgressPercent)
                .Select(x => x.Value)
                .BindToStrict(this, x => x.ThinProgressBar.Value)
                .DisposeWith(dispose);

            this.WhenAny(x => x.ViewModel.Msg)
                .BindToStrict(this, x => x.Text.Text)
                .DisposeWith(dispose);
            this.WhenAny(x => x.ViewModel.Msg)
                .BindToStrict(this, x => x.Text.ToolTip)
                .DisposeWith(dispose);
        });
    }
}
