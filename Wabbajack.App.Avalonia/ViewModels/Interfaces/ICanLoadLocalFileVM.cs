using System.Windows.Input;

namespace Wabbajack;

public interface ICanLoadLocalFileVM
{
    public ICommand LoadLocalFileCommand { get; }
}
