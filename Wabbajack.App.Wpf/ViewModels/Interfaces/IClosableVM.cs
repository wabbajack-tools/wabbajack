using System.Windows.Input;

namespace Wabbajack;

public interface IClosableVM
{
    public ICommand CloseCommand { get; }
}
