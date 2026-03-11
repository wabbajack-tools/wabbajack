using System.Reactive.Disposables;
using ReactiveUI;

namespace Wabbajack;

public partial class ConfirmationDialogView : ReactiveUserControl<ConfirmationDialogVM>
{
    public ConfirmationDialogView()
    {
        InitializeComponent();
        this.WhenActivated(disposables =>
        {
            this.WhenAnyValue(v => v.ViewModel.Title)
                .BindToStrict(this, v => v.TitleText.Text)
                .DisposeWith(disposables);

            this.WhenAnyValue(v => v.ViewModel.Message)
                .BindToStrict(this, v => v.MessageText.Text)
                .DisposeWith(disposables);

            this.BindCommand(ViewModel, vm => vm.CancelCommand, v => v.CancelBtn)
                .DisposeWith(disposables);

            this.BindCommand(ViewModel, vm => vm.ConfirmCommand, v => v.ConfirmBtn)
                .DisposeWith(disposables);
        });
    }
}
