using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.ReactiveUI;
using ReactiveUI;
using Wabbajack.App.Avalonia.ViewModels.Settings;

namespace Wabbajack.App.Avalonia.Views.Settings;

public partial class SettingsView : ReactiveUserControl<SettingsVM>
{
    public SettingsView()
    {
        InitializeComponent();

        this.WhenActivated(disposables =>
        {
            this.BindCommand(ViewModel, vm => vm.OpenGitHubCommand, v => v.OpenGitHubButton)
                .DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.OpenLogFolderCommand, v => v.OpenLogFolderButton)
                .DisposeWith(disposables);

            this.WhenAnyValue(v => v.ViewModel!.Version)
                .Subscribe(ver => VersionText.Text = $"v{ver}")
                .DisposeWith(disposables);

            this.WhenAnyValue(v => v.ViewModel!.ResourceSettings)
                .Subscribe(list => ResourceSettingsList.ItemsSource = list)
                .DisposeWith(disposables);
        });
    }
}
