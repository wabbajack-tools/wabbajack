using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Windows.Controls;
using ReactiveUI;
using System.Windows;

namespace Wabbajack
{
    /// <summary>
    /// Interaction logic for InstallationView.xaml
    /// </summary>
    public partial class InstallationView : ReactiveUserControl<InstallerVM>
    {
        public InstallationView()
        {
            InitializeComponent();
            this.WhenActivated(dispose =>
            {
                this.MarkAsNeeded<InstallationView, InstallerVM, bool>(this.ViewModel, x => x.Installing);
                this.MarkAsNeeded<InstallationView, InstallerVM, bool>(this.ViewModel, x => x.Slideshow.Enable);

                // General progress indicators
                this.WhenAny(x => x.ViewModel.PercentCompleted)
                    .Select(x => (double)x)
                    .BindToStrict(this, x => x.HeatedBackground.PercentCompleted)
                    .DisposeWith(dispose);

                // Top Progress Bar
                this.WhenAny(x => x.ViewModel.ModListName)
                    .BindToStrict(this, x => x.TopProgressBar.Title)
                    .DisposeWith(dispose);
                this.WhenAny(x => x.ViewModel.ProgressTitle)
                    .BindToStrict(this, x => x.TopProgressBar.StatePrefixTitle)
                    .DisposeWith(dispose);
                this.WhenAny(x => x.ViewModel.PercentCompleted)
                    .Select(x => (double)x)
                    .BindToStrict(this, x => x.TopProgressBar.ProgressPercent)
                    .DisposeWith(dispose);
                this.WhenAny(x => x.ViewModel.BackCommand)
                    .BindToStrict(this, x => x.BackButton.Command)
                    .DisposeWith(dispose);

                // Detail Image
                this.WhenAny(x => x.ViewModel.TitleText)
                    .BindToStrict(this, x => x.DetailImage.Title)
                    .DisposeWith(dispose);
                this.WhenAny(x => x.ViewModel.AuthorText)
                    .BindToStrict(this, x => x.DetailImage.Author)
                    .DisposeWith(dispose);
                this.WhenAny(x => x.ViewModel.Description)
                    .BindToStrict(this, x => x.DetailImage.Description)
                    .DisposeWith(dispose);
                this.WhenAny(x => x.ViewModel.Image)
                    .BindToStrict(this, x => x.DetailImage.Image)
                    .DisposeWith(dispose);
                this.WhenAny(x => x.ViewModel.LoadingModlist)
                    .Select(x => x ? Visibility.Visible : Visibility.Collapsed)
                    .BindToStrict(this, x => x.ModlistLoadingRing.Visibility)
                    .DisposeWith(dispose);

                // Slideshow controls
                this.WhenAny(x => x.ViewModel.Slideshow.SlideShowNextItemCommand)
                    .BindToStrict(this, x => x.SkipSlideButton.Command)
                    .DisposeWith(dispose);
                this.BindStrict(this.ViewModel, x => x.Slideshow.Enable, x => x.PlayPauseButton.IsChecked,
                        vmToViewConverter: x => x,
                        viewToVmConverter: x => x ?? true)
                    .DisposeWith(dispose);
                this.WhenAny(x => x.ViewModel.Slideshow.Enable)
                    .Select(enabled =>
                    {
                        return $"{(enabled ? "Pause" : "Play")} slideshow";
                    })
                    .BindToStrict(this, x => x.PlayPauseButton.ToolTip)
                    .DisposeWith(dispose);
                this.WhenAny(x => x.ViewModel.Slideshow.VisitNexusSiteCommand)
                    .BindToStrict(this, x => x.OpenWebsite.Command)
                    .DisposeWith(dispose);
                this.BindStrict(this.ViewModel, x => x.Slideshow.ShowNSFW, x => x.ShowNSFWButton.IsChecked,
                        vmToViewConverter: x => x,
                        viewToVmConverter: x => x ?? true)
                    .DisposeWith(dispose);
                this.WhenAny(x => x.ViewModel.Slideshow.ShowNSFW)
                    .Select(show =>
                    {
                        return $"{(show ? "Hide" : "Show")} NSFW mods";
                    })
                    .BindToStrict(this, x => x.ShowNSFWButton.ToolTip)
                    .DisposeWith(dispose);
                this.WhenAny(x => x.ViewModel.Slideshow.ShowNSFW)
                    .Select(show => show ? Visibility.Collapsed : Visibility.Visible)
                    .BindToStrict(this, x => x.NSFWSlashIcon.Visibility)
                    .DisposeWith(dispose);

                // Bottom Input Customization
                this.WhenAny(x => x.ViewModel.StartedInstallation)
                    .Select(started => started ? Visibility.Hidden : Visibility.Visible)
                    .BindToStrict(this, x => x.BottomButtonInputGrid.Visibility)
                    .DisposeWith(dispose);
                this.WhenAny(x => x.ViewModel.OpenReadmeCommand)
                    .BindToStrict(this, x => x.OpenReadmePreInstallButton.Command)
                    .DisposeWith(dispose);
                this.WhenAny(x => x.ViewModel.VisitModListWebsiteCommand)
                    .BindToStrict(this, x => x.VisitWebsitePreInstallButton.Command)
                    .DisposeWith(dispose);
                this.WhenAny(x => x.ViewModel.ShowManifestCommand)
                    .BindToStrict(this, x => x.ShowManifestPreInstallButton.Command)
                    .DisposeWith(dispose);
                this.WhenAny(x => x.ViewModel.StartedInstallation)
                    .Select(started => started ? Visibility.Collapsed : Visibility.Visible)
                    .BindToStrict(this, x => x.InstallationConfigurationView.Visibility)
                    .DisposeWith(dispose);
                this.WhenAny(x => x.ViewModel.Installer.ConfigVisualVerticalOffset)
                    .Select(i => (double)i)
                    .BindToStrict(this, x => x.InstallConfigSpacer.Height)
                    .DisposeWith(dispose);
                this.WhenAny(x => x.ViewModel.ModListLocation)
                    .BindToStrict(this, x => x.ModListLocationPicker.PickerVM)
                    .DisposeWith(dispose);
                this.WhenAny(x => x.ViewModel.Installer)
                    .BindToStrict(this, x => x.InstallerCustomizationContent.Content)
                    .DisposeWith(dispose);
                this.WhenAny(x => x.ViewModel.BeginCommand)
                    .BindToStrict(this, x => x.BeginButton.Command)
                    .DisposeWith(dispose);

                // Bottom mid-install display
                this.WhenAny(x => x.ViewModel.StartedInstallation)
                    .Select(started => started ? Visibility.Visible : Visibility.Hidden)
                    .BindToStrict(this, x => x.MidInstallDisplayGrid.Visibility)
                    .DisposeWith(dispose);
                this.WhenAny(x => x.ViewModel.PercentCompleted)
                    .Select(x => (double)x)
                    .BindToStrict(this, x => x.LogView.ProgressPercent)
                    .DisposeWith(dispose);
                this.WhenAny(x => x.ViewModel.PercentCompleted)
                    .Select(f => (double)f)
                    .BindToStrict(this, x => x.CpuView.ProgressPercent)
                    .DisposeWith(dispose);
                this.WhenAny(x => x.ViewModel.MWVM.Settings)
                    .BindToStrict(this, x => x.CpuView.SettingsHook)
                    .DisposeWith(dispose);
                this.WhenAny(x => x.ViewModel.ActiveGlobalUserIntervention)
                    .Select(x => x == null ? Visibility.Visible : Visibility.Collapsed)
                    .BindToStrict(this, x => x.CpuView.Visibility)
                    .DisposeWith(dispose);
                this.WhenAny(x => x.ViewModel.ActiveGlobalUserIntervention)
                    .Select(x => x != null ? Visibility.Visible : Visibility.Collapsed)
                    .BindToStrict(this, x => x.UserInterventionsControl.Visibility)
                    .DisposeWith(dispose);
                this.WhenAny(x => x.ViewModel.Completed)
                    .Select(completed => completed != null ? Visibility.Visible : Visibility.Collapsed)
                    .BindToStrict(this, x => x.InstallComplete.Visibility)
                    .DisposeWith(dispose);
            });
        }
    }
}
