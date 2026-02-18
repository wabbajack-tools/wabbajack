using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.ReactiveUI;
using ReactiveUI;
using Wabbajack.App.Avalonia.Messages;
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
            this.BindCommand(ViewModel, vm => vm.CreateCommand,   v => v.CreateButton)
                .DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.SettingsCommand, v => v.SettingsButton)
                .DisposeWith(disposables);

            this.WhenAnyValue(x => x.ViewModel!.Version)
                .Subscribe(version => VersionText.Text = $"v{version}")
                .DisposeWith(disposables);

            this.WhenAnyValue(v => v.ViewModel!.ActiveScreen)
                .Subscribe(screen =>
                {
                    HomeButton.Classes.Set("active",    screen == ScreenType.Home);
                    BrowseButton.Classes.Set("active",  screen == ScreenType.ModListGallery);
                    CreateButton.Classes.Set("active",  screen == ScreenType.Compiler);
                    SettingsButton.Classes.Set("active", screen == ScreenType.Settings);
                })
                .DisposeWith(disposables);
        });
    }
}
