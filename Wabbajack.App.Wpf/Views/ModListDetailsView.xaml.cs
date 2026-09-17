using System.Reactive.Disposables;
using ReactiveUI;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
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

            this.BindCommand(ViewModel, x => x.OpenReadmeCommand, x => x.ReadmeButton)
                .DisposeWith(disposables);

            this.BindCommand(ViewModel, x => x.OpenWebsiteCommand, x => x.WebsiteButton)
                .DisposeWith(disposables);

            this.BindCommand(ViewModel, x => x.OpenDiscordCommand, x => x.DiscordButton)
                .DisposeWith(disposables);

            this.BindCommand(ViewModel, x => x.MetadataVM.InstallCommand, x => x.InstallButton)
                .DisposeWith(disposables);
        });
    }

    private void DataGridRow_GotFocus(object sender, RoutedEventArgs e)
    {
        var presenter = ((DataGridCellsPresenter)e.Source);
        var archive = (Archive)presenter.Item;
        if (archive.State is Nexus nexusState && nexusState.LinkUrl is { } link)
        {
            UIUtils.OpenWebsite(link);
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

