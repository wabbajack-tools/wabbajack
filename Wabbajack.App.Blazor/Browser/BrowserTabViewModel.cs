using ReactiveUI.Fody.Helpers;

namespace Wabbajack.App.Blazor.Browser;

public class BrowserTabViewModel : ViewModel
{
    [Reactive]
    public string HeaderText { get; set; }
    
}
