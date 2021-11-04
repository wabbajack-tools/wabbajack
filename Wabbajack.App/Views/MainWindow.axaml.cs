using System;
using System.Reactive.Disposables;
using Avalonia;
using Avalonia.Controls;
using Avalonia.ReactiveUI;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI;
using Wabbajack.App.ViewModels;

namespace Wabbajack.App.Views;

public partial class MainWindow : ReactiveWindow<MainWindowViewModel>
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = App.Services.GetService<MainWindowViewModel>()!;

        this.WhenActivated(disposables =>
        {
            CloseButton.Command = ReactiveCommand.Create(() => Environment.Exit(0))
                .DisposeWith(disposables);
            MinimizeButton.Command = ReactiveCommand.Create(() => WindowState = WindowState.Minimized)
                .DisposeWith(disposables);

            this.BindCommand(ViewModel, vm => vm.BackButton, view => view.BackButton)
                .DisposeWith(disposables);

            this.BindCommand(ViewModel, vm => vm.SettingsButton, view => view.SettingsButton)
                .DisposeWith(disposables);

            this.BindCommand(ViewModel, vm => vm.LogViewButton, view => view.LogButton)
                .DisposeWith(disposables);

            this.OneWayBind(ViewModel, vm => vm.CurrentScreen, view => view.Contents.Content)
                .DisposeWith(disposables);

            this.OneWayBind(ViewModel, vm => vm.ResourceStatus, view => view.ResourceStatus.Text)
                .DisposeWith(disposables);
            
            this.OneWayBind(ViewModel, vm => vm.TitleText, view => view.TitleText.Text)
                .DisposeWith(disposables);
        });


        Width = 1125;
        Height = 900;

#if DEBUG
        this.AttachDevTools();
#endif
    }
}