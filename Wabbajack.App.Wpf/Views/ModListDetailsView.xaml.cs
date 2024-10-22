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

namespace Wabbajack;

public partial class ModListDetailsView
{

    public ModListDetailsView()
    {
        InitializeComponent();
        this.WhenActivated(disposables =>
        {
            this.BindStrict(ViewModel, x => x.Archives, x => x.ArchiveGrid.ItemsSource)
                .DisposeWith(disposables);

            this.BindStrict(ViewModel, x => x.Search, x => x.SearchBox.Text)
                .DisposeWith(disposables);

            this.BindCommand(ViewModel, x => x.BackCommand, x => x.BackButton)
                .DisposeWith(disposables);
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
            ArchiveGrid.SelectedItem = null;
            ArchiveGrid.CurrentItem = null;
            return Disposable.Empty;
        });
    }
}

