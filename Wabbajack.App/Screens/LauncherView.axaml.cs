using System.Reactive.Disposables;
using ReactiveUI;
using Wabbajack.App.Views;

namespace Wabbajack.App.Screens;

public partial class LauncherView : ScreenBase<LauncherViewModel>
{
    public LauncherView()
    {
        InitializeComponent();
        this.WhenActivated(disposables =>
        {
            this.OneWayBind(ViewModel, vm => vm.Image, view => view.ModListImage.Source)
                .DisposeWith(disposables);

            this.OneWayBind(ViewModel, vm => vm.Title, view => view.ModList.Text)
                .DisposeWith(disposables);

            this.OneWayBind(ViewModel, vm => vm.InstallFolder, view => view.InstallPath.Text,
                    v => v.ToString())
                .DisposeWith(disposables);

            this.BindCommand(ViewModel, vm => vm.PlayButton, view => view.PlayGame.Button)
                .DisposeWith(disposables);
        });
    }
}