using System;
using System.Collections.Generic;
using System.Linq;
using Wabbajack.Common;
using Wabbajack.DTOs;

namespace Wabbajack.App.Avalonia.ViewModels.Gallery;

/// <summary>
///     Everything the gallery's filter panel is set to, except the game, which the game dropdown's counts are
///     taken without.
/// </summary>
public sealed record GalleryCriteria(
    string Search,
    bool OnlyInstalled,
    bool IncludeUnofficial,
    bool IncludeNsfw,
    double MinSize,
    double MaxSize,
    IReadOnlyCollection<string> Tags,
    IReadOnlyCollection<string> Mods,
    bool ExcludeMods);

/// <summary>
///     Which lists the gallery shows. The list itself filters on each rule as it changes, and the game dropdown
///     counts what every rule but the game would leave; both go through these, so a count never disagrees with
///     what choosing that game then shows.
/// </summary>
public static class GalleryFilter
{
    public const string AllGames = "All games";

    public static bool MatchesSearch(ModlistMetadata list, string search) =>
        string.IsNullOrWhiteSpace(search) ||
        list.Title.ContainsCaseInsensitive(search) ||
        list.Description.ContainsCaseInsensitive(search) ||
        list.Tags.Contains(search);

    public static bool MatchesInstalled(ModlistMetadata list, bool onlyInstalled, Func<Game, bool> isInstalled) =>
        !onlyInstalled || isInstalled(list.Game);

    public static bool MatchesOfficial(ModlistMetadata list, bool includeUnofficial) =>
        includeUnofficial || list.Official;

    public static bool MatchesNsfw(ModlistMetadata list, bool includeNsfw) =>
        includeNsfw || !list.NSFW;

    public static bool MatchesGame(ModlistMetadata list, string? game) =>
        game is null or AllGames || list.Game.MetaData().HumanFriendlyGameName == game;

    public static bool MatchesSize(ModlistMetadata list, double min, double max) =>
        list.DownloadMetadata!.TotalSize >= min && list.DownloadMetadata.TotalSize <= max;

    /// <summary>A list has to carry every tag chosen.</summary>
    public static bool MatchesTags(ModlistMetadata list, IReadOnlyCollection<string> tags) =>
        tags.Count == 0 || tags.All(tag => list.Tags.Contains(tag));

    /// <summary>
    ///     With mods chosen, a list has to contain all of them, or in exclude mode none of them. A list the mod
    ///     index knows nothing about contains nothing.
    /// </summary>
    public static bool MatchesMods(ModlistMetadata list, IReadOnlyDictionary<string, HashSet<string>> modsPerList,
        IReadOnlyCollection<string> mods, bool exclude)
    {
        if (mods.Count == 0) return true;
        var known = modsPerList.TryGetValue(list.Links.MachineURL, out var contains);
        return exclude
            ? !known || !mods.Any(contains!.Contains)
            : known && mods.All(contains!.Contains);
    }

    public static bool MatchesAllButGame(ModlistMetadata list, GalleryCriteria c, Func<Game, bool> isInstalled,
        IReadOnlyDictionary<string, HashSet<string>> modsPerList) =>
        MatchesSearch(list, c.Search) &&
        MatchesInstalled(list, c.OnlyInstalled, isInstalled) &&
        MatchesOfficial(list, c.IncludeUnofficial) &&
        MatchesNsfw(list, c.IncludeNsfw) &&
        MatchesSize(list, c.MinSize, c.MaxSize) &&
        MatchesTags(list, c.Tags) &&
        MatchesMods(list, modsPerList, c.Mods, c.ExcludeMods);
}
