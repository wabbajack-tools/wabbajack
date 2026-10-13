using System.Threading.Tasks;
using System.Windows.Input;
using ReactiveUI;
using Wabbajack.App.Avalonia.Interfaces;

namespace Wabbajack.App.Avalonia.ViewModels;

/// <summary>
///     A yes-or-no question shown as a floating pane, which the installer asks through
///     <see cref="MainWindowVM.ShowConfirmationDialog" />. Closing the pane is a no.
/// </summary>
public class ConfirmationDialogVM : ViewModel, IClosableVM
{
    private readonly TaskCompletionSource<bool> _tcs = new();

    public ConfirmationDialogVM(string title, string message)
    {
        Title = title;
        Message = message;
        ConfirmCommand = ReactiveCommand.Create(() => { _tcs.TrySetResult(true); });
        CancelCommand = ReactiveCommand.Create(() => { _tcs.TrySetResult(false); });
        CloseCommand = CancelCommand;
    }

    public string Title { get; }
    public string Message { get; }
    public Task<bool> Result => _tcs.Task;

    public ICommand ConfirmCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand CloseCommand { get; }
}
