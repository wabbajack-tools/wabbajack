using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.ReactiveUI;
using ReactiveUI;
using Wabbajack.App.Avalonia.ViewModels;

namespace Wabbajack.App.Avalonia.Views;

public partial class NavigationView : ReactiveUserControl<NavigationVM>
{
    public NavigationView()
    {
        InitializeComponent();

        this.WhenActivated(disposables =>
        {
            this.BindCommand(ViewModel, vm => vm.HomeCommand,   v => v.HomeButton)
                .DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.BrowseCommand,  v => v.BrowseButton)
                .DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.CreateCommand,  v => v.CreateButton)
                .DisposeWith(disposables);

            this.WhenAnyValue(x => x.ViewModel!.Version)
                .Subscribe(version => VersionText.Text = $"v{version}")
                .DisposeWith(disposables);
        });
    }
}
