using System.Reactive.Disposables;
using System.Reactive.Linq;
using ReactiveUI;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.ReactiveUI;
using System;
using System.Linq;
using Wabbajack.Paths;
using Wabbajack.Messages;
using System.Reactive.Concurrency;
using Symbol = FluentIcons.Common.Symbol;
using Wabbajack.Installer;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using System.Threading.Tasks;

namespace Wabbajack;

/// <summary>
/// Interaction logic for InstallationView.axaml
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
                             ErrorStateBorder.IsVisible = false;
                             InstallButton.IsEnabled = true;
                         }
                         else
                         {
                             InstallButton.IsEnabled = false;
                             ErrorStateBorder.IsVisible = true;
                             ErrorStateReasonText.Text = vr.Reason;
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

            // TODO: WPF's Visibility.Hidden keeps layout space reserved while invisible; Avalonia's
            // IsVisible=false behaves like Collapsed (layout space is not reserved). No direct
            // equivalent exists without a custom layout workaround.
            this.WhenAnyValue(x => x.ReadmeToggleButton.IsChecked)
                .Select(x => x ?? false)
                .BindToStrict(this, x => x.OpenReadmeButton.IsVisible)
                .DisposeWith(disposables);

            this.WhenAnyValue(x => x.LogToggleButton.IsChecked)
                .Select(x => x ?? false)
                .BindToStrict(this, x => x.OpenLogFolderButton.IsVisible)
                .DisposeWith(disposables);

            ViewModel.WhenAnyValue(vm => vm.InstallResult)
                     .ObserveOnGuiThread()
                     .Subscribe(result =>
                     {
                         StoppedTitle.Text = result?.GetTitle() ?? string.Empty;
                         StoppedDescription.Text = result?.GetDescription() ?? string.Empty;
                         switch (result)
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
                         SetupGrid.IsVisible = x == InstallState.Configuration;
                         InstallationGrid.IsVisible = x == InstallState.Installing || x == InstallState.Failure;
                         CompletedInstallationGrid.IsVisible = x == InstallState.Success;

                         CpuView.IsVisible = x == InstallState.Installing;
                         InstallationRightColumn.Width = x == InstallState.Installing ? new GridLength(3, GridUnitType.Star) : new GridLength(4, GridUnitType.Star);
                         WorkerIndicators.IsVisible = x == InstallState.Installing;
                         StoppedMessage.IsVisible = x == InstallState.Failure;
                         StoppedBorder.Background = x == InstallState.Failure ? (IBrush)Application.Current.Resources["ErrorBrush"] : (IBrush)Application.Current.Resources["SuccessBrush"];
                         StoppedIcon.Symbol = x == InstallState.Failure ? Symbol.ErrorCircle : Symbol.CheckmarkCircle;
                         StoppedInstallMsg.Text = x == InstallState.Failure ? "Installation failed" : "Installation succeeded";

                         CancelButton.IsVisible = x == InstallState.Installing;
                         EditInstallDetailsButton.IsVisible = x == InstallState.Failure;
                         RetryButton.IsVisible = x == InstallState.Failure;

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

            // TODO: InstallationVM.ModListImage is currently typed as
            // System.Windows.Media.Imaging.BitmapImage (WPF-specific). Avalonia's
            // DetailImageView.Image expects Avalonia.Media.IImage (e.g.
            // Avalonia.Media.Imaging.Bitmap). The bindings below are a structurally faithful port
            // of the WPF code but will need InstallationVM updated to expose an
            // Avalonia-compatible bitmap type before this compiles/works; that VM change is out of
            // scope for this view-only port (see ContributorView.axaml.cs for the same pattern).
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

            // WPF: Visibility.Visible/Collapsed via a Select converter -> Avalonia: bind IsVisible
            // directly to the bool.
            ViewModel.WhenAnyValue(vm => vm.LoadingLock.IsLoading)
                .BindToStrict(this, v => v.ModlistLoadingRing.IsVisible)
                .DisposeWith(disposables);

            // TODO: InstallationVM.ReadmeBrowser is still typed as Microsoft.Web.WebView2.Wpf.WebView2
            // in the (not-yet-ported) InstallationVM. Once that VM exposes an Avalonia-compatible
            // WebView2 (Avalonia.Controls.WebView2, from the WebView2.Avalonia package already
            // referenced by Wabbajack.App.Avalonia.csproj), ViewModel.ReadmeBrowser needs a Source
            // property of a compatible type for this binding to compile (see BrowserWindow.axaml.cs
            // for the same pattern).
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

            // WPF's ToggleButton.Checked routed event only fires on the transition into the
            // checked state; the equivalent here observes IsChecked and reacts only when it
            // becomes true, giving the same "select this tab, deselect the others" behavior as
            // the original three .Events().Checked subscriptions.
            this.WhenAnyValue(x => x.ReadmeToggleButton.IsChecked)
                .Where(x => x == true)
                .ObserveOnGuiThread()
                .Subscribe(_ =>
                {
                    LogToggleButton.IsChecked = false;
                    ErrorToggleButton.IsChecked = false;

                    LogView.IsVisible = false;
                    ErrorSummaryGrid.IsVisible = false;

                    ReadmeBrowserGrid.IsVisible = true;
                })
                .DisposeWith(disposables);

            this.WhenAnyValue(x => x.LogToggleButton.IsChecked)
                .Where(x => x == true)
                .ObserveOnGuiThread()
                .Subscribe(_ =>
                {
                    ReadmeToggleButton.IsChecked = false;
                    ErrorToggleButton.IsChecked = false;

                    ReadmeBrowserGrid.IsVisible = false;
                    ErrorSummaryGrid.IsVisible = false;

                    LogView.IsVisible = true;
                })
                .DisposeWith(disposables);

            this.WhenAnyValue(x => x.ErrorToggleButton.IsChecked)
                .Where(x => x == true)
                .ObserveOnGuiThread()
                .Subscribe(_ =>
                {
                    ReadmeToggleButton.IsChecked = false;
                    LogToggleButton.IsChecked = false;

                    ReadmeBrowserGrid.IsVisible = false;
                    LogView.IsVisible = false;

                    ErrorSummaryGrid.IsVisible = true;

                })
                .DisposeWith(disposables);

            ViewModel.WhenAnyValue(vm => vm.FailureDetailsDescription)
                .Where(desc => !string.IsNullOrEmpty(desc))
                .ObserveOnGuiThread()
                .Subscribe(async desc =>
                {
                    await RenderErrorMarkdownAsync(desc);
                })
                .DisposeWith(disposables);

            this.WhenAnyValue(x => x.ReadmeBrowserGrid.IsVisible)
                .Where(x => x)
                .Subscribe(_ => TakeWebViewOwnershipForReadme())
                .DisposeWith(disposables);

            this.BindCommand(ViewModel, vm => vm.OpenReadmeCommand, v => v.ReadmeButton)
                .DisposeWith(disposables);

            this.BindCommand(ViewModel, vm => vm.OpenInstallFolderCommand, v => v.OpenFolderButton)
                .DisposeWith(disposables);

            this.BindCommand(ViewModel, vm => vm.CreateShortcutCommand, v => v.CreateShortcutButton)
                .DisposeWith(disposables);

            this.BindCommand(ViewModel, vm => vm.DiagnoseFailureCommand, v => v.DiagnoseButton)
                .DisposeWith(disposables);

            // ReactiveMarbles' .Events() generator doesn't reliably wrap Avalonia controls;
            // use a direct Rx event wrapper around Button.Click instead.
            Observable.FromEventPattern<RoutedEventArgs>(h => DiagnoseButton.Click += h, h => DiagnoseButton.Click -= h)
                .ObserveOnGuiThread()
                .Subscribe(_ => ErrorToggleButton.IsChecked = true)
                .DisposeWith(disposables);

            Observable.FromEventPattern<RoutedEventArgs>(h => JoinDiscordButton.Click += h, h => JoinDiscordButton.Click -= h)
                .ObserveOnGuiThread()
                .Subscribe(_ => UIUtils.OpenWebsite(new Uri("https://www.wabbajack.org/discord")))
                .DisposeWith(disposables);

            Observable.FromEventPattern<RoutedEventArgs>(h => VisitWikiButton.Click += h, h => VisitWikiButton.Click -= h)
                .ObserveOnGuiThread()
                .Subscribe(_ => UIUtils.OpenWebsite(new Uri("https://wiki.wabbajack.org")))
                .DisposeWith(disposables);

            // Initially, readme tab should be visible
            ReadmeToggleButton.IsChecked = true;

            MessageBus.Current.Listen<ShowFloatingWindow>()
                              .ObserveOnGuiThread()
                              .Subscribe(msg =>
                              {
                                  if (msg.Screen == FloatingScreenType.None && (ReadmeToggleButton.IsChecked ?? false))
                                      ReadmeBrowserGrid.IsVisible = true;
                                  else
                                      ReadmeBrowserGrid.IsVisible = false;
                              })
                              .DisposeWith(disposables);
        });
    }

    private void TakeWebViewOwnershipForReadme()
    {
        RxApp.MainThreadScheduler.Schedule(() =>
        {
            // TODO: ViewModel.ReadmeBrowser is still typed as Microsoft.Web.WebView2.Wpf.WebView2 in
            // the (not-yet-ported) InstallationVM. Once that VM exposes an Avalonia-compatible
            // WebView2 (Avalonia.Controls.WebView2), the members touched below (Margin/Parent/Width/
            // Height/IsVisible/Source, and ReadmeBrowserGrid.Children.Add) all have direct Avalonia
            // equivalents and this method should compile largely unchanged (Visibility -> IsVisible
            // bool, see BrowserWindow.axaml.cs for the same pattern).
            ViewModel.ReadmeBrowser.Margin = new Thickness(0, 0, 0, 16);
            if (ViewModel.ReadmeBrowser.Parent != null)
            {
                ((Panel)ViewModel.ReadmeBrowser.Parent).Children.Remove(ViewModel.ReadmeBrowser);
            }
            ViewModel.ReadmeBrowser.Width = double.NaN;
            ViewModel.ReadmeBrowser.Height = double.NaN;
            ViewModel.ReadmeBrowser.IsVisible = true;
            if (!string.IsNullOrEmpty(ViewModel?.ModList?.Readme))
                ViewModel.ReadmeBrowser.Source = new Uri(UIUtils.GetHumanReadableReadmeLink(ViewModel.ModList.Readme));
            ReadmeBrowserGrid.Children.Add(ViewModel.ReadmeBrowser);
        });
    }

    private string BuildErrorHtml(string markdown)
    {
        var pipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .UsePipeTables()
            .UseAutoLinks()
            .Build();

        if (string.IsNullOrWhiteSpace(markdown))
            markdown = "_No details provided._";

        var html = Markdig.Markdown.ToHtml(markdown, pipeline);

        var fg = (Application.Current.Resources["ForegroundBrush"] as SolidColorBrush)?.Color ?? Colors.White;
        var bg = (Application.Current.Resources["CardBackgroundBrush"] as SolidColorBrush)?.Color ?? Color.FromRgb(0x1E, 0x1E, 0x1E);
        var accent = (Application.Current.Resources["PrimaryBrush"] as SolidColorBrush)?.Color ?? Color.FromRgb(0x5B, 0x9B, 0xD5);

        static string ToCss(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";

        var css = $@"
:root {{ color-scheme: dark; }}
body {{
  margin: 0; padding: 12px 16px;
  background: {ToCss(bg)};
  color: {ToCss(fg)};
  font-family: Segoe UI, system-ui, -apple-system, Arial, sans-serif;
  font-size: 14px; line-height: 1.6;
}}
a {{ color: {ToCss(accent)}; text-decoration: none; }}
a:hover {{ text-decoration: underline; }}
pre, code {{ font-family: Consolas, 'Cascadia Code', monospace; }}
pre {{ padding: 8px 10px; overflow: auto; background: rgba(255,255,255,0.06); border-radius: 6px; }}
blockquote {{ margin: 0; padding: 8px 12px; border-left: 3px solid {ToCss(accent)}; background: rgba(255,255,255,0.04); border-radius: 4px; }}
table {{ border-collapse: collapse; width: 100%; }}
th, td {{ border: 1px solid rgba(255,255,255,0.12); padding: 6px 8px; }}
ul, ol {{ margin: 0 0 0 20px; }}
h1, h2, h3, h4 {{ margin: 8px 0; }}
";

        return $@"
<!doctype html>
<html>
  <head>
    <meta charset=""utf-8"">
    <base target=""_blank"">
    <style>{css}</style>
  </head>
  <body>{html}</body>
</html>";
    }

    private async Task EnsureErrorWebViewReadyAsync()
    {
        if (ErrorMarkdownView.CoreWebView2 == null)
        {
            await ErrorMarkdownView.EnsureCoreWebView2Async(null);

            ErrorMarkdownView.CoreWebView2.NewWindowRequested += (s, e) =>
            {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = e.Uri,
                        UseShellExecute = true
                    });
                }
                catch { }
                e.Handled = true;
            };

            ErrorMarkdownView.CoreWebView2.NavigationStarting += (s, e) =>
            {
                if (e.Uri.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = e.Uri,
                            UseShellExecute = true
                        });
                    }
                    catch { }
                    e.Cancel = true;
                }
            };
        }
    }

    private async Task RenderErrorMarkdownAsync(string markdown)
    {
        await EnsureErrorWebViewReadyAsync();
        var html = BuildErrorHtml(markdown ?? string.Empty);
        ErrorMarkdownView.NavigateToString(html);
    }
}
