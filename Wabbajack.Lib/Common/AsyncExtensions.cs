using System.Threading.Tasks;

namespace Wabbajack.Common;

public static class AsyncExtensions
{
    public static void FireAndForget(this Task task)
    {
    }

    public static void FireAndForget<T>(this Task<T> task)
    {
    }
}