using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.ReactiveUI;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Wabbajack.Paths;

namespace Wabbajack.App.Controls;

public partial class FileSelectionBox : UserControl
{
    public static readonly DirectProperty<FileSelectionBox, AbsolutePath> SelectedPathProperty =
        AvaloniaProperty.RegisterDirect<FileSelectionBox, AbsolutePath>(nameof(SelectedPath), o => o.SelectedPath);

    public static readonly StyledProperty<string> AllowedExtensionsProperty =
        AvaloniaProperty.Register<FileSelectionBox, string>(nameof(AllowedExtensions));

    public static readonly StyledProperty<bool> SelectFolderProperty =
        AvaloniaProperty.Register<FileSelectionBox, bool>(nameof(SelectFolder));


    public FileSelectionBox()
    {
        InitializeComponent();
        SelectButton.Command = ReactiveCommand.CreateFromTask(ShowDialog);
    }

    private async Task ShowDialog()
    {
        if (SelectFolder)
        {
            var dialog = new OpenFolderDialog
            {
                Title = "Select a folder"
            };
            var result = await dialog.ShowAsync(App.MainWindow);
            if (result != null)
                Load(result.ToAbsolutePath());
        }
        else
        {
            var extensions = AllowedExtensions.Split(",").Select(e => e.ToString()[1..]).ToList();
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
                Load(results!.First().ToAbsolutePath());
        }
    }

    [Reactive]
    public AbsolutePath SelectedPath { get; private set; }

    public string AllowedExtensions
    {
        get => GetValue(AllowedExtensionsProperty);
        set => SetValue(AllowedExtensionsProperty, value);
    }

    public bool SelectFolder
    {
        get => GetValue(SelectFolderProperty);
        set => SetValue(SelectFolderProperty, value);
    }

    public void Load(AbsolutePath path)
    {
        TextBox.Text = path.ToString();
        SelectedPath = path;
    }
}