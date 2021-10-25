using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using ReactiveUI;
using Wabbajack.App.Controls;
using Wabbajack.App.Views;
using Wabbajack.Common;
using Wabbajack.Paths;

namespace Wabbajack.App.Screens;

public partial class CompilerConfigurationView : ScreenBase<CompilerConfigurationViewModel>
{
    public CompilerConfigurationView()
    {
        InitializeComponent();
        AddAlwaysEnabled.Command = ReactiveCommand.Create(() => AddAlwaysEnabled_Command().FireAndForget());

        this.WhenActivated(disposables =>
        {
            this.Bind(ViewModel, vm => vm.SettingsFile, view => view.SettingsFile.SelectedPath)
                .DisposeWith(disposables);

            this.Bind(ViewModel, vm => vm.Title, view => view.Title.Text)
                .DisposeWith(disposables);

            this.Bind(ViewModel, vm => vm.SettingsFile, view => view.SettingsFile.SelectedPath)
                .DisposeWith(disposables);

            this.Bind(ViewModel, vm => vm.Source, view => view.Source.SelectedPath)
                .DisposeWith(disposables);

            this.Bind(ViewModel, vm => vm.Downloads, view => view.DownloadsFolder.SelectedPath)
                .DisposeWith(disposables);

            this.Bind(ViewModel, vm => vm.OutputFolder, view => view.OutputFolder.SelectedPath)
                .DisposeWith(disposables);

            this.OneWayBind(ViewModel, vm => vm.AllGames, view => view.BaseGame.Items)
                .DisposeWith(disposables);

            this.Bind(ViewModel, vm => vm.BaseGame, view => view.BaseGame.SelectedItem)
                .DisposeWith(disposables);

            this.BindCommand(ViewModel, vm => vm.StartCompilation, view => view.StartCompilation)
                .DisposeWith(disposables);

            this.OneWayBind(ViewModel, vm => vm.AlwaysEnabled, view => view.AlwaysEnabledList.Items,
                    d => d!.Select(itm => new RemovableItemViewModel
                    {
                        Text = itm.ToString(),
                        DeleteCommand = ReactiveCommand.Create(() => { ViewModel?.RemoveAlwaysExcluded(itm); })
                    }))
                .DisposeWith(disposables);
        });
    }

    private async Task AddAlwaysEnabled_Command()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select a folder"
        };
        var result = await dialog.ShowAsync(App.MainWindow);
        if (!string.IsNullOrWhiteSpace(result))
            ViewModel!.AddAlwaysExcluded(result.ToAbsolutePath());
    }

    private void InferSettings_OnClick(object? sender, RoutedEventArgs e)
    {
        Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var dialog = new OpenFileDialog
            {
                Title = "Select a modlist.txt file",
                Filters = new List<FileDialogFilter>
                    {new() {Extensions = new List<string> {"txt"}, Name = "modlist.txt"}},
                AllowMultiple = false
            };
            var result = await dialog.ShowAsync(App.MainWindow);
            if (result is {Length: > 0})
                await ViewModel!.InferSettingsFromModlistTxt(result.First().ToAbsolutePath());
        });
    }
}