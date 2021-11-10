using System;
using System.Reactive.Disposables;
using Avalonia.Interactivity;
using ReactiveUI;
using Wabbajack.App.Utilities;
using Wabbajack.App.Views;
using Wabbajack.Installer;

namespace Wabbajack.App.Screens;

public partial class LauncherView : ScreenBase<LauncherViewModel>
{
    public LauncherView() : base("Launch Modlist")
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

    private void ShowWebsite(object? sender, RoutedEventArgs e)
    {
        OSUtil.OpenWebsite(ViewModel!.Setting!.StrippedModListData?.Website!);
    }
    
    private void ShowReadme(object? sender, RoutedEventArgs e)
    {
        OSUtil.OpenWebsite(new Uri(ViewModel!.Setting!.StrippedModListData?.Readme!));
    }

    private void ShowLocalFiles(object? sender, RoutedEventArgs e)
    {
        OSUtil.OpenFolder(ViewModel!.Setting!.Install);
    }
}