namespace Wabbajack.Messages;

public class CloseBrowserTab
{
    public BrowserWindowViewModel ViewModel { get; init; }

    public CloseBrowserTab(BrowserWindowViewModel viewModel)
    {
        ViewModel = viewModel;
    }
}