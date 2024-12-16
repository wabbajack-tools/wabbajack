using System.Reactive.Disposables;
using ReactiveUI;

namespace Wabbajack.ViewModels.Controls;

public partial class RemovableItemView : ReactiveUserControl<RemovableItemViewModel>
{
    public RemovableItemView()
    {
        InitializeComponent();

        this.WhenActivated(disposables =>
        {
            this.BindStrict(ViewModel, vm => vm.Text, view => view.DisplayText.Text)
                .DisposeWith(disposables);
            

        });

        DeleteButton.Command = ReactiveCommand.Create(() => ViewModel.RemoveFn());
    }
}