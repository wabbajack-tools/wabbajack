using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.ReactiveUI;
using Avalonia.Threading;
using ReactiveUI;
using Wabbajack.App.Avalonia.Util;
using Wabbajack.App.Avalonia.ViewModels.Gallery;
using Wabbajack.DTOs;
using Wabbajack.DTOs.DownloadStates;
using ModListStatus = Wabbajack.App.Avalonia.ViewModels.Gallery.BaseModListMetadataVM.ModListStatus;

namespace Wabbajack.App.Avalonia.Views.Gallery;

public partial class ModListDetailsView : ReactiveUserControl<ModListDetailsVM>
{
    public ModListDetailsView()
    {
        InitializeComponent();

        // Sizes WPF bound through MathConverter: the image is 16:9 at its own width, and the search box is a
        // third of the archives column.
        ImageBorder.GetObservable(BoundsProperty)
            .Subscribe(b => ImageBorder.Height = Math.Round(b.Width / (16.0 / 9.0)));
        ArchivesGrid.GetObservable(BoundsProperty)
            .Subscribe(b => SearchBox.Width = b.Width / 3);

        ArchivesDataGrid.GetObservable(BoundsProperty)
            .Subscribe(b => SizeColumns(b.Width));

        ArchivesDataGrid.LoadingRow += (_, e) =>
            e.Row.Classes.Set("nexus", e.Row.DataContext is Archive { State: Nexus });
        ArchivesDataGrid.SelectionChanged += OnRowPicked;

        this.WhenActivated(disposables =>
        {
            this.WhenAnyValue(x => x.ViewModel!.MetadataVM.Status)
                .Select(x => x == ModListStatus.NotDownloaded ? "Download & Install"
                    : x == ModListStatus.Downloading ? "Downloading..." : "Install")
                .Subscribe(text => InstallButton.Text = text)
                .DisposeWith(disposables);
        });
    }

    /// <summary>
    ///     WPF's 3* and 4* columns share the grid's width less its 17px scroll bar, the 2px row header, another
    ///     8px and the 60px Size column (measured: 339 and 451 of a 877px grid). Avalonia holds back a few
    ///     pixels more, so its star columns came out narrower and every column after Name started early.
    /// </summary>
    private void SizeColumns(double gridWidth)
    {
        var shared = gridWidth - 17 - 2 - 8 - 60;
        if (shared <= 0) return;
        ArchivesDataGrid.Columns[0].Width = new DataGridLength(Math.Round(shared * 3 / 7));
        ArchivesDataGrid.Columns[1].Width = new DataGridLength(Math.Round(shared * 4 / 7));
    }

    /// <summary>
    ///     A click on a row opens a Nexus archive's mod page, and on any row leaves nothing selected: WPF did
    ///     this when the row took focus, then cleared the selection straight after.
    /// </summary>
    private void OnRowPicked(object? sender, SelectionChangedEventArgs e)
    {
        if (ArchivesDataGrid.SelectedItem is not Archive archive) return;

        if (archive.State is Nexus { LinkUrl: { } link })
            UIUtils.OpenWebsite(link);

        Dispatcher.UIThread.Post(() => ArchivesDataGrid.SelectedItem = null);
    }
}
