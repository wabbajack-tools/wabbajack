using ReactiveUI.SourceGenerators;

namespace Wabbajack.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    [Reactive] public partial string Greeting { get; set; }

    public MainWindowViewModel()
    {
        Greeting = "Wabbajack — Avalonia head (scaffold)";
    }
}
