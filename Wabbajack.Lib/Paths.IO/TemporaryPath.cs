using System;
using System.Threading.Tasks;

namespace Wabbajack.Paths.IO;

public struct TemporaryPath : IDisposable, IAsyncDisposable
{
    public readonly AbsolutePath Path { get; init; }

    public TemporaryPath(AbsolutePath path)
    {
        Path = path;
    }

    public void Dispose()
    {
        Path.Delete();
    }

    public override string ToString()
    {
        return Path.ToString();
    }

    public static implicit operator AbsolutePath(TemporaryPath tp)
    {
        return tp.Path;
    }

    public ValueTask DisposeAsync()
    {
        Path.Delete();
        return ValueTask.CompletedTask;
    }
}