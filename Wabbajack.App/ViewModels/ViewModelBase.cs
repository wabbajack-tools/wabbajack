using ReactiveUI;
using ReactiveUI.Validation.Helpers;

namespace Wabbajack.App.ViewModels;

public class ViewModelBase : ReactiveValidationObject, IActivatableViewModel
{
    public ViewModelActivator Activator { get; protected set; }
}