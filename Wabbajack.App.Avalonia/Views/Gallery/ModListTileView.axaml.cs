using System.Reactive.Disposables;
using Avalonia.ReactiveUI;
using ReactiveUI;
using Wabbajack.App.Avalonia.ViewModels.Gallery;

namespace Wabbajack.App.Avalonia.Views.Gallery;

public partial class ModListTileView : ReactiveUserControl<BaseModListMetadataVM>
{
    public ModListTileView()
    {
        InitializeComponent();

        this.WhenActivated(disposables =>
        {
            // Wire the background image
            this.OneWayBind(ViewModel, vm => vm.Image, v => v.ModlistImage.Source)
                .DisposeWith(disposables);

            // Install command on the button
            this.BindCommand(ViewModel, vm => vm.InstallCommand, v => v.ModlistButton)
                .DisposeWith(disposables);
        });
    }
}
