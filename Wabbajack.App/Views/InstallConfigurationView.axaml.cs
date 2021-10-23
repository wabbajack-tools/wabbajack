using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.ReactiveUI;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI;
using Wabbajack.App.Interfaces;
using Wabbajack.App.ViewModels;

namespace Wabbajack.App.Views;

public partial class InstallConfigurationView : ReactiveUserControl<InstallConfigurationViewModel>, IScreenView
{
    public InstallConfigurationView()
    {
        InitializeComponent();
        DataContext = App.Services.GetService<InstallConfigurationViewModel>()!;

        this.WhenActivated(disposables =>
        {
            this.Bind(ViewModel, x => x.ModListPath,
                    view => view.ModListFile.SelectedPath)
                .DisposeWith(disposables);
            this.Bind(ViewModel, x => x.Download,
                    view => view.DownloadPath.SelectedPath)
                .DisposeWith(disposables);
            this.Bind(ViewModel, x => x.Install,
                    view => view.InstallPath.SelectedPath)
                .DisposeWith(disposables);

            ViewModel.WhenAnyValue(x => x.BeginCommand)
                .Where(x => x != default)
                .BindTo(BeginInstall, x => x.Button.Command)
                .DisposeWith(disposables);

            ViewModel.WhenAnyValue(x => x.ModList)
                .Where(x => x != default)
                .Select(x => x!.Name)
                .BindTo(ModListName, x => x.Text)
                .DisposeWith(disposables);

            ViewModel.WhenAnyValue(x => x.ModListImage)
                .Where(x => x != default)
                .BindTo(ModListImage, x => x.Source)
                .DisposeWith(disposables);
        });
    }

    public Type ViewModelType => typeof(InstallConfigurationViewModel);
}