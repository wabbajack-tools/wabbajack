using ReactiveUI;

namespace Wabbajack.Messages;

public class ShowBrowserWindow
{
    public BrowserWindowViewModel ViewModel { get; set; }
    public ShowBrowserWindow(BrowserWindowViewModel viewModel)
    {
        ViewModel = viewModel;
    }
    public static void Send(BrowserWindowViewModel viewModel)
    {
        MessageBus.Current.SendMessage(new ShowBrowserWindow(viewModel));
    }
}