using System;
using System.Collections.Generic;
using System.Linq;
using Wabbajack.App.Avalonia.ViewModels.Gallery;
using Wabbajack.DTOs;
using Xunit;

namespace Wabbajack.App.Avalonia.Test.Gallery;

/// <summary>
///     Which lists the gallery shows for each setting of its filter panel. The game dropdown's counts are taken
///     with every rule but the game's, through the same functions, so they agree with what choosing a game shows;
///     they used to be a second copy of the rules kept in step by hand.
/// </summary>
public class GalleryFilterTests
{
    private static ModlistMetadata List(string machineUrl, Game game = Game.SkyrimSpecialEdition, bool official = true,
        bool nsfw = false, long size = 10, string title = "A list", string description = "", params string[] tags)
    {
        return new ModlistMetadata
        {
            Title = title, Description = description, Game = game, Official = official, NSFW = nsfw,
            Tags = tags.ToList(), Links = new LinksObject {MachineURL = machineUrl},
            DownloadMetadata = new DownloadMetadata {SizeOfArchives = size, SizeOfInstalledFiles = 0}
        };
    }

    private static readonly Dictionary<string, HashSet<string>> Mods = new()
    {
        ["has-both"] = new() {"SkyUI", "USSEP"},
        ["has-skyui"] = new() {"SkyUI"}
    };

    [Theory]
    [InlineData("", true)]
    [InlineData("living", true)]
    [InlineData("SKYRIM", true)]
    [InlineData("survival", true)]
    [InlineData("Hardcore", true)]
    [InlineData("hardcore", false)]
    [InlineData("fallout", false)]
    public void Search_LooksAtTitleAndDescriptionCaselessly_AndTagsExactly(string search, bool shown)
    {
        var list = List("ls", title: "Living Skyrim", description: "A survival list", tags: "Hardcore");

        Assert.Equal(shown, GalleryFilter.MatchesSearch(list, search));
    }

    [Fact]
    public void UnofficialAndNsfw_AreHiddenUnlessAskedFor()
    {
        var unofficial = List("u", official: false);
        var nsfw = List("n", nsfw: true);

        Assert.False(GalleryFilter.MatchesOfficial(unofficial, includeUnofficial: false));
        Assert.True(GalleryFilter.MatchesOfficial(unofficial, includeUnofficial: true));
        Assert.False(GalleryFilter.MatchesNsfw(nsfw, includeNsfw: false));
        Assert.True(GalleryFilter.MatchesNsfw(nsfw, includeNsfw: true));
    }

    [Fact]
    public void OnlyInstalled_AsksTheLocator()
    {
        var list = List("fo4", Game.Fallout4);

        Assert.True(GalleryFilter.MatchesInstalled(list, onlyInstalled: false, _ => false));
        Assert.False(GalleryFilter.MatchesInstalled(list, onlyInstalled: true, g => g != Game.Fallout4));
        Assert.True(GalleryFilter.MatchesInstalled(list, onlyInstalled: true, g => g == Game.Fallout4));
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData(GalleryFilter.AllGames, true)]
    [InlineData("Skyrim Special Edition", true)]
    [InlineData("Fallout 4", false)]
    public void Game_MatchesByItsName(string? selected, bool shown)
    {
        Assert.Equal(shown, GalleryFilter.MatchesGame(List("s"), selected));
    }

    [Theory]
    [InlineData(5, 15, true)]
    [InlineData(10, 10, true)]
    [InlineData(11, 20, false)]
    [InlineData(0, 9, false)]
    public void Size_IsInclusive(double min, double max, bool shown)
    {
        Assert.Equal(shown, GalleryFilter.MatchesSize(List("s", size: 10), min, max));
    }

    [Fact]
    public void Tags_AListNeedsEveryOneChosen()
    {
        var list = List("s", tags: new[] {"Hardcore", "Survival"});

        Assert.True(GalleryFilter.MatchesTags(list, Array.Empty<string>()));
        Assert.True(GalleryFilter.MatchesTags(list, new[] {"Hardcore"}));
        Assert.False(GalleryFilter.MatchesTags(list, new[] {"Hardcore", "VR"}));
    }

    [Theory]
    [InlineData("has-both", false, true)]
    [InlineData("has-skyui", false, false)]
    [InlineData("unindexed", false, false)]
    [InlineData("has-both", true, false)]
    [InlineData("has-skyui", true, false)]
    [InlineData("unindexed", true, true)]
    public void Mods_IncludeNeedsAllAndExcludeNeedsNone(string machineUrl, bool exclude, bool shown)
    {
        Assert.Equal(shown, GalleryFilter.MatchesMods(List(machineUrl), Mods, new[] {"SkyUI", "USSEP"}, exclude));
    }

    [Fact]
    public void Mods_NoneChosen_ShowsEverything()
    {
        Assert.True(GalleryFilter.MatchesMods(List("unindexed"), Mods, Array.Empty<string>(), exclude: false));
    }

    /// <summary>
    ///     The per-game counts and the list are the same rules: for any criteria, what the counts keep for a game
    ///     is what the list shows once that game is chosen.
    /// </summary>
    [Fact]
    public void Counts_AgreeWithWhatChoosingTheGameShows()
    {
        var lists = new[]
        {
            List("has-both", tags: "Hardcore"), List("has-skyui", Game.Fallout4, tags: "Hardcore"),
            List("unindexed", official: false), List("n", nsfw: true, tags: "Hardcore"),
            List("big", size: 1000, tags: "Hardcore")
        };
        var criteria = new GalleryCriteria("", false, false, false, 0, 100, new[] {"Hardcore"}, new[] {"SkyUI"}, false);

        foreach (var game in new[] {Game.SkyrimSpecialEdition, Game.Fallout4})
        {
            var counted = lists.Count(l => l.Game == game &&
                                           GalleryFilter.MatchesAllButGame(l, criteria, _ => true, Mods));
            var shown = lists.Count(l =>
                GalleryFilter.MatchesGame(l, game.MetaData().HumanFriendlyGameName) &&
                GalleryFilter.MatchesSearch(l, criteria.Search) && GalleryFilter.MatchesOfficial(l, criteria.IncludeUnofficial) &&
                GalleryFilter.MatchesNsfw(l, criteria.IncludeNsfw) && GalleryFilter.MatchesSize(l, criteria.MinSize, criteria.MaxSize) &&
                GalleryFilter.MatchesTags(l, criteria.Tags) && GalleryFilter.MatchesMods(l, Mods, criteria.Mods, criteria.ExcludeMods));

            Assert.Equal(shown, counted);
            Assert.Equal(1, counted);
        }
    }
}
