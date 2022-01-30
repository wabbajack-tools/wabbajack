using Wabbajack.App.Blazor.Browser;

namespace Wabbajack.App.Blazor.Messages;

public class OpenBrowserTab
{
    public BrowserTabViewModel ViewModel { get; set; }

    public OpenBrowserTab(BrowserTabViewModel viewModel)
    {
        ViewModel = viewModel;
    }
}
