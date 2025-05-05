using ReactiveUI;

namespace Wabbajack.Messages;

public class ShowBrowserWindow
{
    public BrowserWindowViewModel ViewModel { get; set; }
    public bool OpenExistingOperation { get; set; } = false;
    public ShowBrowserWindow(BrowserWindowViewModel viewModel, bool openExistingOperation = false)
    {
        ViewModel = viewModel;
        OpenExistingOperation = openExistingOperation;
    }
    public static void Send(BrowserWindowViewModel viewModel, bool openExistingOperation = false)
    {
        MessageBus.Current.SendMessage(new ShowBrowserWindow(viewModel, openExistingOperation));
    }
}