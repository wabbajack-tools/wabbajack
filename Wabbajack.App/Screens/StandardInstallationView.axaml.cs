using System.Reactive.Disposables;
using ReactiveUI;
using Wabbajack.App.ViewModels;

namespace Wabbajack.App.Views;

public partial class StandardInstallationView : ScreenBase<StandardInstallationViewModel>
{
    public StandardInstallationView()
    {
        InitializeComponent();

        this.WhenActivated(disposables =>
        {
            this.Bind(ViewModel, vm => vm.Slide.Image, view => view.SlideImage.Source)
                .DisposeWith(disposables);

            this.BindCommand(ViewModel, vm => vm.NextCommand, view => view.NextSlide)
                .DisposeWith(disposables);

            this.BindCommand(ViewModel, vm => vm.PrevCommand, view => view.PrevSlide)
                .DisposeWith(disposables);

            this.BindCommand(ViewModel, vm => vm.PauseCommand, view => view.PauseSlides)
                .DisposeWith(disposables);

            this.BindCommand(ViewModel, vm => vm.PlayCommand, view => view.PlaySlides)
                .DisposeWith(disposables);

            this.OneWayBind(ViewModel, vm => vm.StatusText, view => view.StatusText.Text)
                .DisposeWith(disposables);

            this.OneWayBind(ViewModel, vm => vm.StepsProgress, view => view.StepsProgress.Value, p => p.Value * 1000)
                .DisposeWith(disposables);

            this.OneWayBind(ViewModel, vm => vm.StepProgress, view => view.StepProgress.Value, p => p.Value * 10000)
                .DisposeWith(disposables);
        });
    }
}