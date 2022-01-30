using ReactiveUI;

namespace Wabbajack.App.Blazor.Browser;

public class ViewModel : ReactiveObject, IActivatableViewModel
{
    public ViewModelActivator Activator { get; } = new();
}
