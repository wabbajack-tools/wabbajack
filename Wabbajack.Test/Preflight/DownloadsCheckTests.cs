// Wabbajack.Test/Preflight/DownloadsCheckTests.cs
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Wabbajack.DTOs;
using Wabbajack.DTOs.DownloadStates;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Wabbajack.Preflight;
using Xunit;

namespace Wabbajack.Preflight.Test;

public class DownloadsCheckTests : IDisposable
{
    private readonly TemporaryFileManager _tempManager;
    private readonly AbsolutePath _downloadDir;
    private readonly AbsolutePath _watchDir;

    public DownloadsCheckTests()
    {
        _tempManager = new TemporaryFileManager();
        _downloadDir = _tempManager.CreateFolder().Path;
        _watchDir = _tempManager.CreateFolder().Path;
    }

    private Archive MakeArchive(string name, long size, Hash hash, IDownloadState state)
    {
        return new Archive { Name = name, Size = size, Hash = hash, State = state };
    }

    [Fact]
    public void NoManualArchives_Passes()
    {
        var archives = new[]
        {
            MakeArchive("mod1.zip", 100, new Hash(1), new Nexus { Game = Game.SkyrimSpecialEdition, ModID = 1, FileID = 1 }),
            MakeArchive("mod2.zip", 200, new Hash(2), new Http { Url = new Uri("https://example.com/mod2.zip") }),
        };

        var check = new DownloadsCheck(archives, _downloadDir, _watchDir, isPremium: true);

        Assert.Equal(PreflightCheckStatus.Passed, check.Status);
    }

    [Fact]
    public void ManualArchivesMissing_Fails()
    {
        var archives = new[]
        {
            MakeArchive("manual.zip", 100, new Hash(1), new Manual { Url = new Uri("https://example.com/manual") }),
        };

        var check = new DownloadsCheck(archives, _downloadDir, _watchDir, isPremium: true);

        Assert.Equal(PreflightCheckStatus.Failed, check.Status);
        Assert.Equal(1, check.SubItems!.Count);
        Assert.Equal("manual.zip", check.SubItems[0].Name);
    }

    [Fact]
    public void NonPremium_NexusArchivesAreManual()
    {
        var archives = new[]
        {
            MakeArchive("nexusmod.zip", 100, new Hash(1), new Nexus { Game = Game.SkyrimSpecialEdition, ModID = 1, FileID = 1 }),
        };

        var check = new DownloadsCheck(archives, _downloadDir, _watchDir, isPremium: false);

        Assert.Equal(PreflightCheckStatus.Failed, check.Status);
        Assert.Equal(1, check.SubItems!.Count);
    }

    [Fact]
    public async Task ArchiveAlreadyInDownloadFolder_Passes()
    {
        var data = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
        var hash = await data.Hash();
        var filePath = _downloadDir.Combine("existing.zip");
        await filePath.WriteAllBytesAsync(data);

        var archives = new[]
        {
            MakeArchive("existing.zip", data.Length, hash, new Manual { Url = new Uri("https://example.com") }),
        };

        var check = new DownloadsCheck(archives, _downloadDir, _watchDir, isPremium: true);
        await check.ScanExistingFiles(CancellationToken.None);

        Assert.Equal(PreflightCheckStatus.Passed, check.Status);
    }

    public void Dispose()
    {
        _tempManager.Dispose();
    }
}
