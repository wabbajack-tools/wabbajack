using Microsoft.Extensions.Logging;
using ReactiveUI;
using Wabbajack.App.Messages;

namespace Wabbajack.App.ViewModels
{
    public class SettingsViewModel : ViewModelBase, IReceiverMarker
    {
        private readonly ILogger<SettingsViewModel> _logger;

        public SettingsViewModel(ILogger<SettingsViewModel> logger)
        {
            _logger = logger;
            Activator = new ViewModelActivator();
        }
        
    }
}