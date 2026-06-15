using System;
using System.Linq;

namespace Wabbajack.Paths.IO.Test;

public class AbsolutePathTests
{
    private AbsolutePath GetTempFile()
    {
        return KnownFolders.EntryPoint.Combine(Guid.NewGuid().ToString());
    }

    [Test]
    [Arguments(0)]
    [Arguments(1)]
    [Arguments(16)]
    [Arguments(4096)]
    public async Task CanReadAndWriteFiles(int size)
    {
        var data = new byte[size];
        new Random(size + 1).NextBytes(data);

        var file = GetTempFile();
        file.WriteAllBytes(data);

        await Assert.That(file.Size()).IsEqualTo(data.Length);
        await Assert.That(file.ReadAllBytes().SequenceEqual(data)).IsTrue();
        file.Delete();

        file.WriteAllText("Test");
        await Assert.That(file.ReadAllText()).IsEqualTo("Test");
        file.Delete();
    }

    [Test]
    public async Task CanReadAndWriteFilesAsync()
    {
        var data = "This is a test";
        var file = GetTempFile();
        await file.WriteAllTextAsync(data);

        await Assert.That(file.Size()).IsEqualTo(data.Length);

        await Assert.That(await file.ReadAllTextAsync()).IsEqualTo(data);
        file.Delete();
    }

    [Test]
    [Arguments(1)]
    [Arguments(10)]
    [Arguments(100)]
    public async Task LongPathsAreSupported(int depth)
    {
        // OSX has a max length of 1024, so cap depth at 100
        var basePath = KnownFolders.EntryPoint.Combine("deep_paths");
        basePath.DeleteDirectory();

        var path = Enumerable.Range(1, depth + 1).Aggregate(basePath, (p, i) => p.Combine($"path_{i}"));
        path.Parent.CreateDirectory();
        path.WriteAllText("test");

        await Assert.That(path.FileExists()).IsTrue();

        basePath.DeleteDirectory();
    }
}
