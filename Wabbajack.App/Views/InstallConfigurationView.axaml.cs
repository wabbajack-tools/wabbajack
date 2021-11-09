using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.ReactiveUI;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI;
using Wabbajack.App.Interfaces;
using Wabbajack.App.ViewModels;
using Wabbajack.Common;
using Wabbajack.Paths;

namespace Wabbajack.App.Views;

public partial class InstallConfigurationView : ScreenBase<InstallConfigurationViewModel>, IScreenView
{
    public InstallConfigurationView() : base("Install Configuration")
    {
        InitializeComponent();
        DataContext = App.Services.GetService<InstallConfigurationViewModel>()!;

        this.WhenActivated(disposables =>
        {
            ModListFile.SelectButton.Command = ReactiveCommand.CreateFromTask(SelectWabbajackFile)
                .DisposeWith(disposables);
            this.OneWayBind(ViewModel, vm => vm.ModListPath, view => view.ModListFile.TextBox.Text)
                .DisposeWith(disposables);

            InstallPath.SelectButton.Command = ReactiveCommand.CreateFromTask(() =>
                    SelectFolder("Select Install Location", p => ViewModel!.Install = p))
                .DisposeWith(disposables);
            this.OneWayBind(ViewModel, vm => vm.Install, view => view.InstallPath.TextBox.Text)
                .DisposeWith(disposables);
            
            DownloadPath.SelectButton.Command = ReactiveCommand.CreateFromTask(() =>
                    SelectFolder("Select Download Location", p => ViewModel!.Download = p))
                .DisposeWith(disposables);
            this.OneWayBind(ViewModel, vm => vm.Download, view => view.DownloadPath.TextBox.Text)
                .DisposeWith(disposables);
            
            this.OneWayBind(ViewModel, vm => vm.ModListImage, view => view.ModListImage.Source)
                .DisposeWith(disposables);

            this.BindCommand(ViewModel, vm => vm.BeginCommand, view => view.BeginInstall.Button)
                .DisposeWith(disposables);
        });
    }

    private async Task SelectWabbajackFile()
    {
        var fod = new OpenFileDialog()
        {
            Filters = new List<FileDialogFilter> {new()
                {
                    Name = "Wabbajack",
                    Extensions = new List<string> {Ext.Wabbajack.ToString()}
                }
            }
        };
        var result = await fod.ShowAsync(App.MainWindow);
        if (result == null) return;
        
        Dispatcher.UIThread.Post(() =>
        {
            ViewModel!.ModListPath = result.First().ToAbsolutePath();
        });
    }

    public async Task SelectFolder(string title, Action<AbsolutePath> toCall)
    {
        var fod = new OpenFolderDialog
        {
            Title = title
        };
        var result = await fod.ShowAsync(App.MainWindow);
        if (result == null) return;
        
        Dispatcher.UIThread.Post(() =>
        {
            toCall(result.ToAbsolutePath());
        });
    }

    public Type ViewModelType => typeof(InstallConfigurationViewModel);
}