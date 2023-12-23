using ReactiveUI;
using System.Windows.Input;
using Wabbajack.Messages;

namespace Wabbajack.Models;

public abstract class ANavigationItem : INavigationItem
{
    public ICommand GoToCommand { get; }
    public virtual NavigateToGlobal.ScreenType Screen { get; }

    public virtual bool MainMenuItem { get; }

    public ANavigationItem()
    {
        GoToCommand = ReactiveCommand.Create(() => NavigateToGlobal.Send(Screen));
    }
}
