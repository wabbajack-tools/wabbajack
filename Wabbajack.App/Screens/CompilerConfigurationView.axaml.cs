using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using ReactiveUI;
using Wabbajack.App.Controls;
using Wabbajack.App.Extensions;
using Wabbajack.App.Views;
using Wabbajack.Common;
using Wabbajack.Paths;

namespace Wabbajack.App.Screens;

public partial class CompilerConfigurationView : ScreenBase<CompilerConfigurationViewModel>
{
    public CompilerConfigurationView() : base("Compiler Configuration")
    {
        InitializeComponent();
        AddAlwaysEnabled.Command = ReactiveCommand.Create(() => AddAlwaysEnabled_Command().FireAndForget());
        AddOtherProfile.Command =  ReactiveCommand.Create(() => AddOtherProfile_Command().FireAndForget());

        this.WhenActivated(disposables =>
        {
            this.Bind(ViewModel, vm => vm.Title, view => view.Title.Text)
                .DisposeWith(disposables);

            SettingsFile.BindFileSelectionBox(ViewModel, vm => vm.SettingsFile)
                .DisposeWith(disposables);

            Source.BindFileSelectionBox(ViewModel, vm => vm.Source)
                .DisposeWith(disposables);
            
            DownloadsFolder.BindFileSelectionBox(ViewModel, vm => vm.Downloads)
                .DisposeWith(disposables);

            OutputFolder.BindFileSelectionBox(ViewModel, vm => vm.OutputFolder)
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
            
            this.OneWayBind(ViewModel, vm => vm.OtherProfiles, view => view.OtherProfilesList.Items,
                    d => d!.Select(itm => new RemovableItemViewModel
                    {
                        Text = itm.ToString(),
                        DeleteCommand = ReactiveCommand.Create(() => { ViewModel?.RemoveOtherProfile(itm); })
                    }))
                .DisposeWith(disposables);
        });
    }
    
    private async Task AddOtherProfile_Command()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select a profile folder"
        };
        var result = await dialog.ShowAsync(App.MainWindow);
        if (!string.IsNullOrWhiteSpace(result))
            ViewModel!.AddOtherProfile(result.ToAbsolutePath());
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