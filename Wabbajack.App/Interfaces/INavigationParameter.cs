using System.Threading.Tasks;

namespace Wabbajack.App.Interfaces;

public interface INavigationParameter<T>
{
    public Task NavigatedTo(T param);
}