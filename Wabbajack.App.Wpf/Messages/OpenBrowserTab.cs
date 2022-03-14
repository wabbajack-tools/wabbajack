namespace Wabbajack.Messages;

public class OpenBrowserTab
{
    public BrowserTabViewModel ViewModel { get; set; }

    public OpenBrowserTab(BrowserTabViewModel viewModel)
    {
        ViewModel = viewModel;
    }
}