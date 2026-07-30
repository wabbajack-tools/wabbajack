using System.Windows.Input;

namespace Wabbajack;

public interface ICanGetHelpVM
{
    public ICommand GetHelpCommand { get; }
}
