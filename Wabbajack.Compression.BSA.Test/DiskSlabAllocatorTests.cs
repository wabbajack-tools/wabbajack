using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Wabbajack.Paths.IO;
using Xunit;

namespace Wabbajack.Compression.BSA.Test;

public class DiskSlabAllocatorTests
{
    [Fact]
    public async Task Allocate_BelowMaxMemorySize_ReturnsMemoryStream()
    {
        var tempFolder = KnownFolders.EntryPoint.Combine(Guid.NewGuid().ToString());
        using var manager = new TemporaryFileManager(tempFolder);
        var allocator = new DiskSlabAllocator(manager, maxMemorySize: 1024 * 1024);

        var stream = allocator.Allocate(100);
        Assert.IsType<MemoryStream>(stream);

        await allocator.DisposeAsync();
    }

    [Fact]
    public async Task Allocate_ExceedingMaxMemorySize_ReturnsFileStream()
    {
        var tempFolder = KnownFolders.EntryPoint.Combine(Guid.NewGuid().ToString());
        using var manager = new TemporaryFileManager(tempFolder);
        var allocator = new DiskSlabAllocator(manager, maxMemorySize: 100);

        // First allocation fits in memory
        var memStream = allocator.Allocate(50);
        Assert.IsType<MemoryStream>(memStream);

        // Second allocation exceeds threshold, should be on disk
        var diskStream = allocator.Allocate(100);
        Assert.IsType<FileStream>(diskStream);

        var testData = new byte[] { 1, 2, 3, 4, 5 };
        await diskStream.WriteAsync(testData);
        diskStream.Position = 0;

        var readBuffer = new byte[5];
        await diskStream.ReadExactlyAsync(readBuffer);
        Assert.Equal(testData, readBuffer);

        await allocator.DisposeAsync();
    }

    [Fact]
    public async Task Allocate_ConcurrentAllocations_HandlesHeavyDiskLoad()
    {
        var tempFolder = KnownFolders.EntryPoint.Combine(Guid.NewGuid().ToString());
        using var manager = new TemporaryFileManager(tempFolder);
        // Force all allocations to disk
        var allocator = new DiskSlabAllocator(manager, maxMemorySize: 0);

        var tasks = Enumerable.Range(0, 50).Select(i => Task.Run(async () =>
        {
            var stream = allocator.Allocate(1024);
            Assert.IsType<FileStream>(stream);

            var data = Enumerable.Range(0, 256).Select(b => (byte) (b ^ i)).ToArray();
            await stream.WriteAsync(data);
            stream.Position = 0;

            var read = new byte[256];
            await stream.ReadExactlyAsync(read);
            Assert.Equal(data, read);
        })).ToArray();

        await Task.WhenAll(tasks);
        await allocator.DisposeAsync();
    }

    [Fact]
    public async Task DisposeAsync_CleansUpStreamsAndFiles()
    {
        var tempFolder = KnownFolders.EntryPoint.Combine(Guid.NewGuid().ToString());
        using var manager = new TemporaryFileManager(tempFolder, deleteOnDispose: false);
        var allocator = new DiskSlabAllocator(manager, maxMemorySize: 100);

        var memStream = allocator.Allocate(50);
        var diskStream = allocator.Allocate(200);

        await allocator.DisposeAsync();

        // Stream operations should throw ObjectDisposedException
        Assert.Throws<ObjectDisposedException>(() => memStream.Write(new byte[] { 1 }));
        Assert.Throws<ObjectDisposedException>(() => diskStream.Write(new byte[] { 1 }));

        // Any temporary files in tempFolder should have been deleted on allocator dispose
        var remainingFiles = tempFolder.EnumerateFiles().ToList();
        Assert.Empty(remainingFiles);
    }
}
