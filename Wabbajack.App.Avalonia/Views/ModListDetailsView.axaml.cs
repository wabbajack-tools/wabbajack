using Avalonia.Controls.Primitives;
using System.Reactive.Disposables;
using System.Collections;
using ReactiveUI;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.ReactiveUI;
using Avalonia.Threading;
using System;
using System.Diagnostics;
using Wabbajack.DTOs;
using Wabbajack.DTOs.DownloadStates;
using System.Reactive.Linq;
using System.Reactive.Concurrency;
using ModListStatus = Wabbajack.BaseModListMetadataVM.ModListStatus;
using System.Linq;

namespace Wabbajack;

public partial class ModListDetailsView : ReactiveUserControl<ModListDetailsVM>
{
    public ModListDetailsView()
    {
        InitializeComponent();

        // TODO: WPF's DataGridRow.GotFocus EventSetter (see the ItemContainerStyle TODO in the .axaml)
        // has no Avalonia Style equivalent, so the same "click a row -> open the Nexus link, then clear
        // the row's selection" behavior is wired up manually here via AddHandler on the DataGrid itself
        // (Avalonia's GotFocus is a bubbling routed event, same as WPF's).
        ArchivesDataGrid.AddHandler(InputElement.GotFocusEvent, ArchivesDataGrid_GotFocus, RoutingStrategies.Bubble);

        this.WhenActivated(disposables =>
        {
            this.BindStrict(ViewModel, x => x.Archives, x => x.ArchivesDataGrid.ItemsSource)
                .DisposeWith(disposables);

            this.BindStrict(ViewModel, x => x.Search, x => x.SearchBox.Text)
                .DisposeWith(disposables);

            this.BindCommand(ViewModel, x => x.CloseCommand, x => x.CloseButton)
                .DisposeWith(disposables);

            this.WhenAnyValue(x => x.ArchivesButton.IsChecked)
                .Select(x => !x)
                .BindToStrict(this, x => x.ReadmeButton.IsChecked)
                .DisposeWith(disposables);

            this.WhenAnyValue(x => x.ReadmeButton.IsChecked)
                .Select(x => !x)
                .BindToStrict(this, x => x.ArchivesButton.IsChecked)
                .DisposeWith(disposables);

            this.WhenAnyValue(x => x.ArchivesButton.IsChecked)
                .Select(x => x ?? false)
                .BindToStrict(this, x => x.ArchivesDataGrid.IsVisible)
                .DisposeWith(disposables);

            // NOTE: ViewModel.Browser is Avalonia.Controls.WebView2 (WebView2.Avalonia package),
            // which derives from Shapes.Rectangle/Control/Visual, so IsVisible (bool) and Source
            // (Uri, see below) are available directly - no further porting needed here.
            this.WhenAnyValue(x => x.ReadmeButton.IsChecked)
                .Select(x => x ?? false)
                .BindToStrict(this, x => x.ViewModel.Browser.IsVisible)
                .DisposeWith(disposables);

            this.WhenAnyValue(x => x.ArchivesButton.IsChecked)
                .Select(x => x ?? false)
                .BindToStrict(this, x => x.SearchBox.IsVisible)
                .DisposeWith(disposables);

            this.WhenAnyValue(x => x.ArchivesButton.IsChecked)
                .Select(x => x ?? false)
                .BindToStrict(this, x => x.SearchBoxBackground.IsVisible)
                .DisposeWith(disposables);

            this.WhenAnyValue(x => x.ReadmeButton.IsChecked)
                .Select(x => x ?? false)
                .BindToStrict(this, x => x.OpenReadmeButton.IsVisible)
                .DisposeWith(disposables);

            this.WhenAnyValue(x => x.ViewModel.MetadataVM.Metadata.Links.Readme)
                .Select(readme =>
                {
                    try
                    {
                        var humanReadableReadme = UIUtils.GetHumanReadableReadmeLink(readme);
                        if(Uri.TryCreate(humanReadableReadme, UriKind.Absolute, out var uri)) {
                            return uri;
                        }
                        return default;
                    }
                    catch(Exception)
                    {
                        return new Uri(readme);
                    }
                })
                .BindToStrict(this, x => x.ViewModel.Browser.Source)
                .DisposeWith(disposables);

            this.WhenAnyValue(x => x.ViewModel.MetadataVM.ProgressPercent)
                .BindToStrict(this, x => x.InstallButton.ProgressPercentage)
                .DisposeWith(disposables);

            this.WhenAnyValue(x => x.ViewModel.MetadataVM.Status)
                .Select(x => x == ModListStatus.NotDownloaded ? "Download & Install" : x == ModListStatus.Downloading ? "Downloading..." : "Install")
                .BindToStrict(this, x => x.InstallButton.Text)
                .DisposeWith(disposables);

            this.WhenAnyValue(x => x.ViewModel.MetadataVM.IsBroken)
                .Select(x => !x)
                .BindToStrict(this, x => x.InstallButton.IsVisible)
                .DisposeWith(disposables);

            this.WhenAnyValue(x => x.ViewModel.MetadataVM.IsBroken)
                .BindToStrict(this, x => x.UnavailableDescription.IsVisible)
                .DisposeWith(disposables);

            this.WhenAnyValue(x => x.ViewModel.MetadataVM.ModListTagList)
                // DataGrid/ItemsControl.ItemsSource is the non-generic IEnumerable, so the
                // HashSet<ModListTag> source needs an explicit cast/selector for BindToStrict's
                // matching-type constraint (same pattern as CompilerHomeView.axaml.cs).
                .Select(x => (IEnumerable)x)
                .BindToStrict(this, v => v.TagsControl.ItemsSource)
                .DisposeWith(disposables);

            this.BindCommand(ViewModel, x => x.OpenReadmeCommand, x => x.OpenReadmeButton)
                .DisposeWith(disposables);

            this.BindCommand(ViewModel, x => x.OpenWebsiteCommand, x => x.WebsiteButton)
                .DisposeWith(disposables);

            this.BindCommand(ViewModel, x => x.OpenDiscordCommand, x => x.DiscordButton)
                .DisposeWith(disposables);

            this.BindCommand(ViewModel, x => x.MetadataVM.InstallCommand, x => x.InstallButton)
                .DisposeWith(disposables);

            RxApp.MainThreadScheduler.Schedule(() =>
            {
                if (ViewModel.Browser.Parent != null)
                {
                    ((Panel)ViewModel.Browser.Parent).Children.Remove(ViewModel.Browser);
                }
                MainContentGrid.Children.Add(ViewModel.Browser);
            });

        });
    }

    private void ArchivesDataGrid_GotFocus(object sender, GotFocusEventArgs e)
    {
        // TODO: WPF located the clicked archive via ((DataGridCellsPresenter)e.Source).Item. Avalonia's
        // DataGrid does not expose an equivalent DataGridCellsPresenter type in the same way; this reads
        // the row/cell's DataContext instead, which should carry the same Archive item. Verify against
        // the installed Avalonia.Controls.DataGrid version once the project builds.
        if (e.Source is not Control control) return;
        if (control.DataContext is not Archive archive) return;

        if (archive.State is Nexus nexusState)
        {
            Process.Start(new ProcessStartInfo(nexusState.LinkUrl.ToString()) { UseShellExecute = true });
        }

        RxApp.MainThreadScheduler.Schedule(() =>
        {
            // TODO: WPF used FocusManager.SetFocusedElement(..., null) + Keyboard.ClearFocus() to drop
            // keyboard focus off the presenter. Avalonia's focus-clearing API differs (TopLevel-scoped
            // IFocusManager); this needs verifying against the Avalonia version in use.
            TopLevel.GetTopLevel(this)?.FocusManager?.ClearFocus();
            ArchivesDataGrid.SelectedItem = null;
        });
    }
}
