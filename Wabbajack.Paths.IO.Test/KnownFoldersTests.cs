using System;
using System.IO;
using Xunit;

namespace Wabbajack.Paths.IO.Test;

public class KnownFoldersTests
{
    [Fact]
    public void DownloadsIsNeverDefault()
    {
        var downloads = KnownFolders.Downloads;
        Assert.NotEqual(default, downloads);
        Assert.False(string.IsNullOrWhiteSpace(downloads.ToString()));
    }

    [Fact]
    public void DownloadsIsStableAcrossCalls()
    {
        Assert.Equal(KnownFolders.Downloads, KnownFolders.Downloads);
    }

    [Fact]
    public void DownloadsExistsOnWindows()
    {
        if (!OperatingSystem.IsWindows()) return;
        Assert.True(Directory.Exists(KnownFolders.Downloads.ToString()),
            $"expected the Downloads folder to exist: {KnownFolders.Downloads}");
    }

    [Fact]
    public void DownloadsIsNamedDownloadsWhenNotRelocated()
    {
        // The shell answers with the user's chosen location, so only the fallback shape is asserted here.
        var downloads = KnownFolders.Downloads;
        if (downloads.Parent == Environment.GetFolderPath(Environment.SpecialFolder.UserProfile).ToAbsolutePath())
            Assert.Equal("Downloads", downloads.FileName.ToString());
    }
}
