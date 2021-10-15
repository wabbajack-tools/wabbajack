using Avalonia.Controls.Mixins;
using ReactiveUI;
using Wabbajack.App.ViewModels;
using Wabbajack.App.Views;

namespace Wabbajack.App.Screens;

public partial class CompilationView : ScreenBase<CompilationViewModel>
{
    public CompilationView()
    {
        InitializeComponent();

        this.WhenActivated(disposables =>
        {
            this.OneWayBind(ViewModel, vm => vm.StatusText, view => view.StatusText.Text)
                .DisposeWith(disposables);

            this.OneWayBind(ViewModel, vm => vm.StepsProgress, view => view.StepsProgress.Value, p => p.Value * 1000)
                .DisposeWith(disposables);

            this.OneWayBind(ViewModel, vm => vm.StepProgress, view => view.StepProgress.Value, p => p.Value * 10000)
                .DisposeWith(disposables);
        });
    }
}
