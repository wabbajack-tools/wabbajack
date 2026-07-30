using System.Threading.Tasks;
using System.Windows.Input;
using ReactiveUI;

namespace Wabbajack;

public class ConfirmationDialogVM : ViewModel, IClosableVM
{
    public string Title { get; }
    public string Message { get; }
    private readonly TaskCompletionSource<bool> _tcs = new();
    public Task<bool> Result => _tcs.Task;

    public ICommand ConfirmCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand CloseCommand { get; }

    public ConfirmationDialogVM(string title, string message)
    {
        Title = title;
        Message = message;
        ConfirmCommand = ReactiveCommand.Create(() => { _tcs.TrySetResult(true); });
        CancelCommand = ReactiveCommand.Create(() => { _tcs.TrySetResult(false); });
        CloseCommand = CancelCommand;
    }
}
