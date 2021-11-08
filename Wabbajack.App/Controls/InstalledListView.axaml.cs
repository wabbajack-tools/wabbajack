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
            this.OneWayBind(ViewModel, vm => vm.Image, view => view.ListImage.Source)
                .DisposeWith(disposables);
            
            this.OneWayBind(ViewModel, vm => vm.Name, view => view.Title.Text)
                .DisposeWith(disposables);

            this.OneWayBind(ViewModel, vm => vm.Author, view => view.Author.Text)
                .DisposeWith(disposables);

            this.OneWayBind(ViewModel, vm => vm.Version, view => view.Version.Text)
                .DisposeWith(disposables);

            this.OneWayBind(ViewModel, vm => vm.InstallPath, view => view.InstallationPath.Text,
                    p => p.ToString())
                .DisposeWith(disposables);

            this.BindCommand(ViewModel, vm => vm.Play, view => view.ListButton)
                .DisposeWith(disposables);
        });
    }
}