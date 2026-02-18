using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Avalonia.ReactiveUI;
using FluentIcons.Common;
using ReactiveUI;
using Wabbajack.App.Avalonia.ViewModels.Installer;

namespace Wabbajack.App.Avalonia.Views.Installer;

public partial class InstallationView : ReactiveUserControl<InstallationVM>
{
    public InstallationView()
    {
        InitializeComponent();

        this.WhenActivated(disposables =>
        {
            // ── Path boxes (two-way) ──────────────────────────────────────
            this.Bind(ViewModel, vm => vm.Installer.InstallPath, v => v.InstallPathBox.Text)
                .DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.Installer.DownloadPath, v => v.DownloadPathBox.Text)
                .DisposeWith(disposables);

            // ── Browse buttons ────────────────────────────────────────────
            BrowseInstallButton.Click += async (_, _) =>
            {
                var result = await TopLevel.GetTopLevel(this)!.StorageProvider
                    .OpenFolderPickerAsync(new FolderPickerOpenOptions
                    {
                        Title = "Select installation folder",
                        AllowMultiple = false
                    });
                if (result.Count > 0)
                    ViewModel!.Installer.InstallPath = result[0].Path.LocalPath;
            };

            BrowseDownloadButton.Click += async (_, _) =>
            {
                var result = await TopLevel.GetTopLevel(this)!.StorageProvider
                    .OpenFolderPickerAsync(new FolderPickerOpenOptions
                    {
                        Title = "Select downloads folder",
                        AllowMultiple = false
                    });
                if (result.Count > 0)
                    ViewModel!.Installer.DownloadPath = result[0].Path.LocalPath;
            };

            // ── Commands ──────────────────────────────────────────────────
            this.BindCommand(ViewModel, vm => vm.InstallCommand, v => v.InstallButton)
                .DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.CancelCommand, v => v.CancelButton)
                .DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.EditInstallDetailsCommand, v => v.EditInstallDetailsButton)
                .DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.InstallCommand, v => v.RetryButton)
                .DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.OpenReadmeCommand, v => v.DocumentationButton)
                .DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.OpenWebsiteCommand, v => v.WebsiteButton)
                .DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.OpenLogFolderCommand, v => v.OpenLogFolderButton)
                .DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.BackToGalleryCommand, v => v.BackToGalleryButton)
                .DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.OpenReadmeCommand, v => v.ReadmeButton)
                .DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.CreateShortcutCommand, v => v.CreateShortcutButton)
                .DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.OpenInstallFolderCommand, v => v.OpenFolderButton)
                .DisposeWith(disposables);

            // ── Modlist image (shared across all states) ──────────────────
            this.WhenAnyValue(v => v.ViewModel!.ModListImage)
                .Subscribe(img =>
                {
                    SetupModListImage.Source = img;
                    InstallingModListImage.Source = img;
                    CompletedModListImage.Source = img;
                })
                .DisposeWith(disposables);

            // ── Modlist title / author ─────────────────────────────────────
            this.WhenAnyValue(v => v.ViewModel!.ModList)
                .Subscribe(ml =>
                {
                    var name = ml?.Name ?? "";
                    var author = ml?.Author ?? "";
                    SetupModlistNameText.Text = name;
                    SetupTitleText.Text = name;
                    InstallingTitleText.Text = name;
                    CompletedTitleText.Text = name;
                    SetupAuthorText.Text = string.IsNullOrWhiteSpace(author) ? "" : $"by {author}";
                })
                .DisposeWith(disposables);

            // ── Loading lock → loading bar ────────────────────────────────
            this.WhenAnyValue(v => v.ViewModel!.LoadingLock.IsLoading)
                .Subscribe(loading => ModlistLoadingBar.IsVisible = loading)
                .DisposeWith(disposables);

            // ── Progress ──────────────────────────────────────────────────
            this.WhenAnyValue(v => v.ViewModel!.ProgressText)
                .Subscribe(t =>
                {
                    InstallProgressText.Text = t;
                    StatusDetailText.Text = t;
                })
                .DisposeWith(disposables);

            this.WhenAnyValue(v => v.ViewModel!.ProgressPercent)
                .Subscribe(p => InstallProgressBar.Value = p.Value)
                .DisposeWith(disposables);

            // ── Speed indicators ──────────────────────────────────────────
            this.WhenAnyValue(v => v.ViewModel!.HashingSpeed)
                .Subscribe(s => HashSpeedText.Text = s)
                .DisposeWith(disposables);
            this.WhenAnyValue(v => v.ViewModel!.ExtractingSpeed)
                .Subscribe(s => ExtractionSpeedText.Text = s)
                .DisposeWith(disposables);
            this.WhenAnyValue(v => v.ViewModel!.DownloadingSpeed)
                .Subscribe(s => DownloadSpeedText.Text = s)
                .DisposeWith(disposables);

            // ── InstallState → show/hide panels ───────────────────────────
            this.WhenAnyValue(v => v.ViewModel!.InstallState)
                .Subscribe(state =>
                {
                    SetupGrid.IsVisible        = state == InstallState.Configuration;
                    InstallationGrid.IsVisible = state == InstallState.Installing || state == InstallState.Failure;
                    CompletedGrid.IsVisible    = state == InstallState.Success;

                    // Failure sub-panel
                    StoppedBorder.IsVisible = state == InstallState.Failure;
                    StoppedInstallMsg.Text  = state == InstallState.Failure ? "Installation failed" : "";
                    StoppedIcon.Symbol      = state == InstallState.Failure ? Symbol.ErrorCircle : Symbol.CheckmarkCircle;

                    // Show edit/retry only on failure
                    EditInstallDetailsButton.IsVisible = state == InstallState.Failure;
                    RetryButton.IsVisible              = state == InstallState.Failure;
                    CancelButton.IsVisible             = state == InstallState.Installing;
                })
                .DisposeWith(disposables);

            // ── InstallResult → stopped message details ───────────────────
            this.WhenAnyValue(v => v.ViewModel!.InstallResult)
                .Subscribe(result =>
                {
                    if (result == null) return;
                    StoppedTitle.Text = result switch
                    {
                        Wabbajack.Installer.InstallResult.Succeeded    => "Installation succeeded",
                        Wabbajack.Installer.InstallResult.Cancelled    => "Installation was cancelled",
                        Wabbajack.Installer.InstallResult.DownloadFailed => "One or more downloads failed",
                        Wabbajack.Installer.InstallResult.NotEnoughSpace => "Not enough disk space",
                        Wabbajack.Installer.InstallResult.GameMissing  => "Game installation not found",
                        _                                               => "An error occurred"
                    };
                    StoppedButton.Content = result == Wabbajack.Installer.InstallResult.DownloadFailed
                        ? "Open Logs"
                        : "Open Logs";
                    if (StoppedButton.Command == null)
                        StoppedButton.Command = ViewModel?.OpenLogFolderCommand;
                })
                .DisposeWith(disposables);
        });
    }
}
