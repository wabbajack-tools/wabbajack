using ReactiveUI.Fody.Helpers;

namespace Wabbajack.Networking.Browser.ViewModels
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
