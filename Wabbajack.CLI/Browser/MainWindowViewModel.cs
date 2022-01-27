using ReactiveUI.Fody.Helpers;

namespace Wabbajack.CLI.Browser
{
    public class MainWindowViewModel : ViewModelBase
    {
        public MainWindowViewModel()
        {

        }

        [Reactive]
        public string Instructions { get; set; }
        
    }
}
