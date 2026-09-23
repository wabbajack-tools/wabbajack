using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.ReactiveUI;
using FluentIcons.Common;
using ReactiveUI;
using Wabbajack.App.Avalonia.Converters;
using Wabbajack.App.Avalonia.Messages;
using Wabbajack.App.Avalonia.Util;
using Wabbajack.App.Avalonia.ViewModels.Installers;
using Wabbajack.Paths;

namespace Wabbajack.App.Avalonia.Views.Installers;

public partial class InstallationView : ReactiveUserControl<InstallationVM>
{
    // Transparent66ForegroundBrush on the stopped message, as WPF draws it: translucent text is blended in
    // gamma space, so these are WPF's resulting colours over the red and the green fill.
    private static readonly IBrush DimOnError = new SolidColorBrush(Color.Parse("#C3BEC0"));
    private static readonly IBrush DimOnSuccess = new SolidColorBrush(Color.Parse("#C3D4C4"));

    public InstallationView()
    {
        InitializeComponent();

        // Sizes WPF bound through MathConverter, rounded to the pixel as WPF's layout rounding did: the image
        // is 16:9 at its own width, and the header's toggles and buttons are fractions of the column.
        InstallDetailImage.GetObservable(BoundsProperty)
            .Subscribe(b => InstallDetailImage.Height = Math.Round(b.Width / (16.0 / 9.0)));
        RightSideGrid.GetObservable(BoundsProperty)
            .Subscribe(b =>
            {
                LogToggleButton.Width = Math.Round(b.Width / 7);
                ErrorToggleButton.Width = Math.Round(b.Width / 6.5);
                OpenReadmeButton.Width = Math.Round(b.Width / 6.5);
                OpenLogFolderButton.Width = Math.Round(b.Width / 5.5);
            });
        StoppedMessage.GetObservable(BoundsProperty)
            .Subscribe(_ => PlaceStoppedIcon());

        this.WhenActivated(disposables =>
        {
            this.WhenAnyValue(x => x.ViewModel!.Installer.Location)
                .Subscribe(picker => InstallationLocationPicker.PickerVM = picker)
                .DisposeWith(disposables);

            this.WhenAnyValue(x => x.ViewModel!.Installer.DownloadLocation)
                .Subscribe(picker => DownloadLocationPicker.PickerVM = picker)
                .DisposeWith(disposables);

            InstallationLocationPicker.PickerVM!.AdditionalError = ViewModel!.WhenAnyValue(vm => vm.ValidationResult)
                .Where(vr => vr is InstallPathValidationResult);
            DownloadLocationPicker.PickerVM!.AdditionalError = ViewModel.WhenAnyValue(vm => vm.ValidationResult)
                .Where(vr => vr is DownloadsPathValidationResult);

            ViewModel.WhenAnyValue(vm => vm.ValidationResult)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(vr =>
                {
                    if (vr == null) return;
                    if (vr.Succeeded)
                    {
                        ErrorStateBorder.IsVisible = false;
                        InstallButton.IsEnabled = true;
                    }
                    else
                    {
                        InstallButton.IsEnabled = false;
                        ErrorStateBorder.IsVisible = true;
                        ErrorStateText.Inlines = new InlineCollection
                        {
                            new Run("Cannot start installation!") { FontWeight = FontWeight.Bold },
                            new Run(" " + vr.Reason)
                        };
                    }
                })
                .DisposeWith(disposables);

            this.BindCommand(ViewModel, vm => vm.OpenReadmeCommand, v => v.DocumentationButton)
                .DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.OpenWebsiteCommand, v => v.WebsiteButton)
                .DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.OpenCommunityCommand, v => v.CommunityButton)
                .DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.OpenManifestCommand, v => v.ManifestButton)
                .DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.CancelCommand, v => v.CancelButton)
                .DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.EditInstallDetailsCommand, v => v.EditInstallDetailsButton)
                .DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.InstallCommand, v => v.RetryButton)
                .DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.InstallCommand, v => v.InstallButton)
                .DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.BackToGalleryCommand, v => v.BackToGalleryButton)
                .DisposeWith(disposables);

            this.WhenAnyValue(v => v.ViewModel!.HashingSpeed)
                .Subscribe(t => HashSpeedText.Text = t)
                .DisposeWith(disposables);
            this.WhenAnyValue(v => v.ViewModel!.ExtractingSpeed)
                .Subscribe(t => ExtractionSpeedText.Text = t)
                .DisposeWith(disposables);
            this.WhenAnyValue(v => v.ViewModel!.DownloadingSpeed)
                .Subscribe(t => DownloadSpeedText.Text = t)
                .DisposeWith(disposables);

            this.BindCommand(ViewModel, vm => vm.OpenReadmeCommand, v => v.OpenReadmeButton)
                .DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.OpenLogFolderCommand, v => v.OpenLogFolderButton)
                .DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.OpenFailureArticleCommand, v => v.ReadFullArticleButton)
                .DisposeWith(disposables);

            // Collapsed rather than hidden: it sits in its own auto-sized column, so a hidden one would hold a
            // gap open beside the Readme button.
            this.WhenAnyValue(x => x.LogToggleButton.IsChecked)
                .Subscribe(x => OpenLogFolderButton.IsVisible = x ?? false)
                .DisposeWith(disposables);

            ViewModel.WhenAnyValue(vm => vm.InstallResult)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(result =>
                {
                    StoppedTitle.Text = result?.GetTitle() ?? string.Empty;
                    StoppedDescription.Text = result?.GetDescription() ?? string.Empty;
                })
                .DisposeWith(disposables);

            this.BindCommand(ViewModel, vm => vm.OpenLogFolderCommand, v => v.StoppedButton)
                .DisposeWith(disposables);

            this.WhenAnyValue(x => x.ViewModel!.Preflight)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(preflight => PreflightView.ViewModel = preflight)
                .DisposeWith(disposables);

            ViewModel.WhenAnyValue(vm => vm.InstallState)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(x =>
                {
                    SetupGrid.IsVisible = x == InstallState.Configuration;
                    PreflightView.IsVisible = x == InstallState.Preflight;
                    InstallationGrid.IsVisible = x is InstallState.Installing or InstallState.Failure;
                    CompletedInstallationGrid.IsVisible = x == InstallState.Success;

                    CpuView.IsVisible = x == InstallState.Installing;
                    InstallationGrid.ColumnDefinitions[2].Width = x == InstallState.Installing
                        ? new GridLength(3, GridUnitType.Star)
                        : new GridLength(4, GridUnitType.Star);
                    WorkerIndicators.IsVisible = x == InstallState.Installing;
                    StoppedMessage.IsVisible = x == InstallState.Failure;
                    StoppedBorder.Background = PreflightConverters.Brush(x == InstallState.Failure ? "ErrorBrush" : "SuccessBrush");
                    StoppedIcon.Symbol = x == InstallState.Failure ? Symbol.ErrorCircle : Symbol.CheckmarkCircle;
                    StoppedInstallMsg.Text = x == InstallState.Failure ? "Installation failed" : "Installation succeeded";
                    StoppedInstallMsg.Foreground = IfThisDidNotHelpText.Foreground =
                        x == InstallState.Failure ? DimOnError : DimOnSuccess;

                    CancelButton.IsVisible = x == InstallState.Installing;
                    EditInstallDetailsButton.IsVisible = x == InstallState.Failure;
                    RetryButton.IsVisible = x == InstallState.Failure;

                    if (x is InstallState.Failure or InstallState.Success)
                        LogToggleButton.IsChecked = true;

                    if (x is InstallState.Installing or InstallState.Preflight)
                        HideNavigation.Send();
                    else
                        ShowNavigation.Send();
                })
                .DisposeWith(disposables);

            ViewModel.WhenAnyValue(vm => vm.SuggestedInstallFolder)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(x =>
                {
                    InstallationLocationPicker.Watermark = x;
                    if (string.IsNullOrEmpty(ViewModel?.Installer?.Location?.TargetPath.ToString()))
                        ViewModel!.Installer.Location.TargetPath = (AbsolutePath)x;
                })
                .DisposeWith(disposables);

            ViewModel.WhenAnyValue(vm => vm.SuggestedDownloadFolder)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(x =>
                {
                    DownloadLocationPicker.Watermark = x;
                    if (string.IsNullOrEmpty(ViewModel?.Installer?.DownloadLocation?.TargetPath.ToString()))
                        ViewModel!.Installer.DownloadLocation.TargetPath = (AbsolutePath)x;
                })
                .DisposeWith(disposables);

            // The same image, title, author and version on all four pictures of the list.
            var images = new[] { DetailImage, InstallDetailImage, CompletedImage, PreflightView.DetailImage };

            ViewModel.WhenAnyValue(vm => vm.ModListImage)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(image => { foreach (var view in images) view.Image = image; })
                .DisposeWith(disposables);

            ViewModel.WhenAnyValue(vm => vm.ModList.Author)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(author => { foreach (var view in images) view.Author = author; })
                .DisposeWith(disposables);

            ViewModel.WhenAnyValue(vm => vm.ModList.Name)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(name => { foreach (var view in images) view.Title = name; })
                .DisposeWith(disposables);

            ViewModel.WhenAnyValue(vm => vm.ModList.Version)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(version => { foreach (var view in images) view.Version = version; })
                .DisposeWith(disposables);

            ViewModel.WhenAnyValue(vm => vm.LoadingLock.IsLoading)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(loading => ModlistLoadingRing.IsVisible = loading)
                .DisposeWith(disposables);

            LogToggleButton.IsCheckedChanged += OnLogToggled;
            ErrorToggleButton.IsCheckedChanged += OnErrorToggled;
            Disposable.Create(() =>
            {
                LogToggleButton.IsCheckedChanged -= OnLogToggled;
                ErrorToggleButton.IsCheckedChanged -= OnErrorToggled;
            }).DisposeWith(disposables);

            ViewModel.WhenAnyValue(vm => vm.FailureDetailsTitle)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(t => FailureDetailsTitleText.Text = t)
                .DisposeWith(disposables);

            ViewModel.WhenAnyValue(vm => vm.FailureDetailsDescription)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(t => FailureDetailsDescriptionText.Text = t)
                .DisposeWith(disposables);

            // Only a matched article has more to read; the other three outcomes are the whole of what the
            // diagnosis found, so there is nothing to send the user to a browser for.
            ViewModel.WhenAnyValue(vm => vm.FailureArticleMarkdown)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(markdown => ReadFullArticleButton.IsVisible = !string.IsNullOrWhiteSpace(markdown))
                .DisposeWith(disposables);

            this.BindCommand(ViewModel, vm => vm.OpenReadmeCommand, v => v.ReadmeButton)
                .DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.OpenInstallFolderCommand, v => v.OpenFolderButton)
                .DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.CreateShortcutCommand, v => v.CreateShortcutButton)
                .DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.DiagnoseFailureCommand, v => v.DiagnoseButton)
                .DisposeWith(disposables);

            DiagnoseButton.Click += OnDiagnoseClicked;
            JoinDiscordButton.Click += OnJoinDiscordClicked;
            VisitWikiButton.Click += OnVisitWikiClicked;
            Disposable.Create(() =>
            {
                DiagnoseButton.Click -= OnDiagnoseClicked;
                JoinDiscordButton.Click -= OnJoinDiscordClicked;
                VisitWikiButton.Click -= OnVisitWikiClicked;
            }).DisposeWith(disposables);

            // The log is what the panel opens on now that the readme is a button rather than a tab.
            LogToggleButton.IsChecked = true;
            ShowLog(true);
        });
    }

    private void OnLogToggled(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (LogToggleButton.IsChecked == true) ShowLog(true);
    }

    private void OnErrorToggled(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (ErrorToggleButton.IsChecked == true) ShowLog(false);
    }

    /// <summary>What WPF's Checked handlers did: the two toggles are a pair, and each shows its own panel.</summary>
    private void ShowLog(bool log)
    {
        if (log) ErrorToggleButton.IsChecked = false;
        else LogToggleButton.IsChecked = false;
        LogView.IsVisible = log;
        ErrorSummaryGrid.IsVisible = !log;
    }

    private void OnDiagnoseClicked(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
        => ErrorToggleButton.IsChecked = true;

    private void OnJoinDiscordClicked(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
        => UIUtils.OpenWebsite(new Uri("https://www.wabbajack.org/discord"));

    private void OnVisitWikiClicked(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
        => UIUtils.OpenWebsite(new Uri("https://wiki.wabbajack.org"));

    /// <summary>
    ///     The stopped message's icon, as WPF sized and placed it: one and a half times the message's height,
    ///     starting two thirds of its own width short of the right edge, and clipped to the message. When that
    ///     would start left of the message WPF refused the negative width for its spacer, which then took no
    ///     room, so the icon starts at the left edge instead.
    /// </summary>
    private void PlaceStoppedIcon()
    {
        var bounds = StoppedMessage.Bounds;
        var size = Math.Round(bounds.Height * 1.5);
        if (size <= 0) return;
        StoppedIcon.FontSize = size;
        Canvas.SetLeft(StoppedIcon, Math.Max(0, bounds.Width - size / 1.5));
    }
}
