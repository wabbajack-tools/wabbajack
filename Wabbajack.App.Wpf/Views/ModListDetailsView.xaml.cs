using System.Reactive.Disposables;
using ReactiveUI;

namespace Wabbajack;

public partial class ModListDetailsView
{

    public ModListDetailsView()
    {
        InitializeComponent();
        this.WhenActivated(disposables =>
        {
            this.ArchiveGrid.ItemsSource = this.ViewModel.Archives;
            
            this.BindStrict(ViewModel, x => x.SearchString, x => x.SearchBox.Text)
                .DisposeWith(disposables);

            this.BindCommand(ViewModel, x => x.BackCommand, x => x.BackButton)
                .DisposeWith(disposables);

            ViewModel.WhenAnyValue(vm => vm.MetadataVM.Image)
                     .BindToStrict(this, v => v.DetailImage.Image)
                     .DisposeWith(disposables);
        });
    }
}

