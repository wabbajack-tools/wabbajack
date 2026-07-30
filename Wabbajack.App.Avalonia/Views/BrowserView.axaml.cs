using Avalonia.ReactiveUI;

namespace Wabbajack.Views;

public partial class BrowserView : ReactiveUserControl<BrowserWindowViewModel>
{
    public BrowserView()
    {
        InitializeComponent();
    }
}
