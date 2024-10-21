using System.Reactive.Disposables;
using ReactiveUI;
using ReactiveMarbles.ObservableEvents;
using System.Windows;
using System.Windows.Controls.Primitives;
using System;

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

    private void ColumnHeaderClicked(object sender, RoutedEventArgs e)
    {
        int i = 0;
    }
}

