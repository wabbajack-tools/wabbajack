using System;
using System.Collections.Generic;
using System.Linq;
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
                // A CDN address is a folder of numbered parts; an authored file's page on the build server is what
                // turns them back into the file in a browser.
                "https://build.wabbajack.org/authored_files/download/Tonal Architect_WJ_TEST_FILES.zip_9cb97a01-3354-4077-9e4a-7e808d47794f",
                "Wabbajack CDN", "Download the file from this page"
            },
            new object[]
            {
                new WabbajackCDN {Url = new Uri("https://wabbajack.b-cdn.net/636039181_MNCABC-FluffworksPatchv0.2.rar_6d2f8414-b3a5-4e1b-bd37-2a3c4f3ecde1")},
                "https://build.wabbajack.org/authored_files/download/636039181_MNCABC-FluffworksPatchv0.2.rar_6d2f8414-b3a5-4e1b-bd37-2a3c4f3ecde1",
                "Wabbajack CDN", "Download the file from this page"
            },
            new object[]
            {
                // Mirrored and patch files have no page to download them from.
                new WabbajackCDN {Url = new Uri("https://mirror.wabbajack.org/7c2a1f6e4b3d9a80")},
                null, null, null
            },
            new object[]
            {
                new GoogleDrive {Id = "1grLRTrpHxlg7VPxATTFNfq2OkU_Plvh_"},
                "https://drive.google.com/uc?id=1grLRTrpHxlg7VPxATTFNfq2OkU_Plvh_&export=download", "Google Drive", "Download the file from this page"
            },
            new object[]
            {
                new Nexus {Game = Game.SkyrimSpecialEdition, ModID = 51939, FileID = 212497},
                "https://www.nexusmods.com/Core/Libs/Common/Widgets/DownloadPopUp?id=212497&game_id=1704", "Nexus Mods", "Press Slow download on this page"
            },
            // The page Nexus's own Manual download button opens. The owner confirmed on 2026-10-17 that this
            // exact link, opened in a browser, goes straight to the download for the file.
            new object[]
            {
                new Nexus {Game = Game.SkyrimSpecialEdition, ModID = 266, FileID = 209150},
                "https://www.nexusmods.com/Core/Libs/Common/Widgets/DownloadPopUp?id=209150&game_id=1704", "Nexus Mods", "Press Slow download on this page"
            },
            // No file id recorded. file_id=0 selects nothing, so the Files tab on its own is the most
            // specific page that still resolves, and the user is told they have to find the file there.
            new object[]
            {
                new Nexus {Game = Game.SkyrimSpecialEdition, ModID = 266},
                "https://www.nexusmods.com/skyrimspecialedition/mods/266?tab=files", "Nexus Mods",
                "This list doesn't record which file, so find it by name in the Files tab"
            },
            // Terraria is one of the games the registry gives no NexusName or Nexus game id, so it keeps the Files
            // tab link; without the fallback this would be https://www.nexusmods.com//mods/2547, which resolves to
            // nothing. The domain is worked out from the game's name and is therefore a good guess rather than a
            // fact, so the card says so instead of promising the page is there.
            new object[]
            {
                new Nexus {Game = Game.Terraria, ModID = 2547, FileID = 10432},
                "https://www.nexusmods.com/terraria/mods/2547?tab=files&file_id=10432", "Nexus Mods",
                "Wabbajack has no Nexus address recorded for this game, so if this page doesn't open, search Nexus Mods for the file by name"
            },
            new object[]
            {
                new Nexus {Game = Game.KarrynsPrison, ModID = 61},
                "https://www.nexusmods.com/karrynsprison/mods/61?tab=files", "Nexus Mods",
                "Wabbajack has no Nexus address recorded for this game and this list doesn't record which file, so you may have to search Nexus Mods for it by name"
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

    /// <summary>
    ///     No game may produce a Nexus URL that resolves to nothing. Every game in the registry bar two has
    ///     a NexusName; the two that do not fall back to their own name, and the thing to catch is an empty
    ///     domain leaving "https://www.nexusmods.com//mods/1".
    /// </summary>
    [Fact]
    public void EveryGameProducesAReachableNexusFilePage()
    {
        foreach (var game in GameRegistry.Games.Keys)
        {
            Assert.True(ManualDownloadUrls.TryGet(new Nexus {Game = game, ModID = 1, FileID = 2}, out var target));

            var url = target!.Url;
            Assert.Equal("www.nexusmods.com", url.Host);
            Assert.DoesNotContain("//mods/", url.AbsolutePath + url.Query);
            if (game.MetaData().NexusGameId > 0)
            {
                Assert.Equal("/Core/Libs/Common/Widgets/DownloadPopUp", url.AbsolutePath);
                Assert.Equal($"?id=2&game_id={game.MetaData().NexusGameId}", url.Query);
            }
            else
            {
                Assert.EndsWith("/mods/1", url.AbsolutePath);
                Assert.Equal("?tab=files&file_id=2", url.Query);
            }
        }
    }

    /// <summary>
    ///     The direct download page is what saves the user two clicks per file, and the Files tab is what they
    ///     got before it. Nearly every game has the numeric id the direct page needs, so a registry change that
    ///     dropped them would quietly put the clicks back; this keeps the ones without an id to the few known.
    /// </summary>
    [Fact]
    public void NearlyEveryGameGetsTheDirectDownloadPage()
    {
        var withoutId = GameRegistry.Games.Where(g => g.Value.NexusGameId <= 0).Select(g => g.Key).ToList();

        Assert.True(withoutId.Count <= 3, $"Games without a Nexus game id: {string.Join(", ", withoutId)}");
    }

    /// <summary>
    ///     The mod link on the state itself and the file link built here are two different pages, but they
    ///     have to agree about which game they are on: both go through <see cref="GameMetaData.NexusDomain" />
    ///     so that the empty-domain case cannot be fixed in one and left in the other.
    /// </summary>
    [Fact]
    public void TheModLinkAndTheFileLinkAgreeOnTheGame()
    {
        foreach (var game in GameRegistry.Games.Keys)
        {
            // Without a file id there is no download page to go to, so the file link is the Files tab and has
            // to name the same game as the mod link.
            var state = new Nexus {Game = game, ModID = 7};
            Assert.True(ManualDownloadUrls.TryGet(state, out var target));

            Assert.Equal($"https://www.nexusmods.com/{game.MetaData().NexusDomain}/mods/7", state.LinkUrl!.AbsoluteUri);
            Assert.StartsWith($"https://www.nexusmods.com/{game.MetaData().NexusDomain}/mods/7?", target!.Url.AbsoluteUri);
            Assert.DoesNotContain("//mods/", state.LinkUrl.AbsolutePath);
        }
    }

    /// <summary>
    ///     What a host hands the browser has to be the whole URL. This is the shape the owner asked for,
    ///     checked through <see cref="Uri.AbsoluteUri" /> because that is what the shell is given: the
    ///     failure this pins was a host passing the URL through <c>cmd /c start</c>, where an unquoted
    ///     <c>&amp;</c> is a command separator and the link arrived as the mod's Files tab with no file on it.
    /// </summary>
    [Fact]
    public void TheNexusFileLinkSurvivesAsOnePiece()
    {
        Assert.True(ManualDownloadUrls.TryGet(
            new Nexus {Game = Game.SkyrimSpecialEdition, ModID = 266, FileID = 209150}, out var target));

        Assert.Equal("https://www.nexusmods.com/Core/Libs/Common/Widgets/DownloadPopUp?id=209150&game_id=1704",
            target!.Url.AbsoluteUri);
    }
}
