using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Wabbajack.Paths.IO.Test;

/// <summary>
///     <see cref="AbsolutePathExtensions.MoveToAsync" /> against the real file system. The permanent cases use
///     a cancellation token with seconds of slack and assert that what comes back is the file system's own
///     error rather than a cancellation: a move that sat in a retry loop would be cancelled out of it instead.
/// </summary>
public class MoveToAsyncTests : IDisposable
{
    private readonly AbsolutePath _root = KnownFolders.EntryPoint.Combine("movetests-" + Guid.NewGuid());

    public MoveToAsyncTests()
    {
        _root.CreateDirectory();
    }

    public void Dispose()
    {
        _root.DeleteDirectory();
    }

    private AbsolutePath NewFile(string name, string content = "data")
    {
        var path = _root.Combine(name);
        path.WriteAllText(content);
        return path;
    }

    /// <summary>Long enough that nothing correct trips it, short enough that a retry loop would.</summary>
    private static CancellationTokenSource Guard()
    {
        return new CancellationTokenSource(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task MovesAFile()
    {
        var src = NewFile("src.bin", "hello");
        var dest = _root.Combine("dest.bin");

        await src.MoveToAsync(dest, false, CancellationToken.None);

        Assert.False(src.FileExists());
        Assert.Equal("hello", dest.ReadAllText());
    }

    [Fact]
    public async Task OverwritesAReadOnlyDestination()
    {
        var src = NewFile("src.bin", "new");
        var dest = NewFile("dest.bin", "old");
        new FileInfo(dest.ToString()).IsReadOnly = true;

        await src.MoveToAsync(dest, true, CancellationToken.None);

        Assert.Equal("new", dest.ReadAllText());
    }

    [Fact]
    public async Task ADestinationThatIsADirectoryFailsRatherThanWaiting()
    {
        var src = NewFile("src.bin");
        var dest = _root.Combine("dest.bin");
        dest.CreateDirectory();

        using var cts = Guard();
        var ex = await Assert.ThrowsAnyAsync<Exception>(async () => await src.MoveToAsync(dest, true, cts.Token));

        Assert.False(ex is OperationCanceledException);
        Assert.True(src.FileExists());
    }

    [Fact]
    public async Task AMissingSourceFailsRatherThanWaiting()
    {
        var src = _root.Combine("nothing.bin");
        var dest = _root.Combine("dest.bin");

        using var cts = Guard();
        var ex = await Assert.ThrowsAnyAsync<Exception>(async () => await src.MoveToAsync(dest, true, cts.Token));

        Assert.False(ex is OperationCanceledException);
        Assert.IsAssignableFrom<IOException>(ex);
    }

    [Fact]
    public async Task AnExistingDestinationWithoutOverwriteFailsRatherThanWaiting()
    {
        var src = NewFile("src.bin", "new");
        var dest = NewFile("dest.bin", "old");

        using var cts = Guard();
        var ex = await Assert.ThrowsAnyAsync<Exception>(async () => await src.MoveToAsync(dest, false, cts.Token));

        Assert.False(ex is OperationCanceledException);
        Assert.IsAssignableFrom<IOException>(ex);
        Assert.Equal("old", dest.ReadAllText());
        Assert.True(src.FileExists());
    }

    [Fact]
    public async Task APathTheVolumeCannotHoldFailsRatherThanWaiting()
    {
        var src = NewFile("src.bin");
        var dest = _root.Combine(new string('a', 400) + ".bin");

        using var cts = Guard();
        var ex = await Assert.ThrowsAnyAsync<Exception>(async () => await src.MoveToAsync(dest, true, cts.Token));

        Assert.False(ex is OperationCanceledException);
        Assert.True(src.FileExists());
    }

    /// <summary>
    ///     The case the retry loop exists for. <c>FileShare.None</c> is advisory on Unix, so only Windows can
    ///     arrange it.
    /// </summary>
    [Fact]
    public async Task AFileHeldOpenIsMovedOnceTheHandleCloses()
    {
        if (!OperatingSystem.IsWindows()) return;

        var src = NewFile("held.bin", "hello");
        var dest = _root.Combine("dest.bin");

        var handle = File.Open(src.ToString(), FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        var move = Task.Run(async () => await src.MoveToAsync(dest, true, CancellationToken.None));

        await Task.Delay(200);
        handle.Dispose();

        await move;
        Assert.Equal("hello", dest.ReadAllText());
    }

    [Fact]
    public async Task CancellationComesOutOfAMoveThatIsWaiting()
    {
        if (!OperatingSystem.IsWindows()) return;

        var src = NewFile("held.bin");
        var dest = _root.Combine("dest.bin");

        using var handle = File.Open(src.ToString(), FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await src.MoveToAsync(dest, true, cts.Token));
    }
}
