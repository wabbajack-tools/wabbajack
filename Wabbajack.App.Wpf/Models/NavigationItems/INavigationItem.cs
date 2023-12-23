using System.Windows.Input;
using Wabbajack.Messages;

namespace Wabbajack.Models;

public interface INavigationItem
{
    public ICommand GoToCommand { get; }
    public NavigateToGlobal.ScreenType Screen { get; }
    public bool MainMenuItem { get; }
}
