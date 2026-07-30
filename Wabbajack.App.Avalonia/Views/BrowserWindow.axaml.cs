using Avalonia.ReactiveUI;

namespace Wabbajack.Views;

// Stub during the port; the WebView2.Avalonia host is wired in Phase 4 (browser integration).
public partial class BrowserWindow : ReactiveUserControl<BrowserWindowViewModel>
{
    public BrowserWindow()
    {
        InitializeComponent();
    }
}
