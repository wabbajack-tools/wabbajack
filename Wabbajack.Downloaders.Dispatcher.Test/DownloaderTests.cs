using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Wabbajack.Downloaders.GameFile;
using Wabbajack.Downloaders.Http;
using Wabbajack.Downloaders.Interfaces;
using Wabbajack.Downloaders.ManualSources;
using Wabbajack.DTOs;
using Wabbajack.DTOs.DownloadStates;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Installer;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Wabbajack.RateLimiter;
using Xunit;

namespace Wabbajack.Downloaders.Dispatcher.Test;

[SuppressMessage("Usage", "xUnit1026:Theory methods should use all of their parameters")]
public class DownloaderTests
{
    private readonly DownloadDispatcher _dispatcher;
    private readonly IServiceProvider _provider;
    private readonly TemporaryFileManager _temp;

    public DownloaderTests(DownloadDispatcher dispatcher, TemporaryFileManager temp, IServiceProvider provider)
    {
        _temp = temp;
        _dispatcher = dispatcher;
        _provider = provider;
    }

    /// <summary>
    ///     Pairs of archives for each downloader. The first archive is considered valid,
    ///     the second should be invalid.
    /// </summary>
    public static IEnumerable<object[]> TestStates =>
        new List<object[]>
        {
            // Nexus Data
            new object[]
            {
                new Archive
                {
                    Hash = Hash.FromBase64("U9NkoW0w21k="),
                    State = new Nexus
                    {
                        Game = Game.SkyrimSpecialEdition,
                        ModID = 51939,
                        FileID = 212497
                    }
                },
                new Archive
                {
                    State = new Nexus
                    {
                        Game = Game.SkyrimSpecialEdition,
                        ModID = 51939,
                        FileID = 212497 + 1
                    }
                }
            },
            // Google Drive Data
            new object[]
            {
                new Archive
                {
                    Hash = Hash.FromBase64("eSIyd+KOG3s="),
                    State = new DTOs.DownloadStates.GoogleDrive {Id = "1grLRTrpHxlg7VPxATTFNfq2OkU_Plvh_"}
                },
                new Archive
                {
                    State = new DTOs.DownloadStates.GoogleDrive {Id = "2grLRTrpHxlg7VPxATTFNfq2OkU_Plvh_"}
                }
            },
            // LoversLab Data
            new object[]
            {
                new Archive
                {
                    Hash = Hash.FromBase64("eSIyd+KOG3s="),
                    State = new LoversLab {IPS4Mod = 11116, IPS4File = "WABBAJACK_TEST_FILE.zip"}
                },
                new Archive
                {
                    State = new LoversLab {IPS4Mod = 11116, IPS4File = "WABBAJACK_TEST_FILE_bad.zip"}
                }
            },
            // LoversLab Attachment Data
            new object[]
            {
                new Archive
                {
                    Hash = Hash.FromBase64("gLJDxGDaeQ0="),
                    State = new LoversLab {IsAttachment = true, IPS4Mod = 853295}
                },
                new Archive
                {
                    State = new LoversLab {IsAttachment = true, IPS4Mod = 85329599}
                }
            },
            // Manual Data
            new object[]
            {
                new Archive
                {
                    State = new DTOs.DownloadStates.Manual
                    {
                        Url = new Uri("https://example.com/files/WABBAJACK_TEST_FILE.zip"),
                        Prompt = "Click the green download button"
                    }
                },
                new Archive
                {
                    State = new DTOs.DownloadStates.Manual
                    {
                        Url = new Uri("https://example.com/files/WABBAJACK_TEST_FILE_bad.zip"),
                        Prompt = ""
                    }
                }
            },
            // MediaFire Data
            new object[]
            {
                new Archive
                {
                    Hash = Hash.FromBase64("eSIyd+KOG3s="),
                    State = new DTOs.DownloadStates.MediaFire
                    {
                        Url = new Uri("http://www.mediafire.com/file/agiqzm1xwebczpx/WABBAJACK_TEST_FILE.txt")
                    }
                },
                new Archive
                {
                    State = new DTOs.DownloadStates.MediaFire
                    {
                        Url = new Uri("http://www.mediafire.com/file/agiqzm1xwebcz42/WABBAJACK_TEST_FILE.txt")
                    }
                }
            },
            // Mega Data
            new object[]
            {
                new Archive
                {
                    Hash = Hash.FromBase64("eSIyd+KOG3s="),
                    State = new Mega
                    {
                        Url = new Uri("https://mega.nz/file/CsMSFaaJ#-uziC4mbJPRy2e4pPk8Gjb3oDT_38Be9fzZ6Ld4NL-k")
                    }
                },
                new Archive
                {
                    State = new Mega
                    {
                        Url = new Uri("https://mega.nz/file/zz42FaaJ#-uziC4mbJPRy2e4pPk8Gjb3oDT_38Be9fzZ6L42NL-k")
                    }
                }
            },
            // ModDB Data
            new object[]
            {
                new Archive
                {
                    Hash = Hash.FromBase64("V3ejL5oUeQI="),
                    State = new DTOs.DownloadStates.ModDB
                        {Url = new Uri("https://www.moddb.com/downloads/start/199178")}
                },
                new Archive
                {
                    State = new DTOs.DownloadStates.ModDB
                        {Url = new Uri("https://www.moddb.com/downloads/start/199178000000")}
                }
            },
            // VectorPlexus Data
            new object[]
            {
                new Archive
                {
                    Hash = Hash.FromBase64("eSIyd+KOG3s="),
                    State = new VectorPlexus {IPS4Mod = 290, IPS4File = "WABBAJACK_TEST_FILE.zip"}
                },
                new Archive
                {
                    State = new VectorPlexus {IPS4Mod = 290, IPS4File = "WABBAJACK_TEST_FILE_bad.zip"}
                }
            },
            // Wabbajack CDN Data
            new object[]
            {
                new Archive
                {
                    Hash = Hash.FromBase64("u7aZhqgDA6Y="),
                    State = new WabbajackCDN
                    {
                        Url = new Uri(
                            "https://authored-files.wabbajack.org/Tonal%20Architect_WJ_TEST_FILES.zip_9cb97a01-3354-4077-9e4a-7e808d47794f")
                    }
                },
                new Archive
                {
                    State = new WabbajackCDN
                    {
                        Url = new Uri(
                            "https://authored-files.wabbajack.org/Tonal%20Architect_WJ_TEST_FILES.zip_9cb97a01-3354-4077-9e4a-7e808d47794fFFOOO")
                    }
                }
            },
            // Bethesda
            new object[]
            {
                new Archive
                {
                    Hash = default,
                    State = new DTOs.DownloadStates.Bethesda
                    {
                        Game = Game.SkyrimSpecialEdition,
                        IsCCMod = true,
                        ProductId = 4,
                        BranchId = 90898,
                        ContentId = "4059054"
                    }
                },
                new Archive
                {
                    Hash = default,
                    State = new DTOs.DownloadStates.Bethesda
                    {
                        Game = Game.SkyrimSpecialEdition,
                        IsCCMod = true,
                        ProductId = 6,
                        BranchId = 9898,
                        ContentId = "059054"
                    }
                }
            }
        };

