using System.Windows.Input;

namespace Wabbajack;

public interface IHasInfoVM
{
    public ICommand InfoCommand { get; }
}
