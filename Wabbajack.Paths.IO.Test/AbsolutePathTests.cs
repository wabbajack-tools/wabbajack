using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FsCheck.Xunit;
using Xunit;

namespace Wabbajack.Paths.IO.Test;

public class AbsolutePathTests
{
    private AbsolutePath GetTempFile()
    {
        return KnownFolders.EntryPoint.Combine(Guid.NewGuid().ToString());
    }

    [Fact]
    public async Task DeleteShouldSucceedWhenFileIsLockedAndThenFreed()
    {
        var file = GetTempFile();
        file.WriteAllText("Test");

        // Lock the file
        using var fs = File.Open(file.ToString(), FileMode.Open, FileAccess.Read, FileShare.None);

        // Try to delete in background, which should block and then retry
        var deleteTask = Task.Run(() => file.Delete());

        // Wait a bit to ensure it enters the retry loop
        await Task.Delay(2000);

        // Unlock the file
        fs.Dispose();

        // Ensure deletion completes
        await deleteTask;

        Assert.False(file.FileExists());
    }

    [Property(StartSize = 1024)]
    public void CanReadAndWriteFiles(byte[] data)
    {
        var file = GetTempFile();
        file.WriteAllBytes(data);

        Assert.Equal(data.Length, file.Size());

        Assert.Equal(data, file.ReadAllBytes());
        file.Delete();

        file.WriteAllText("Test");
        Assert.Equal("Test", file.ReadAllText());
    }

    [Fact]
    public async Task CanReadAndWriteFilesAsync()
    {
        var data = "This is a test";
        var file = GetTempFile();
        await file.WriteAllTextAsync(data);

        Assert.Equal(data.Length, file.Size());

        Assert.Equal(data, await file.ReadAllTextAsync());
        file.Delete();
    }

    [Property(EndSize = 100)] // OSX has a max length of 1024
    public void LongPathsAreSupported(uint depth)
    {
        var basePath = KnownFolders.EntryPoint.Combine("deep_paths");
        basePath.DeleteDirectory();

        var path = Enumerable.Range(1, (int) depth + 1).Aggregate(basePath, (path, i) => path.Combine($"path_{i}"));
        path.Parent.CreateDirectory();
        path.WriteAllText("test");

        basePath.DeleteDirectory();
    }
}