    [Theory]
    [MemberData(nameof(TestStates))]
    public async Task CanParseAndUnParseMetaInis(Archive goodArchive, Archive badArchive)
    {
        var meta = _dispatcher.MetaIniSection(goodArchive);
        var parsedIni = meta.LoadIniString()["General"];
        var newState = await _dispatcher.ResolveArchive(parsedIni.ToDictionary(d => d.KeyName, d => d.Value));
        Assert.NotNull(newState);
        Assert.Equal(meta, _dispatcher.MetaIniSection(new Archive {State = newState!}));
    }

    /// <summary>
    ///     A .meta directURL for each source that writes one, with the state it has to resolve to.
    ///     HttpDownloader.Resolve accepts any absolute directURL, so it will happily claim all of these
    ///     if it is asked first; only the stand-ins outranking it keeps a MediaFire link a MediaFire
    ///     state. Downloader(archive) cannot catch that - it dispatches on the state that came out - and
    ///     neither can the MetaIni round trip, since Http writes an identical directURL line. Resolving
    ///     from ini and checking the concrete type is the assertion that does.
    /// </summary>
    public static IEnumerable<object[]> DirectUrlResolutions =>
        new List<object[]>
        {
            new object[] {"http://www.mediafire.com/file/agiqzm1xwebczpx/WABBAJACK_TEST_FILE.txt", typeof(DTOs.DownloadStates.MediaFire)},
            new object[] {"https://mega.nz/file/CsMSFaaJ#-uziC4mbJPRy2e4pPk8Gjb3oDT_38Be9fzZ6Ld4NL-k", typeof(Mega)},
            new object[] {"https://drive.google.com/uc?id=1grLRTrpHxlg7VPxATTFNfq2OkU_Plvh_&export=download", typeof(DTOs.DownloadStates.GoogleDrive)},
            new object[] {"https://www.moddb.com/downloads/start/199178", typeof(DTOs.DownloadStates.ModDB)},
            new object[] {"https://github.com/ModOrganizer2/modorganizer/releases/download/v2.4.2/Mod.Organizer-2.4.2.7z", typeof(DTOs.DownloadStates.Http)}
        };

