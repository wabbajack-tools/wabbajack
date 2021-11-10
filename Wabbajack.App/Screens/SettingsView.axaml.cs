using System.Reactive.Disposables;
using Avalonia.Interactivity;
using ReactiveUI;
using Wabbajack.App.Views;
using Wabbajack.Common;

namespace Wabbajack.App.Screens;

public partial class SettingsView : ScreenBase<SettingsViewModel>
{
    public SettingsView() : base("Settings")
    {
        InitializeComponent();
        this.WhenActivated(disposables =>
        {
            this.BindCommand(ViewModel, vm => vm.NexusLogin, view => view.NexusLogIn)
                .DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.NexusLogout, view => view.NexusLogOut)
                .DisposeWith(disposables);
            
            this.BindCommand(ViewModel, vm => vm.LoversLabLogin, view => view.LoversLabLogIn)
                .DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.LoversLabLogout, view => view.LoversLabLogOut)
                .DisposeWith(disposables);

            
            this.BindCommand(ViewModel, vm => vm.VectorPlexusLogin, view => view.VectorPlexusLogIn)
                .DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.VectorPlexusLogout, view => view.VectorPlexusLogOut)
                .DisposeWith(disposables);

            
            this.OneWayBind(ViewModel, vm => vm.Resources, view => view.ResourcesList.Items)
                .DisposeWith(disposables);

        });
    }

    private void SaveSettingsAndRestart(object? sender, RoutedEventArgs e)
    {
        ViewModel!.SaveResourceSettingsAndRestart().FireAndForget();
    }
}