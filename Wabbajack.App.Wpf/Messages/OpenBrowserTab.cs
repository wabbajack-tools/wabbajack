namespace Wabbajack.Messages;

public class OpenBrowserTab
{
    public BrowserWindowViewModel ViewModel { get; set; }

    public OpenBrowserTab(BrowserWindowViewModel viewModel)
    {
        ViewModel = viewModel;
    }
}