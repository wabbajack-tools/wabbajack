using System.Reactive;
using Avalonia;
using Avalonia.Input;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Wabbajack.App.Utilities;
using Wabbajack.App.ViewModels;

namespace Wabbajack.App.Screens;

public class LogScreenViewModel : ViewModelBase, IActivatableViewModel
{
    private readonly LoggerProvider _provider;
    public LogScreenViewModel(LoggerProvider provider)
    {
        _provider = provider;
        Activator = new ViewModelActivator();

    }
    
}