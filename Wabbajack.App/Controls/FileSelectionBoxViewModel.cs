using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Threading.Tasks;
using Avalonia.Controls;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Wabbajack.App.ViewModels;
using Wabbajack.Paths;

namespace Wabbajack.App.Controls;

public class FileSelectionBoxViewModel : ViewModelBase
{
    public FileSelectionBoxViewModel()
    {
        Activator = new ViewModelActivator();
        this.WhenActivated(disposables =>
        {
            BrowseCommand = ReactiveCommand.Create(async () =>
            {
                if (SelectFolder)
                {
                    var dialog = new OpenFolderDialog
                    {
                        Title = "Select a folder"
                    };
                    var result = await dialog.ShowAsync(App.MainWindow);
                    if (result != null)
                        Path = result.ToAbsolutePath();
                }
                else
                {
                    var extensions = Extensions.Select(e => e.ToString()[1..]).ToList();
                    var dialog = new OpenFileDialog
                    {
                        AllowMultiple = false,
                        Title = "Select a file",
                        Filters = new List<FileDialogFilter>
                        {
                            new FileDialogFilter {Extensions = extensions, Name = "*"}
                        }
                    };
                    var results = await dialog.ShowAsync(App.MainWindow);
                    if (results != null)
                        Path = results!.First().ToAbsolutePath();
                }
            }).DisposeWith(disposables);
        });
    }

    [Reactive] public AbsolutePath Path { get; set; }

    [Reactive] public Extension[] Extensions { get; set; } = Array.Empty<Extension>();

    [Reactive] public bool SelectFolder { get; set; }

    [Reactive] public ReactiveCommand<Unit, Task> BrowseCommand { get; set; } = null!;
}