using ReactiveUI;
using Wabbajack.App.Messages;
using Wabbajack.App.ViewModels;

namespace Wabbajack.App.Screens;

public class CompilerConfigurationViewModel : ViewModelBase, IReceiverMarker
{
    public CompilerConfigurationViewModel()
    {
        Activator = new ViewModelActivator();
    }
    
}