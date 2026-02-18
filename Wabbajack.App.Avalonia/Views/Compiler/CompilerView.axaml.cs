using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.ReactiveUI;
using FluentIcons.Common;
using ReactiveUI;
using Wabbajack.App.Avalonia.ViewModels.Compiler;
using Wabbajack.Common;
using Wabbajack.Paths;

namespace Wabbajack.App.Avalonia.Views.Compiler;

public partial class CompilerView : ReactiveUserControl<CompilerVM>
{
    public CompilerView()
    {
        InitializeComponent();

        this.WhenActivated(disposables =>
        {
            // ── Two-way bindings (Configuration form) ─────────────────
            this.Bind(ViewModel, vm => vm.SourcePath,         v => v.SourcePathBox.Text)
                .DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.Profile,            v => v.ProfileBox.Text)
                .DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.DownloadsPath,      v => v.DownloadsPathBox.Text)
                .DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.OutputPath,         v => v.OutputPathBox.Text)
                .DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.ModListName,        v => v.ModListNameBox.Text)
                .DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.ModListAuthor,      v => v.ModListAuthorBox.Text)
                .DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.ModListDescription, v => v.ModListDescriptionBox.Text)
                .DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.ModListVersion,     v => v.VersionBox.Text)
                .DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.ImagePath,          v => v.ImagePathBox.Text)
                .DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.IsNSFW,             v => v.NSFWCheckBox.IsChecked)
                .DisposeWith(disposables);

            // ── Commands ──────────────────────────────────────────────
            this.BindCommand(ViewModel, vm => vm.StartCommand,            v => v.StartButton)
                .DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.BackCommand,             v => v.BackToHomeButton)
                .DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.BackCommand,             v => v.BackFromProgressButton)
                .DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.CancelCommand,           v => v.CancelCompileButton)
                .DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.OpenOutputFolderCommand, v => v.OpenOutputFolderButton)
                .DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.OpenLogFolderCommand,    v => v.OpenLogFolderButton)
                .DisposeWith(disposables);

            // ── Browse buttons ────────────────────────────────────────
            NewModlistButton.Click += async (_, _) =>
            {
                var result = await TopLevel.GetTopLevel(this)!.StorageProvider
                    .OpenFilePickerAsync(new FilePickerOpenOptions
                    {
                        Title = "Select modlist.txt",
                        AllowMultiple = false,
                        FileTypeFilter = new[]
                        {
                            new FilePickerFileType("Modlist text file") { Patterns = new[] { "modlist.txt" } },
                            FilePickerFileTypes.All
                        }
                    });
                if (result.Count > 0)
                    await ViewModel!.InferFromModlistTxt((AbsolutePath)result[0].Path.LocalPath);
            };

            LoadSettingsButton.Click += async (_, _) =>
            {
                var result = await TopLevel.GetTopLevel(this)!.StorageProvider
                    .OpenFilePickerAsync(new FilePickerOpenOptions
                    {
                        Title = "Select compiler settings file",
                        AllowMultiple = false,
                        FileTypeFilter = new[]
                        {
                            new FilePickerFileType("Compiler settings") { Patterns = new[] { "*.wabbajack_compiler_settings" } },
                            FilePickerFileTypes.All
                        }
                    });
                if (result.Count > 0)
                    await ViewModel!.LoadFromSettingsFile((AbsolutePath)result[0].Path.LocalPath);
            };

            BrowseModlistTxtButton.Click += async (_, _) =>
            {
                var result = await TopLevel.GetTopLevel(this)!.StorageProvider
                    .OpenFilePickerAsync(new FilePickerOpenOptions
                    {
                        Title = "Select modlist.txt",
                        AllowMultiple = false,
                        FileTypeFilter = new[]
                        {
                            new FilePickerFileType("Modlist text file") { Patterns = new[] { "modlist.txt" } },
                            FilePickerFileTypes.All
                        }
                    });
                if (result.Count > 0)
                    await ViewModel!.InferFromModlistTxt((AbsolutePath)result[0].Path.LocalPath);
            };

            BrowseDownloadsButton.Click += async (_, _) =>
            {
                var result = await TopLevel.GetTopLevel(this)!.StorageProvider
                    .OpenFolderPickerAsync(new FolderPickerOpenOptions
                    {
                        Title = "Select downloads folder",
                        AllowMultiple = false
                    });
                if (result.Count > 0)
                    ViewModel!.DownloadsPath = result[0].Path.LocalPath;
            };

            BrowseOutputButton.Click += async (_, _) =>
            {
                var result = await TopLevel.GetTopLevel(this)!.StorageProvider
                    .SaveFilePickerAsync(new FilePickerSaveOptions
                    {
                        Title = "Choose output .wabbajack file",
                        DefaultExtension = ".wabbajack",
                        FileTypeChoices = new[]
                        {
                            new FilePickerFileType("Wabbajack archive") { Patterns = new[] { "*.wabbajack" } }
                        }
                    });
                if (result != null)
                    ViewModel!.OutputPath = result.Path.LocalPath;
            };

            BrowseImageButton.Click += async (_, _) =>
            {
                var result = await TopLevel.GetTopLevel(this)!.StorageProvider
                    .OpenFilePickerAsync(new FilePickerOpenOptions
                    {
                        Title = "Select cover image",
                        AllowMultiple = false,
                        FileTypeFilter = new[]
                        {
                            new FilePickerFileType("Images") { Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.webp" } },
                            FilePickerFileTypes.All
                        }
                    });
                if (result.Count > 0)
                    ViewModel!.ImagePath = result[0].Path.LocalPath;
            };

            // ── Recent items wiring ───────────────────────────────────
            this.WhenAnyValue(v => v.ViewModel!.RecentSettings)
                .Subscribe(list =>
                {
                    RecentPanel.IsVisible = list.Count > 0;
                    RecentItemsControl.ItemsSource = list;
                })
                .DisposeWith(disposables);

            // Wire each recent button click — handled via ItemsControl template
            // Since DataTemplate buttons can't easily BindCommand, use PointerPressed
            RecentItemsControl.AddHandler(
                Button.ClickEvent,
                (object? sender, RoutedEventArgs e) =>
                {
                    if (e.Source is Button btn && btn.DataContext is AbsolutePath path)
                        ViewModel?.LoadRecentCommand.Execute(path);
                });

            // ── Cover image preview ───────────────────────────────────
            this.WhenAnyValue(v => v.ViewModel!.ImagePath)
                .Subscribe(imgPath =>
                {
                    if (!string.IsNullOrWhiteSpace(imgPath))
                    {
                        try { CoverImage.Source = new Bitmap(imgPath); }
                        catch { CoverImage.Source = null; }
                    }
                    else
                    {
                        CoverImage.Source = null;
                    }
                })
                .DisposeWith(disposables);

            this.WhenAnyValue(v => v.ViewModel!.ModListName)
                .Subscribe(name => PreviewNameText.Text = name)
                .DisposeWith(disposables);
            this.WhenAnyValue(v => v.ViewModel!.ModListAuthor)
                .Subscribe(author => PreviewAuthorText.Text =
                    string.IsNullOrWhiteSpace(author) ? "" : $"by {author}")
                .DisposeWith(disposables);

            // ── Progress bar ──────────────────────────────────────────
            this.WhenAnyValue(v => v.ViewModel!.ProgressPercent)
                .Subscribe(p => CompileProgressBar.Value = p.Value)
                .DisposeWith(disposables);

            this.WhenAnyValue(v => v.ViewModel!.ProgressText)
                .Subscribe(t => ProgressStatusText.Text = t)
                .DisposeWith(disposables);

            this.WhenAnyValue(v => v.ViewModel!.ModListName)
                .Subscribe(name => ProgressTitleText.Text = name)
                .DisposeWith(disposables);

            // ── State → panel visibility ──────────────────────────────
            this.WhenAnyValue(v => v.ViewModel!.State)
                .Subscribe(state =>
                {
                    HomeGrid.IsVisible     = state == CompilerState.Home;
                    ConfigGrid.IsVisible   = state == CompilerState.Configuration;
                    ProgressGrid.IsVisible = state == CompilerState.Compiling
                                         || state == CompilerState.Completed
                                         || state == CompilerState.Errored;

                    CompileProgressBar.IsVisible    = state == CompilerState.Compiling;
                    CancelCompileButton.IsVisible   = state == CompilerState.Compiling;
                    OpenOutputFolderButton.IsVisible = state == CompilerState.Completed;
                    OpenLogFolderButton.IsVisible   = state == CompilerState.Errored;
                    BackFromProgressButton.IsVisible = state == CompilerState.Completed
                                                   || state == CompilerState.Errored;

                    ProgressIcon.Symbol = state switch
                    {
                        CompilerState.Compiling => Symbol.Toolbox,
                        CompilerState.Completed => Symbol.CheckmarkCircle,
                        CompilerState.Errored   => Symbol.ErrorCircle,
                        _                       => Symbol.Toolbox
                    };
                })
                .DisposeWith(disposables);
        });
    }
}