    [Theory]
    [MemberData(nameof(DirectUrlResolutions))]
    public async Task DirectUrlResolvesToTheSourcesOwnState(string url, Type expected)
    {
        var ini = $"[General]\ndirectURL={url}".LoadIniString()["General"];
        var state = await _dispatcher.ResolveArchive(ini.ToDictionary(d => d.KeyName, d => d.Value));

        Assert.NotNull(state);
        Assert.IsType(expected, state);
    }

    /// <summary>
    ///     Each state type paired with the downloader the dispatcher must pick for it. Only Http, Nexus,
    ///     WabbajackCDN and game files download on their own; every other state is served by the
    ///     metadata-only stand-in from Wabbajack.Downloaders.ManualSources, which is now the sole
    ///     provider for it.
    /// </summary>
    public static IEnumerable<object[]> ExpectedDownloaders =>
        new List<object[]>
        {
            new object[] {new DTOs.DownloadStates.Http {Url = new Uri("https://example.com/a.zip")}, typeof(HttpDownloader)},
            new object[] {new Nexus {Game = Game.SkyrimSpecialEdition, ModID = 1, FileID = 1}, typeof(NexusDownloader)},
            new object[] {new WabbajackCDN {Url = new Uri("https://authored-files.wabbajack.org/a.zip")}, typeof(WabbajackCDNDownloader)},
            new object[] {new GameFileSource {Game = Game.SkyrimSpecialEdition, GameFile = "Skyrim.esm".ToRelativePath()}, typeof(GameFileDownloader)},
            new object[] {new DTOs.DownloadStates.Manual {Url = new Uri("https://example.com/a.zip"), Prompt = ""}, typeof(ManualSourceDownloader)},
            new object[] {new DTOs.DownloadStates.MediaFire {Url = new Uri("http://www.mediafire.com/file/a/a.zip")}, typeof(MediaFireSource)},
            new object[] {new Mega {Url = new Uri("https://mega.nz/file/a#b")}, typeof(MegaSource)},
            new object[] {new DTOs.DownloadStates.GoogleDrive {Id = "1grLRTrpHxlg7VPxATTFNfq2OkU_Plvh_"}, typeof(GoogleDriveSource)},
            new object[] {new DTOs.DownloadStates.ModDB {Url = new Uri("https://www.moddb.com/downloads/start/1")}, typeof(ModDBSource)},
            new object[] {new LoversLab {IPS4Mod = 1, IPS4File = "a.zip"}, typeof(LoversLabSource)},
            new object[] {new LoversLab {IPS4Mod = 2, IsAttachment = true}, typeof(LoversLabSource)},
            new object[] {new VectorPlexus {IPS4Mod = 1, IPS4File = "a.zip"}, typeof(VectorPlexusSource)},
            new object[]
            {
                new DTOs.DownloadStates.Bethesda {Game = Game.SkyrimSpecialEdition, IsCCMod = true, ProductId = 4, BranchId = 90898, ContentId = "4059054"},
                typeof(BethesdaSource)
            }
        };

