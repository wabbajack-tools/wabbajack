using System;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.ReactiveUI;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI;
using Wabbajack.Paths;

namespace Wabbajack.App.Controls;

public partial class FileSelectionBox : ReactiveUserControl<FileSelectionBoxViewModel>
{
    public static readonly DirectProperty<FileSelectionBox, AbsolutePath> SelectedPathProperty =
        AvaloniaProperty.RegisterDirect<FileSelectionBox, AbsolutePath>(nameof(SelectedPath), o => o.SelectedPath);

    public static readonly StyledProperty<string> AllowedExtensionsProperty =
        AvaloniaProperty.Register<FileSelectionBox, string>(nameof(AllowedExtensions));

    public static readonly StyledProperty<bool> SelectFolderProperty =
        AvaloniaProperty.Register<FileSelectionBox, bool>(nameof(SelectFolder));

    private AbsolutePath _selectedPath;

    public FileSelectionBox()
    {
        DataContext = App.Services.GetService<FileSelectionBoxViewModel>()!;
        InitializeComponent();

        this.WhenActivated(disposables =>
        {
            this.Bind(ViewModel, vm => vm.Path, view => view.SelectedPath)
                .DisposeWith(disposables);
            this.WhenAnyValue(view => view.SelectFolder)
                .BindTo(ViewModel, vm => vm.SelectFolder)
                .DisposeWith(disposables);
            this.WhenAnyValue(view => view.AllowedExtensions)
                .Where(exts => !string.IsNullOrWhiteSpace(exts))
                .Select(exts =>
                    exts.Split("|", StringSplitOptions.RemoveEmptyEntries).Select(s => new Extension(s)).ToArray())
                .BindTo(ViewModel, vm => vm.Extensions)
                .DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.Path,
                    view => view.TextBox.Text)
                .DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.BrowseCommand,
                    view => view.SelectButton)
                .DisposeWith(disposables);
        });
    }

    public AbsolutePath SelectedPath
    {
        get => _selectedPath;
        set => SetAndRaise(SelectedPathProperty, ref _selectedPath, value);
    }

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
}