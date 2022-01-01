using System.Threading.Tasks;

namespace Wabbajack.Networking.Http.Interfaces;

/// <summary>
///     Interface for services that need a auth token of some sort
/// </summary>
/// <typeparam name="T"></typeparam>
public interface ITokenProvider<T>
{
    public ValueTask<T?> Get();

    public ValueTask SetToken(T val);
    public ValueTask<bool> Delete();

    public bool HaveToken();
}