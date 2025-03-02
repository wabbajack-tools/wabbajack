using System.Reactive.Disposables;
using ReactiveUI;
using ReactiveMarbles.ObservableEvents;
using System.Windows;
using System.Windows.Controls.Primitives;
using System;
using System.Windows.Input;
using System.Diagnostics;
using Wabbajack.DTOs;
using Wabbajack.DTOs.DownloadStates;
using System.Reactive.Linq;
using System.Reactive.Concurrency;
using System.Windows.Controls;
using ModListStatus = Wabbajack.BaseModListMetadataVM.ModListStatus;
using System.Linq;

namespace Wabbajack;

public partial class ModListDetailsView
{
    public ModListDetailsView()
    {
        InitializeComponent();
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
                .Select(x => x ?? false ? Visibility.Visible : Visibility.Hidden)
                .BindToStrict(this, x => x.ArchivesDataGrid.Visibility)
                .DisposeWith(disposables);

            this.WhenAnyValue(x => x.ReadmeButton.IsChecked)
                .Select(x => x ?? false ? Visibility.Visible : Visibility.Hidden)
                .BindToStrict(this, x => x.ViewModel.Browser.Visibility)
                .DisposeWith(disposables);

            this.WhenAnyValue(x => x.ArchivesButton.IsChecked)
                .Select(x => x ?? false ? Visibility.Visible : Visibility.Hidden)
                .BindToStrict(this, x => x.SearchBox.Visibility)
                .DisposeWith(disposables);

            this.WhenAnyValue(x => x.ArchivesButton.IsChecked)
                .Select(x => x ?? false ? Visibility.Visible : Visibility.Hidden)
                .BindToStrict(this, x => x.SearchBoxBackground.Visibility)
                .DisposeWith(disposables);

            this.WhenAnyValue(x => x.ReadmeButton.IsChecked)
                .Select(x => x ?? false ? Visibility.Visible : Visibility.Hidden)
                .BindToStrict(this, x => x.OpenReadmeButton.Visibility)
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
                .Select(x => x ? Visibility.Collapsed : Visibility.Visible)
                .BindToStrict(this, x => x.InstallButton.Visibility)
                .DisposeWith(disposables);

            this.WhenAnyValue(x => x.ViewModel.MetadataVM.IsBroken)
                .Select(x => x ? Visibility.Visible : Visibility.Collapsed)
                .BindToStrict(this, x => x.UnavailableDescription.Visibility)
                .DisposeWith(disposables);

            this.WhenAnyValue(x => x.ViewModel.MetadataVM.ModListTagList)
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

    private void DataGridRow_GotFocus(object sender, RoutedEventArgs e)
    {
        var presenter = ((DataGridCellsPresenter)e.Source);
        var archive = (Archive)presenter.Item;
        if(archive.State is Nexus nexusState)
        {
            Process.Start(new ProcessStartInfo(nexusState.LinkUrl.ToString()) { UseShellExecute = true });
        }

        RxApp.MainThreadScheduler.Schedule(0, (_, _) =>
        {
            FocusManager.SetFocusedElement(FocusManager.GetFocusScope(presenter), null);
            Keyboard.ClearFocus();
            ArchivesDataGrid.SelectedItem = null;
            ArchivesDataGrid.CurrentItem = null;
            return Disposable.Empty;
        });
    }
}

