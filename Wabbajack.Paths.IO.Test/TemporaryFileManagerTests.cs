using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Wabbajack.Paths.IO.Test;

public class TemporaryFileManagerTests
{
    [Fact]
    public void CreateFile_GeneratesUniquePaths()
    {
        var tempFolder = KnownFolders.EntryPoint.Combine(Guid.NewGuid().ToString());
        using var manager = new TemporaryFileManager(tempFolder);

        var paths = Enumerable.Range(0, 100)
            .Select(_ => manager.CreateFile(new Extension(".tmp")))
            .ToList();

        var pathStrings = paths.Select(p => p.Path.ToString()).ToHashSet();
        Assert.Equal(100, pathStrings.Count);

        foreach (var p in paths)
        {
            Assert.Equal(".tmp", p.Path.Extension.ToString());
            Assert.False(p.Path.FileExists());
            p.Dispose();
        }
    }

    [Fact]
    public void CreateFolder_CreatesDirectoryOnDisk()
    {
        var tempFolder = KnownFolders.EntryPoint.Combine(Guid.NewGuid().ToString());
        using var manager = new TemporaryFileManager(tempFolder);

        using var folder = manager.CreateFolder();
        Assert.True(folder.Path.DirectoryExists());
    }

    [Fact]
    public void CreateFile_SkipsExistingFiles()
    {
        var tempFolder = KnownFolders.EntryPoint.Combine(Guid.NewGuid().ToString());
        using var manager = new TemporaryFileManager(tempFolder);

        // Pre-create some file in the temp directory
        var preExistingFile = tempFolder.Combine("test-file.txt");
        preExistingFile.WriteAllText("hello");

        var newFile = manager.CreateFile();
        Assert.NotEqual(preExistingFile, newFile.Path);
        Assert.False(newFile.Path.FileExists());
        newFile.Dispose();
    }

    /// <summary>
    ///     Handing the same path to two concurrent callers lets them overwrite each other's data. The count is
    ///     high enough that a generator which repeats occasionally fails here every run rather than one run in
    ///     three; see <see cref="RandomNameTests.Next_ProducesUniqueNamesUnderConcurrency" />.
    /// </summary>
    [Fact]
    public void CreateFile_ConcurrentAccess_ProducesUniquePaths()
    {
        const int count = 2000;

        var tempFolder = KnownFolders.EntryPoint.Combine(Guid.NewGuid().ToString());
        using var manager = new TemporaryFileManager(tempFolder);

        var bag = new ConcurrentBag<TemporaryPath>();
        Parallel.For(0, count, _ =>
        {
            bag.Add(manager.CreateFile());
        });

        var distinctCount = bag.Select(p => p.Path).Distinct().Count();
        Assert.Equal(count, distinctCount);

        foreach (var p in bag)
        {
            p.Dispose();
        }
    }

    [Fact]
    public void Dispose_DeletesBasePath()
    {
        var tempFolder = KnownFolders.EntryPoint.Combine(Guid.NewGuid().ToString());
        var manager = new TemporaryFileManager(tempFolder, deleteOnDispose: true);
        Assert.True(tempFolder.DirectoryExists());

        manager.Dispose();
        Assert.False(tempFolder.DirectoryExists());
    }

    [Fact]
    public async Task DisposeAsync_DeletesBasePath()
    {
        var tempFolder = KnownFolders.EntryPoint.Combine(Guid.NewGuid().ToString());
        var manager = new TemporaryFileManager(tempFolder, deleteOnDispose: true);
        Assert.True(tempFolder.DirectoryExists());

        await manager.DisposeAsync();
        Assert.False(tempFolder.DirectoryExists());
    }
}
