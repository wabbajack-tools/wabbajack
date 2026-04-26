using System;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Xunit;

namespace Wabbajack.VFS.Test;

public class VFSTests : IDisposable
{
    private readonly AbsolutePath _archiveTestTxt;


    private readonly Context _context;
    private readonly TemporaryFileManager _manager;
    private readonly AbsolutePath _testTxt;
    private readonly AbsolutePath _testZip;
    private readonly AbsolutePath _vfsTestDir;

    public VFSTests(Context context, TemporaryFileManager manager)
    {
        _context = context;
        _manager = manager;

        _vfsTestDir = _manager.CreateFolder();
        _testZip = "test.zip".ToRelativePath().RelativeTo(_vfsTestDir);
        _testTxt = "test.txt".ToRelativePath().RelativeTo(_vfsTestDir);
        _archiveTestTxt = "archive/test.txt".ToRelativePath().RelativeTo(_vfsTestDir);
    }

    public void Dispose()
    {
        _manager?.Dispose();
    }

    [Fact]
    public async Task FilesAreIndexed()
    {
        await AddFile(_testTxt, "This is a test");
        await _context.AddRoot(_vfsTestDir, CancellationToken.None);

        var file = _context.Index.ByRootPath["test.txt".ToRelativePath().RelativeTo(_vfsTestDir)];
        Assert.NotNull(file);

        Assert.Equal(14, file.Size);
        Assert.Equal(file.Hash, Hash.FromBase64("qX0GZvIaTKM="));
    }

    [Fact]
    public async Task ArchiveContentsAreIndexed()
    {
        await AddFile(_archiveTestTxt, "This is a test");
        await ZipUpFolder(_archiveTestTxt.Parent, _testZip);
        await _context.AddRoot(_vfsTestDir, CancellationToken.None);

        var absPath = "test.zip".ToRelativePath().RelativeTo(_vfsTestDir);
        var file = _context.Index.ByRootPath[absPath];
        Assert.NotNull(file);

        Assert.Equal(130, file.Size);
        //Assert.Equal(await absPath.HashCopyTo(Stream.Null), file.Hash);

        Assert.True(file.IsArchive);
        var innerFile = file.Children.First();
        Assert.Equal(14, innerFile.Size);
        Assert.Equal(Hash.FromBase64("qX0GZvIaTKM="), innerFile.Hash);
        Assert.Same(file, file.Children.First().Parent);
    }


    [Fact]
    public async Task DuplicateFileHashes()
    {
        await AddFile(_archiveTestTxt, "This is a test");
        await ZipUpFolder(_archiveTestTxt.Parent, _testZip);

        await AddFile(_testTxt, "This is a test");
        await _context.AddRoot(_vfsTestDir, CancellationToken.None);

        var files = _context.Index.ByHash[Hash.FromBase64("qX0GZvIaTKM=")];
        Assert.Equal(2, files.Count());
    }

    [Fact]
    public async Task DeletedFilesAreRemoved()
    {
        await AddFile(_testTxt, "This is a test");
        await _context.AddRoot(_vfsTestDir, CancellationToken.None);

        var file = _context.Index.ByRootPath[_testTxt];
        Assert.NotNull(file);

        Assert.Equal(14, file.Size);
        Assert.Equal(Hash.FromBase64("qX0GZvIaTKM="), file.Hash);

        _testTxt.Delete();

        await _context.AddRoot(_vfsTestDir, CancellationToken.None);

        Assert.DoesNotContain(_testTxt, _context.Index.AllFiles.Select(f => f.AbsoluteName));
    }

    [Fact]
    public async Task UnmodifiedFilesAreNotReIndexed()
    {
        await AddFile(_testTxt, "This is a test");
        await _context.AddRoot(_vfsTestDir, CancellationToken.None);

        var old_file = _context.Index.ByRootPath[_testTxt];
        var old_time = old_file.LastAnalyzed;

        await _context.AddRoot(_vfsTestDir, CancellationToken.None);

        var new_file = _context.Index.ByRootPath[_testTxt];

        Assert.Equal(old_time, new_file.LastAnalyzed);
    }

    [Fact]
    public async Task CanStageSimpleArchives()
    {
        await AddFile(_archiveTestTxt, "This is a test");
        await ZipUpFolder(_archiveTestTxt.Parent, _testZip);
        await _context.AddRoot(_vfsTestDir, CancellationToken.None);

        var res = new FullPath(_testZip, (RelativePath) "test.txt");
        var files = new[] {_context.Index.ByFullPath[res]};


        await _context.Extract(files.ToHashSet(), async (file, factory) =>
        {
            await using var s = await factory.GetStream();
            //Assert.Equal("This is a test", await s.ReadAllTextAsync());
        }, CancellationToken.None);
    }

    [Fact]
    public async Task CanStageNestedArchives()
    {
        await AddFile(_archiveTestTxt, "This is a test");
        await ZipUpFolder(_archiveTestTxt.Parent, _testZip);

        var innerDir = @"archive\other\dir".ToRelativePath().RelativeTo(_vfsTestDir);
        innerDir.CreateDirectory();
        await _testZip.MoveToAsync(@"archive\other\dir\nested.zip".ToRelativePath().RelativeTo(_vfsTestDir), true,
            CancellationToken.None);
        await ZipUpFolder(_archiveTestTxt.Parent, _testZip);

        await _context.AddRoot(_vfsTestDir, CancellationToken.None);

        var files = _context.Index.ByHash[Hash.FromBase64("qX0GZvIaTKM=")];

        await _context.Extract(files.ToHashSet(), async (file, factory) =>
        {
            await using var s = await factory.GetStream();
            //Assert.Equal("This is a test", await s.ReadAllTextAsync());
        }, CancellationToken.None);
    }


    private static async Task AddFile(AbsolutePath filename, string text)
    {
        filename.Parent.CreateDirectory();
        await filename.WriteAllTextAsync(text);
    }

    private static Task ZipUpFolder(AbsolutePath folder, AbsolutePath output)
    {
        ZipFile.CreateFromDirectory(folder.ToString(), output.ToString());
        folder.DeleteDirectory();
        return Task.CompletedTask;
    }
}