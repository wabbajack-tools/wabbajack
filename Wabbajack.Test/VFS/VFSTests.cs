using System;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;

namespace Wabbajack.VFS.Test;

[ClassConstructor<VfsClassConstructor>]
public class VFSTests
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

    [After(HookType.Test)]
    public void Cleanup()
    {
        _manager?.Dispose();
    }

    [Test]
    public async Task FilesAreIndexed()
    {
        await AddFile(_testTxt, "This is a test");
        await _context.AddRoot(_vfsTestDir, CancellationToken.None);

        var file = _context.Index.ByRootPath["test.txt".ToRelativePath().RelativeTo(_vfsTestDir)];
        await Assert.That(file).IsNotNull();

        await Assert.That(file.Size).IsEqualTo(14);
        await Assert.That(Hash.FromBase64("qX0GZvIaTKM=")).IsEqualTo(file.Hash);
    }

    [Test]
    public async Task ArchiveContentsAreIndexed()
    {
        await AddFile(_archiveTestTxt, "This is a test");
        await ZipUpFolder(_archiveTestTxt.Parent, _testZip);
        await _context.AddRoot(_vfsTestDir, CancellationToken.None);

        var absPath = "test.zip".ToRelativePath().RelativeTo(_vfsTestDir);
        var file = _context.Index.ByRootPath[absPath];
        await Assert.That(file).IsNotNull();

        await Assert.That(file.Size).IsEqualTo(130);
        //Assert.Equal(await absPath.HashCopyTo(Stream.Null), file.Hash);

        await Assert.That(file.IsArchive).IsTrue();
        var innerFile = file.Children.First();
        await Assert.That(innerFile.Size).IsEqualTo(14);
        await Assert.That(innerFile.Hash).IsEqualTo(Hash.FromBase64("qX0GZvIaTKM="));
        await Assert.That(file.Children.First().Parent).IsSameReferenceAs(file);
    }


    [Test]
    public async Task DuplicateFileHashes()
    {
        await AddFile(_archiveTestTxt, "This is a test");
        await ZipUpFolder(_archiveTestTxt.Parent, _testZip);

        await AddFile(_testTxt, "This is a test");
        await _context.AddRoot(_vfsTestDir, CancellationToken.None);

        var files = _context.Index.ByHash[Hash.FromBase64("qX0GZvIaTKM=")];
        await Assert.That(files.Count()).IsEqualTo(2);
    }

    [Test]
    public async Task DeletedFilesAreRemoved()
    {
        await AddFile(_testTxt, "This is a test");
        await _context.AddRoot(_vfsTestDir, CancellationToken.None);

        var file = _context.Index.ByRootPath[_testTxt];
        await Assert.That(file).IsNotNull();

        await Assert.That(file.Size).IsEqualTo(14);
        await Assert.That(file.Hash).IsEqualTo(Hash.FromBase64("qX0GZvIaTKM="));

        _testTxt.Delete();

        await _context.AddRoot(_vfsTestDir, CancellationToken.None);

        await Assert.That(_context.Index.AllFiles.Select(f => f.AbsoluteName)).DoesNotContain(_testTxt);
    }

    [Test]
    public async Task UnmodifiedFilesAreNotReIndexed()
    {
        await AddFile(_testTxt, "This is a test");
        await _context.AddRoot(_vfsTestDir, CancellationToken.None);

        var old_file = _context.Index.ByRootPath[_testTxt];
        var old_time = old_file.LastAnalyzed;

        await _context.AddRoot(_vfsTestDir, CancellationToken.None);

        var new_file = _context.Index.ByRootPath[_testTxt];

        await Assert.That(new_file.LastAnalyzed).IsEqualTo(old_time);
    }

    [Test]
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

    [Test]
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