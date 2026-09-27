using Avalonia.ReactiveUI;
using Wabbajack.App.Avalonia.ViewModels;

namespace Wabbajack.App.Avalonia.Views;

public partial class ConfirmationDialogView : ReactiveUserControl<ConfirmationDialogVM>
{
    public ConfirmationDialogView() => InitializeComponent();
}
