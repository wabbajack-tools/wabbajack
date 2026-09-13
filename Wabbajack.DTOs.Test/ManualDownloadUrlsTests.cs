using System;
using System.Collections.Generic;
using Wabbajack.DTOs.DownloadStates;
using Wabbajack.Paths;
using Xunit;

namespace Wabbajack.DTOs.Test;

public class ManualDownloadUrlsTests
{
    /// <summary>
    ///     One row per download state. A null URL means the state has no page a user can download from.
    /// </summary>
    public static IEnumerable<object[]> Cases =>
        new List<object[]>
        {
            new object[]
            {
                new Manual {Url = new Uri("https://example.com/files/WABBAJACK_TEST_FILE.zip"), Prompt = "Click the green button"},
                "https://example.com/files/WABBAJACK_TEST_FILE.zip", "Manual download", "Click the green button"
            },
            new object[]
            {
                new Manual {Url = new Uri("https://example.com/files/WABBAJACK_TEST_FILE.zip"), Prompt = ""},
                "https://example.com/files/WABBAJACK_TEST_FILE.zip", "Manual download", "Download the file from this page"
            },
            new object[]
            {
                new MediaFire {Url = new Uri("http://www.mediafire.com/file/agiqzm1xwebczpx/WABBAJACK_TEST_FILE.txt")},
                "http://www.mediafire.com/file/agiqzm1xwebczpx/WABBAJACK_TEST_FILE.txt", "MediaFire", "Download the file from this page"
            },
            new object[]
            {
                new Mega {Url = new Uri("https://mega.nz/file/CsMSFaaJ#-uziC4mbJPRy2e4pPk8Gjb3oDT_38Be9fzZ6Ld4NL-k")},
                "https://mega.nz/file/CsMSFaaJ#-uziC4mbJPRy2e4pPk8Gjb3oDT_38Be9fzZ6Ld4NL-k", "Mega", "Download the file from this page"
            },
            new object[]
            {
                new ModDB {Url = new Uri("https://www.moddb.com/downloads/start/199178")},
                "https://www.moddb.com/downloads/start/199178", "ModDB", "Download the file from this page"
            },
            new object[]
            {
                new Http {Url = new Uri("https://example.com/direct/WABBAJACK_TEST_FILE.zip")},
                "https://example.com/direct/WABBAJACK_TEST_FILE.zip", "Direct download", "Download the file from this page"
            },
            new object[]
            {
                new WabbajackCDN {Url = new Uri("https://authored-files.wabbajack.org/Tonal%20Architect_WJ_TEST_FILES.zip_9cb97a01-3354-4077-9e4a-7e808d47794f")},
                "https://authored-files.wabbajack.org/Tonal Architect_WJ_TEST_FILES.zip_9cb97a01-3354-4077-9e4a-7e808d47794f", "Wabbajack CDN", "Download the file from this page"
            },
            new object[]
            {
                new GoogleDrive {Id = "1grLRTrpHxlg7VPxATTFNfq2OkU_Plvh_"},
                "https://drive.google.com/uc?id=1grLRTrpHxlg7VPxATTFNfq2OkU_Plvh_&export=download", "Google Drive", "Download the file from this page"
            },
            new object[]
            {
                new Nexus {Game = Game.SkyrimSpecialEdition, ModID = 51939, FileID = 212497},
                "https://www.nexusmods.com/skyrimspecialedition/mods/51939?tab=files&file_id=212497", "Nexus Mods", "Download the file from this page"
            },
            new object[]
            {
                new LoversLab {IPS4Mod = 11116, IPS4File = "WABBAJACK_TEST_FILE.zip"},
                "https://www.loverslab.com/files/file/11116/", "Lovers Lab", "Download the file named WABBAJACK_TEST_FILE.zip"
            },
            new object[]
            {
                new LoversLab {IsAttachment = true, IPS4Mod = 853295},
                "https://www.loverslab.com/applications/core/interface/file/attachment.php?id=853295", "Lovers Lab", "Download the file from this page"
            },
            new object[]
            {
                new VectorPlexus {IPS4Mod = 290, IPS4File = "WABBAJACK_TEST_FILE.zip"},
                "https://vectorplexis.com/files/file/290/", "Vector Plexus", "Download the file named WABBAJACK_TEST_FILE.zip"
            },
            new object[]
            {
                new VectorPlexus {IsAttachment = true, IPS4Mod = 4242},
                "https://vectorplexis.com/applications/core/interface/file/attachment.php?id=4242", "Vector Plexus", "Download the file from this page"
            },
            new object[]
            {
                new Bethesda {Game = Game.SkyrimSpecialEdition, IsCCMod = true, ProductId = 4, BranchId = 90898, ContentId = "4059054"},
                null, null, null
            },
            new object[]
            {
                new GameFileSource {Game = Game.SkyrimSpecialEdition, GameFile = "Data/Skyrim.esm".ToRelativePath()},
                null, null, null
            },
            new object[] {new DeprecatedLoversLab(), null, null, null},
            new object[] {new TESAlliance(), null, null, null}
        };

    [Theory]
    [MemberData(nameof(Cases))]
    public void MapsEachStateToItsBrowserPage(IDownloadState state, string url, string siteName, string instructions)
    {
        var found = ManualDownloadUrls.TryGet(state, out var target);

        if (url == null)
        {
            Assert.False(found);
            Assert.Null(target);
            return;
        }

        Assert.True(found);
        Assert.NotNull(target);
        Assert.Equal(url, target.Url.ToString());
        Assert.Equal(siteName, target.SiteName);
        Assert.Equal(instructions, target.Instructions);
    }
}
