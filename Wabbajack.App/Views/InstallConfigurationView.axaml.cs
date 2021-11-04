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
            ViewModel.WhenAnyValue(vm => vm.ModListPath)
                .Subscribe(path => ModListFile.Load(path))
                .DisposeWith(disposables);
            
            ViewModel.WhenAnyValue(vm => vm.ModListPath)
                .Subscribe(path => ModListFile.Load(path))
                .DisposeWith(disposables);
            
            ViewModel.WhenAnyValue(vm => vm.Download)
                .Subscribe(path => DownloadPath.Load(path))
                .DisposeWith(disposables);
            
            ViewModel.WhenAnyValue(vm => vm.Install)
                .Subscribe(path => InstallPath.Load(path))
                .DisposeWith(disposables);

            this.WhenAnyValue(view => view.ModListFile.SelectedPath)
                .BindTo(ViewModel, vm => vm.ModListPath)
                .DisposeWith(disposables);

            this.WhenAnyValue(view => view.DownloadPath.SelectedPath)
                .BindTo(ViewModel, vm => vm.Download)
                .DisposeWith(disposables);

            this.WhenAnyValue(view => view.InstallPath.SelectedPath)
                .BindTo(ViewModel, vm => vm.Install)
                .DisposeWith(disposables);
        });
    }

    public Type ViewModelType => typeof(InstallConfigurationViewModel);
}