    [Theory]
    [MemberData(nameof(ExpectedDownloaders))]
    public void DispatcherPicksTheExpectedDownloader(IDownloadState state, Type expected)
    {
        var archive = new Archive {Name = "a.zip", State = state};
        Assert.Equal(expected, _dispatcher.Downloader(archive).GetType());
    }

    /// <summary>
    ///     Each manual state with the metadata-only downloader that stands in for it.
    /// </summary>
    public static IEnumerable<object[]> ManualSources =>
        new List<object[]>
        {
            new object[] {new DTOs.DownloadStates.Manual {Url = new Uri("https://example.com/a.zip"), Prompt = "Press download"}, typeof(ManualSourceDownloader)},
            new object[] {new DTOs.DownloadStates.MediaFire {Url = new Uri("http://www.mediafire.com/file/a/a.zip")}, typeof(MediaFireSource)},
            new object[] {new Mega {Url = new Uri("https://mega.nz/file/a#b")}, typeof(MegaSource)},
            new object[] {new DTOs.DownloadStates.GoogleDrive {Id = "1grLRTrpHxlg7VPxATTFNfq2OkU_Plvh_"}, typeof(GoogleDriveSource)},
            new object[] {new DTOs.DownloadStates.ModDB {Url = new Uri("https://www.moddb.com/downloads/start/1")}, typeof(ModDBSource)},
            new object[] {new LoversLab {IPS4Mod = 1, IPS4File = "a.zip"}, typeof(LoversLabSource)},
            new object[] {new LoversLab {IPS4Mod = 2, IsAttachment = true}, typeof(LoversLabSource)},
            new object[] {new VectorPlexus {IPS4Mod = 1, IPS4File = "a.zip"}, typeof(VectorPlexusSource)}
        };

    [Theory]
    [MemberData(nameof(ManualSources))]
    public async Task ManualSourceDownloadThrowsManualDownloadRequired(IDownloadState state, Type sourceType)
    {
        var source = (IDownloader) _provider.GetRequiredService(sourceType);
        var archive = new Archive {Name = "a.zip", Size = 1, State = state};
        Assert.True(source.CanDownload(archive));

        using var job = await _provider.GetRequiredService<IResource<DownloadDispatcher>>()
            .Begin("test", 1, CancellationToken.None);
        await using var dest = _temp.CreateFile();

        Assert.True(await source.Prepare());
        Assert.True(await source.Verify(archive, job, CancellationToken.None));

        var ex = await Assert.ThrowsAsync<ManualDownloadRequiredException>(() =>
            source.Download(archive, dest.Path, job, CancellationToken.None));

        Assert.Same(archive, ex.Archive);
        Assert.True(ManualDownloadUrls.TryGet(state, out var expectedTarget));
        Assert.Equal(expectedTarget, ex.Target);

        if (source is IProxyable proxyable)
            await Assert.ThrowsAsync<ManualDownloadRequiredException>(() =>
                proxyable.DownloadStream(archive, _ => Task.FromResult(0), CancellationToken.None));
    }

    [Fact]
    public async Task BethesdaSourceRefusesToDownload()
    {
        var source = _provider.GetRequiredService<BethesdaSource>();
        var archive = new Archive
        {
            Name = "a.zip",
            State = new DTOs.DownloadStates.Bethesda
                {Game = Game.SkyrimSpecialEdition, IsCCMod = true, ProductId = 4, BranchId = 90898, ContentId = "4059054"}
        };

        using var job = await _provider.GetRequiredService<IResource<DownloadDispatcher>>()
            .Begin("test", 1, CancellationToken.None);
        await using var dest = _temp.CreateFile();

        Assert.True(await source.Verify(archive, job, CancellationToken.None));
        await Assert.ThrowsAsync<NotSupportedException>(() =>
            source.Download(archive, dest.Path, job, CancellationToken.None));
    }
}