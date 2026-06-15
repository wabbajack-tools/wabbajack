using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Wabbajack.Downloaders.Interfaces;
using Wabbajack.DTOs;
using Wabbajack.DTOs.DownloadStates;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Installer;
using Wabbajack.Paths.IO;

namespace Wabbajack.Downloaders.Dispatcher.Test;

[SuppressMessage("Usage", "xUnit1026:Theory methods should use all of their parameters")]
[ClassConstructor<DispatcherClassConstructor>]
public class DownloaderTests
{
    private readonly DownloadDispatcher _dispatcher;
    private readonly TemporaryFileManager _temp;

    public DownloaderTests(DownloadDispatcher dispatcher, TemporaryFileManager temp)
    {
        _temp = temp;
        _dispatcher = dispatcher;
    }

    /// <summary>
    ///     Pairs of archives for each downloader. The first archive is considered valid,
    ///     the second should be invalid.
    /// </summary>
    public static IEnumerable<(Archive, Archive)> TestStates =>
        new List<(Archive, Archive)>
        {
            // Nexus Data
            (
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
            ),
            // Wabbajack CDN Data
            (
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
            ),
        };

    [Test]
    [MethodDataSource(nameof(TestStates))]
    public async Task CanParseAndUnParseMetaInis(Archive goodArchive, Archive badArchive)
    {
        var meta = _dispatcher.MetaIniSection(goodArchive);
        var parsedIni = meta.LoadIniString()["General"];
        var newState = await _dispatcher.ResolveArchive(parsedIni.ToDictionary(d => d.KeyName, d => d.Value));
        await Assert.That(newState).IsNotNull();
        await Assert.That(_dispatcher.MetaIniSection(new Archive {State = newState!})).IsEqualTo(meta);
    }

    /// <summary>
    ///     Mega, GoogleDrive, MediaFire, ModDB, LoversLab, VectorPlexus and Bethesda no longer have
    ///     dedicated downloaders. Their states must be reinterpreted as Manual downloads so existing
    ///     modlists still install.
    /// </summary>
    public static IEnumerable<IDownloadState> OrphanedStates =>
        new List<IDownloadState>
        {
            new Mega {Url = new Uri("https://mega.nz/file/CsMSFaaJ#abc")},
            new DTOs.DownloadStates.GoogleDrive {Id = "1grLRTrpHxlg7VPxATTFNfq2OkU_Plvh_"},
            new DTOs.DownloadStates.MediaFire {Url = new Uri("http://www.mediafire.com/file/x/F.txt")},
            new DTOs.DownloadStates.ModDB {Url = new Uri("https://www.moddb.com/downloads/start/199178")},
            new LoversLab {IPS4Mod = 11116, IPS4File = "F.zip", IPS4Url = "https://www.loverslab.com/files/file/123"},
            new VectorPlexus {IPS4Mod = 290, IPS4File = "F.zip"},
            new DTOs.DownloadStates.Bethesda {Game = Game.SkyrimSpecialEdition, IsCCMod = true, Name = "Survival Mode"},
        };

    [Test]
    [MethodDataSource(nameof(OrphanedStates))]
    public async Task RemovedDownloaderStateReinterpretsToManual(IDownloadState state)
    {
        var archive = new Archive {Name = "test.zip", State = state};

        var reinterpreted = _dispatcher.ReinterpretIfOrphaned(archive);

        var manual = (DTOs.DownloadStates.Manual)reinterpreted.State;
        await Assert.That(reinterpreted.State).IsTypeOf<DTOs.DownloadStates.Manual>();
        await Assert.That(manual.Url).IsNotNull();
        await Assert.That(string.IsNullOrWhiteSpace(manual.Prompt)).IsFalse();
    }

    [Test]
    public async Task KnownDownloaderStateIsNotReinterpreted()
    {
        var archive = new Archive
        {
            State = new Nexus {Game = Game.SkyrimSpecialEdition, ModID = 1, FileID = 2}
        };

        var result = _dispatcher.ReinterpretIfOrphaned(archive);

        await Assert.That(result).IsSameReferenceAs(archive);
    }
}