using Avalonia.Controls.Mixins;
using Avalonia.ReactiveUI;
using ReactiveUI;
using Wabbajack.App.ViewModels;

namespace Wabbajack.App.Controls;

public partial class RemovableListItem : ReactiveUserControl<RemovableItemViewModel>, IActivatableView
{
    public RemovableListItem()
    {
        InitializeComponent();
        this.WhenActivated(disposables =>
        {
            this.OneWayBind(ViewModel, vm => vm.Text, view => view.Text.Text)
                .DisposeWith(disposables);

            this.BindCommand(ViewModel, vm => vm.DeleteCommand, view => view.DeleteButton)
                .DisposeWith(disposables);

        });
    }
    
}