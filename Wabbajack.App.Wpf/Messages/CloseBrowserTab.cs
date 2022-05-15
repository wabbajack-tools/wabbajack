namespace Wabbajack.Messages;

public class CloseBrowserTab
{
    public BrowserTabViewModel ViewModel { get; init; }

    public CloseBrowserTab(BrowserTabViewModel viewModel)
    {
        ViewModel = viewModel;
    }
}