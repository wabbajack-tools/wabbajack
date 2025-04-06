using System.Reactive.Disposables;
using System.Reactive.Linq;
using ReactiveUI;
using System.Windows;
using System;
using System.Linq;
using Wabbajack.Paths;
using Wabbajack.Messages;
using ReactiveMarbles.ObservableEvents;
using System.Windows.Controls;
using System.Reactive.Concurrency;
using System.Windows.Media;
using Symbol = FluentIcons.Common.Symbol;
using Wabbajack.Installer;

namespace Wabbajack;

/// <summary>
/// Interaction logic for InstallationView.xaml
/// </summary>
public partial class InstallationView : ReactiveUserControl<InstallationVM>
{
    public InstallationView()
    {
        InitializeComponent();
        this.WhenActivated(disposables =>
        {
            this.Bind(ViewModel, vm => vm.Installer.Location, view => view.InstallationLocationPicker.PickerVM)
                .DisposeWith(disposables);

            this.Bind(ViewModel, vm => vm.Installer.DownloadLocation, view => view.DownloadLocationPicker.PickerVM)
                .DisposeWith(disposables);

            InstallationLocationPicker.PickerVM.AdditionalError = ViewModel.WhenAnyValue(vm => vm.ValidationResult).Where(vr => vr is InstallPathValidationResult);
            DownloadLocationPicker.PickerVM.AdditionalError = ViewModel.WhenAnyValue(vm => vm.ValidationResult).Where(vr => vr is DownloadsPathValidationResult);
            ViewModel.WhenAnyValue(vm => vm.ValidationResult)
                     .Subscribe(vr =>
                     {
                         if (vr.Succeeded)
                         {
                             ErrorStateBorder.Visibility = Visibility.Collapsed;
                             InstallButton.IsEnabled = true;
                         }
                         else
                         {
                             InstallButton.IsEnabled = false;
                             ErrorStateBorder.Visibility = Visibility.Visible;
                             ErrorStateReasonText.Text = vr.Reason;
                         }
                     })
                     .DisposeWith(disposables);

            this.BindCommand(ViewModel, vm => vm.OpenReadmeCommand, v => v.DocumentationButton)
                .DisposeWith(disposables);

            this.BindCommand(ViewModel, vm => vm.OpenWebsiteCommand, v => v.WebsiteButton)
                .DisposeWith(disposables);

            this.BindCommand(ViewModel, vm => vm.OpenDiscordButton, v => v.DiscordButton)
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

            this.WhenAnyValue(v => v.ViewModel.HashingSpeed)
                .BindToStrict(this, v => v.HashSpeedText.Text)
                .DisposeWith(disposables);

            this.WhenAnyValue(v => v.ViewModel.ExtractingSpeed)
                .BindToStrict(this, v => v.ExtractionSpeedText.Text)
                .DisposeWith(disposables);

            this.WhenAnyValue(v => v.ViewModel.DownloadingSpeed)
                .BindToStrict(this, v => v.DownloadSpeedText.Text)
                .DisposeWith(disposables);

            this.BindCommand(ViewModel, vm => vm.OpenReadmeCommand, v => v.OpenReadmeButton)
                .DisposeWith(disposables);

            this.BindCommand(ViewModel, vm => vm.OpenLogFolderCommand, v => v.OpenLogFolderButton)
                .DisposeWith(disposables);

            this.WhenAnyValue(x => x.ReadmeToggleButton.IsChecked)
                .Select(x => x ?? false ? Visibility.Visible : Visibility.Hidden)
                .BindToStrict(this, x => x.OpenReadmeButton.Visibility)
                .DisposeWith(disposables);

            this.WhenAnyValue(x => x.LogToggleButton.IsChecked)
                .Select(x => x ?? false ? Visibility.Visible : Visibility.Hidden)
                .BindToStrict(this, x => x.OpenLogFolderButton.Visibility)
                .DisposeWith(disposables);

            ViewModel.WhenAnyValue(vm => vm.InstallResult)
                     .ObserveOnGuiThread()
                     .Subscribe(result =>
                     {
                         StoppedTitle.Text = result?.GetTitle() ?? string.Empty;
                         StoppedDescription.Text = result?.GetDescription() ?? string.Empty;
                         switch(result)
                         {
                             case InstallResult.DownloadFailed:
                                 StoppedButton.Command = ViewModel.OpenMissingArchivesCommand;
                                 StoppedButton.Icon = Symbol.DocumentGlobe;
                                 StoppedButton.Text = "Show Missing Archives";
                                 break;

                             default:
                                 StoppedButton.Command = ViewModel.OpenLogFolderCommand;
                                 StoppedButton.Icon = Symbol.FolderOpen;
                                 StoppedButton.Text = "Open Logs Folder";
                                 break;
                         }
                     })
                     .DisposeWith(disposables);

            ViewModel.WhenAnyValue(vm => vm.InstallState)
                     .ObserveOnGuiThread()
                     .Subscribe(x =>
                     {
                         SetupGrid.Visibility = x == InstallState.Configuration ? Visibility.Visible : Visibility.Collapsed;
                         InstallationGrid.Visibility = x == InstallState.Installing || x == InstallState.Failure ? Visibility.Visible : Visibility.Collapsed;
                         CompletedInstallationGrid.Visibility = x == InstallState.Success ? Visibility.Visible : Visibility.Collapsed;

                         CpuView.Visibility = x == InstallState.Installing ? Visibility.Visible : Visibility.Collapsed;
                         InstallationRightColumn.Width = x == InstallState.Installing ? new GridLength(3, GridUnitType.Star) : new GridLength(4, GridUnitType.Star);
                         WorkerIndicators.Visibility = x == InstallState.Installing ? Visibility.Visible : Visibility.Collapsed;
                         StoppedMessage.Visibility = x == InstallState.Failure ? Visibility.Visible : Visibility.Collapsed;
                         StoppedBorder.Background = x == InstallState.Failure ? (Brush)Application.Current.Resources["ErrorBrush"] : (Brush)Application.Current.Resources["SuccessBrush"];
                         StoppedIcon.Symbol = x == InstallState.Failure ? Symbol.ErrorCircle : Symbol.CheckmarkCircle;
                         StoppedInstallMsg.Text = x == InstallState.Failure ? "Installation failed" : "Installation succeeded";

                         CancelButton.Visibility = x == InstallState.Installing ? Visibility.Visible : Visibility.Collapsed;
                         EditInstallDetailsButton.Visibility = x == InstallState.Failure ? Visibility.Visible : Visibility.Collapsed;
                         RetryButton.Visibility = x == InstallState.Failure ? Visibility.Visible : Visibility.Collapsed;


                         if (x == InstallState.Failure || x == InstallState.Success)
                             LogToggleButton.IsChecked = true;

                         if (x == InstallState.Installing)
                             HideNavigation.Send();
                         else
                             ShowNavigation.Send();
                     })
                     .DisposeWith(disposables);

            ViewModel.WhenAnyValue(vm => vm.SuggestedInstallFolder)
                     .ObserveOnGuiThread()
                     .Subscribe(x =>
                     {
                         InstallationLocationPicker.Watermark = x;
                         if (string.IsNullOrEmpty(ViewModel?.Installer?.Location?.TargetPath.ToString()))
                             ViewModel.Installer.Location.TargetPath = (AbsolutePath)x;
                     })
                    .DisposeWith(disposables);

            ViewModel.WhenAnyValue(vm => vm.SuggestedDownloadFolder)
                     .ObserveOnGuiThread()
                     .Subscribe(x =>
                     {
                         DownloadLocationPicker.Watermark = x;
                         if (string.IsNullOrEmpty(ViewModel?.Installer?.DownloadLocation?.TargetPath.ToString()))
                             ViewModel.Installer.DownloadLocation.TargetPath = (AbsolutePath)x;
                     })
                    .DisposeWith(disposables);

            ViewModel.WhenAnyValue(vm => vm.ModListImage)
                     .BindToStrict(this, v => v.DetailImage.Image)
                     .DisposeWith(disposables);

            ViewModel.WhenAnyValue(vm => vm.ModListImage)
                     .BindToStrict(this, v => v.InstallDetailImage.Image)
                     .DisposeWith(disposables);

            ViewModel.WhenAnyValue(vm => vm.ModListImage)
                     .BindToStrict(this, v => v.CompletedImage.Image)
                     .DisposeWith(disposables);

            ViewModel.WhenAnyValue(vm => vm.ModList.Author)
                     .BindToStrict(this, v => v.DetailImage.Author)
                     .DisposeWith(disposables);

            ViewModel.WhenAnyValue(vm => vm.ModList.Author)
                     .BindToStrict(this, v => v.InstallDetailImage.Author)
                     .DisposeWith(disposables);

            ViewModel.WhenAnyValue(vm => vm.ModList.Author)
                     .BindToStrict(this, v => v.CompletedImage.Author)
                     .DisposeWith(disposables);

            ViewModel.WhenAnyValue(vm => vm.ModList.Name)
                     .BindToStrict(this, v => v.DetailImage.Title)
                     .DisposeWith(disposables);

            ViewModel.WhenAnyValue(vm => vm.ModList.Name)
                     .BindToStrict(this, v => v.InstallDetailImage.Title)
                     .DisposeWith(disposables);

            ViewModel.WhenAnyValue(vm => vm.ModList.Name)
                     .BindToStrict(this, v => v.CompletedImage.Title)
                     .DisposeWith(disposables);

            ViewModel.WhenAnyValue(vm => vm.ModList.Version)
                     .BindToStrict(this, v => v.DetailImage.Version)
                     .DisposeWith(disposables);

            ViewModel.WhenAnyValue(vm => vm.ModList.Version)
                     .BindToStrict(this, v => v.InstallDetailImage.Version)
                     .DisposeWith(disposables);

            ViewModel.WhenAnyValue(vm => vm.ModList.Version)
                     .BindToStrict(this, v => v.CompletedImage.Version)
                     .DisposeWith(disposables);

            ViewModel.WhenAnyValue(vm => vm.LoadingLock.IsLoading)
                .Select(loading => loading ? Visibility.Visible : Visibility.Collapsed)
                .BindToStrict(this, v => v.ModlistLoadingRing.Visibility)
                .DisposeWith(disposables);

            this.WhenAnyValue(x => x.ViewModel.ModList.Readme)
                     .Select(x =>
                     {
                         var humanReadableReadme = UIUtils.GetHumanReadableReadmeLink(ViewModel.ModList.Readme);
                         if (Uri.TryCreate(humanReadableReadme, UriKind.Absolute, out var uri))
                         {
                             return uri;
                         }
                         return default;
                     })
                     .BindToStrict(this, x => x.ViewModel.ReadmeBrowser.Source)
                     .DisposeWith(disposables);

            ReadmeToggleButton.Events().Checked
                .ObserveOnGuiThread()
                .Subscribe(_ =>
                {
                    LogToggleButton.IsChecked = false;
                    LogView.Visibility = Visibility.Collapsed;
                    ReadmeBrowserGrid.Visibility = Visibility.Visible;
                })
                .DisposeWith(disposables);

            LogToggleButton.Events().Checked
                .ObserveOnGuiThread()
                .Subscribe(_ =>
                {
                    ReadmeToggleButton.IsChecked = false;
                    LogView.Visibility = Visibility.Visible;
                    ReadmeBrowserGrid.Visibility = Visibility.Collapsed;
                })
                .DisposeWith(disposables);


            this.WhenAnyValue(x => x.ReadmeBrowserGrid.Visibility)
                .Where(x => x == Visibility.Visible)
                .Subscribe(x =>
                {
                    if (x == Visibility.Visible)
                        TakeWebViewOwnershipForReadme();
                })
                .DisposeWith(disposables);

            this.BindCommand(ViewModel, vm => vm.OpenReadmeCommand, v => v.ReadmeButton)
                .DisposeWith(disposables);

            this.BindCommand(ViewModel, vm => vm.OpenInstallFolderCommand, v => v.OpenFolderButton)
                .DisposeWith(disposables);

            this.BindCommand(ViewModel, vm => vm.CreateShortcutCommand, v => v.CreateShortcutButton)
                .DisposeWith(disposables);

            // Initially, readme tab should be visible
            ReadmeToggleButton.IsChecked = true;

            MessageBus.Current.Listen<ShowFloatingWindow>()
                              .ObserveOnGuiThread()
                              .Subscribe(msg =>
                              {
                                  if (msg.Screen == FloatingScreenType.None && (ReadmeToggleButton.IsChecked ?? false))
                                      ReadmeBrowserGrid.Visibility = Visibility.Visible;
                                  else
                                      ReadmeBrowserGrid.Visibility = Visibility.Collapsed;
                              })
                              .DisposeWith(disposables);

            /*
            // Slideshow
            ViewModel.WhenAnyValue(vm => vm.SlideShowTitle)
                .Select(f => f)
                .BindToStrict(this, view => view.DetailImage.Title)
                .DisposeWith(disposables);
            ViewModel.WhenAnyValue(vm => vm.SlideShowAuthor)
                .BindToStrict(this, view => view.DetailImage.Author)
                .DisposeWith(disposables);

            ViewModel.WhenAnyValue(vm => vm.SlideShowImage)
                .BindToStrict(this, view => view.DetailImage.Image)
                .DisposeWith(disposables);
            */
        });
    }

    private void TakeWebViewOwnershipForReadme()
    {
        RxApp.MainThreadScheduler.Schedule(() =>
        {
            ViewModel.ReadmeBrowser.Margin = new Thickness(0, 0, 0, 16);
            if (ViewModel.ReadmeBrowser.Parent != null)
            {
                ((Panel)ViewModel.ReadmeBrowser.Parent).Children.Remove(ViewModel.ReadmeBrowser);
            }
            ViewModel.ReadmeBrowser.Width = double.NaN;
            ViewModel.ReadmeBrowser.Height = double.NaN;
            ViewModel.ReadmeBrowser.Visibility = Visibility.Visible;
            if(!string.IsNullOrEmpty(ViewModel?.ModList?.Readme))
                ViewModel.ReadmeBrowser.Source = new Uri(UIUtils.GetHumanReadableReadmeLink(ViewModel.ModList.Readme));
            ReadmeBrowserGrid.Children.Add(ViewModel.ReadmeBrowser);
        });
    }
}
