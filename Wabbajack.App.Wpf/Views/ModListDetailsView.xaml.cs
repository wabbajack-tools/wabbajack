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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Web.WebView2.Wpf;
using System.Windows.Controls;
using Wabbajack.RateLimiter;
using ModListStatus = Wabbajack.BaseModListMetadataVM.ModListStatus;

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

            this.BindCommand(ViewModel, x => x.BackCommand, x => x.BackButton)
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
                        if(readme.Contains("raw.githubusercontent.com") && readme.EndsWith(".md"))
                        {
                            var urlParts = readme.Split('/');
                            var user = urlParts[3];
                            var repository = urlParts[4];
                            var branch = urlParts[5];
                            var fileName = urlParts[6];
                            return new Uri($"https://github.com/{user}/{repository}/blob/{branch}/{fileName}#{repository}");
                        }
                        return new Uri(readme);
                    }
                    catch (Exception)
                    {
                        return default;
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

