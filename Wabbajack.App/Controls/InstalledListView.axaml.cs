using Avalonia.Controls.Mixins;
using Avalonia.ReactiveUI;
using ReactiveUI;

namespace Wabbajack.App.Controls;

public partial class InstalledListView : ReactiveUserControl<InstalledListViewModel>, IActivatableView
{
    public InstalledListView()
    {
        InitializeComponent();

        this.WhenActivated(disposables =>
        {
            this.OneWayBind(ViewModel, vm => vm.Name, view => view.Title.Text)
                .DisposeWith(disposables);

            this.OneWayBind(ViewModel, vm => vm.InstallPath, view => view.Title.Text,
                    p => p.ToString())
                .DisposeWith(disposables);

            this.BindCommand(ViewModel, vm => vm.Play, view => view.PlayButton)
                .DisposeWith(disposables);
        });
    }
}