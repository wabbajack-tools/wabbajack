using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Wabbajack.Common;
using Wabbajack.FileExtractor.ExtractedFiles;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Xunit;

namespace Wabbajack.FileExtractor.Test.Offline;

/// <summary>
///     How an extracted file reaches its destination: moved when it can be, copied when it cannot, and hashed
///     either way.
/// </summary>
public class ExtractedFileTests : IAsyncLifetime
{
    private static readonly byte[] Content = "extracted content"u8.ToArray();

    private readonly TemporaryFileManager _manager;
    private TemporaryPath _folder;

    public ExtractedFileTests(TemporaryFileManager manager)
    {
        _manager = manager;
    }

    public Task InitializeAsync()
    {
        _folder = _manager.CreateFolder();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _folder.DisposeAsync();
    }

    /// <summary>A stream <see cref="MemoryStreamFactory" /> can read the buffer of.</summary>
    public static MemoryStream Visible(byte[] bytes) => new(bytes, 0, bytes.Length, false, true);

    [Fact]
    public async Task NativeFile_ThatCanMove_IsMoved()
    {
        var source = _folder.Path.Combine("source.bin");
        await source.WriteAllBytesAsync(Content);
        var dest = _folder.Path.Combine("dest.bin");

        var hash = await new ExtractedNativeFile(source).MoveHashedAsync(dest, CancellationToken.None);

        Assert.False(source.FileExists());
        Assert.Equal(Content, await dest.ReadAllBytesAsync());
        Assert.Equal(await Content.Hash(), hash);
    }

    [Fact]
    public async Task NativeFile_ThatCannotMove_IsCopied()
    {
        var source = _folder.Path.Combine("source.bin");
        await source.WriteAllBytesAsync(Content);
        var dest = _folder.Path.Combine("dest.bin");

        var hash = await new ExtractedNativeFile(source) {CanMove = false}.MoveHashedAsync(dest, CancellationToken.None);

        Assert.True(source.FileExists());
        Assert.Equal(Content, await dest.ReadAllBytesAsync());
        Assert.Equal(await Content.Hash(), hash);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task MemoryFile_IsWrittenOutAndHashed(bool canMove)
    {
        var factory = new MemoryStreamFactory(Visible(Content), "entry.bin".ToRelativePath(), DateTime.UtcNow);
        var dest = _folder.Path.Combine("dest.bin");

        var hash = await new ExtractedMemoryFile(factory) {CanMove = canMove}.MoveHashedAsync(dest, CancellationToken.None);

        Assert.Equal(Content, await dest.ReadAllBytesAsync());
        Assert.Equal(await Content.Hash(), hash);
    }
}
