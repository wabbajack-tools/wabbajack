using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Wabbajack.Paths.IO.Test;

/// <summary>
///     <see cref="Volumes.TryMoveWithinVolumeAsync" />: a move within a volume renames, and one across volumes
///     touches nothing. The second needs two file systems; off Windows /dev/shm is a tmpfs nearly everywhere.
/// </summary>
public class VolumesTests : IDisposable
{
    private readonly AbsolutePath _root = KnownFolders.EntryPoint.Combine("volumemove-" + Guid.NewGuid());
    private readonly AbsolutePath _otherVolume = "/dev/shm".ToAbsolutePath().Combine("wj-volumemove-" + Guid.NewGuid());

    public VolumesTests()
    {
        _root.CreateDirectory();
    }

    public void Dispose()
    {
        _root.DeleteDirectory();
        if (_otherVolume.DirectoryExists()) _otherVolume.DeleteDirectory();
    }

    [Fact]
    public async Task WithinAVolume_Renames()
    {
        var src = _root.Combine("a.7z");
        var dest = _root.Combine("sub", "b.7z");
        src.WriteAllText("data");
        dest.Parent.CreateDirectory();
        dest.WriteAllText("old");

        Assert.True(await src.TryMoveWithinVolumeAsync(dest, CancellationToken.None));

        Assert.False(src.FileExists());
        Assert.Equal("data", await dest.ReadAllTextAsync());
    }

    [Fact]
    public async Task AcrossVolumes_LeavesBothAlone()
    {
        if (OperatingSystem.IsWindows() || !Directory.Exists("/dev/shm")) return;
        _otherVolume.CreateDirectory();
        var src = _otherVolume.Combine("a.7z");
        var dest = _root.Combine("a.7z");
        src.WriteAllText("data");

        Assert.False(await src.TryMoveWithinVolumeAsync(dest, CancellationToken.None));

        Assert.True(src.FileExists());
        Assert.False(dest.FileExists());
    }

    [Fact]
    public async Task AMissingSource_Throws()
    {
        var src = _root.Combine("missing.7z");
        await Assert.ThrowsAnyAsync<IOException>(async () =>
            await src.TryMoveWithinVolumeAsync(_root.Combine("b.7z"), CancellationToken.None));
    }
}
