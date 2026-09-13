using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wabbajack.Common;
using Wabbajack.Downloaders.GameFile;
using Wabbajack.DTOs;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Wabbajack.RateLimiter;
using Wabbajack.VFS;

namespace Wabbajack.Installer.Preflight.Rules;

/// <summary>
///     Finds which of a list's archives are already on disk. Files are matched by size first and only
///     size matches are hashed, so a downloads folder of hundreds of gigabytes costs a directory listing
///     plus whatever the hash cache does not already know.
/// </summary>
public static class ArchiveInventory
{
    /// <summary>
    ///     The game folders to search alongside the downloads folder: the configured (or located) primary
    ///     game plus every <c>OtherGames</c> entry that can be located. A game that cannot be found is
    ///     logged and left out rather than thrown on.
    /// </summary>
    public static IReadOnlyCollection<AbsolutePath> GameFolders(InstallerConfiguration config, IGameLocator locator,
        ILogger logger)
    {
        var folders = new HashSet<AbsolutePath>();

        void AddIfValid(AbsolutePath p)
        {
            if (p != default && p != AbsolutePath.Empty)
                folders.Add(p);
        }

        if (config.GameFolder != default)
            AddIfValid(config.GameFolder);
        else if (locator.TryFindLocation(config.Game, out var primary))
            AddIfValid(primary);

        // .othergames should only be non-null if the compiled list specifically named othergames
        foreach (var g in config.OtherGames ?? Array.Empty<Game>())
        {
            logger.LogInformation("Also searching othergame folder for {Game}", g);
            if (locator.TryFindLocation(g, out var other))
                AddIfValid(other);
            else
                logger.LogWarning("Other game {Game} is not installed, its folder will not be searched", g);
        }

        return folders;
    }

    /// <summary>
    ///     Returns hash -> path for every archive found. When several files share a hash the most recently
    ///     modified wins.
    /// </summary>
    /// <param name="onFilesToHash">Called once with the number of files that will be hashed.</param>
    /// <param name="onFileHashed">Called after each file is hashed.</param>
    public static async Task<Dictionary<Hash, AbsolutePath>> Scan(IReadOnlyCollection<Archive> archives,
        AbsolutePath downloads, IEnumerable<AbsolutePath> gameFolders, FileHashCache hashCache,
        IResource<IInstaller> limiter, ILogger logger, CancellationToken token,
        Action<int>? onFilesToHash = null, Action? onFileHashed = null)
    {
        // Enumerate downloads + every game folder, filtering out any paths that
        // don't survive the AbsolutePath round-trip (e.g. UNC/device paths like \\.\nul)
        var allFiles = downloads.EnumerateFiles()
            .Concat(gameFolders.Where(p => p.DirectoryExists()).SelectMany(p => p.EnumerateFiles()))
            .Where(f => f.FileExists())
            .ToList();

        logger.LogInformation("Getting archive sizes");
        var hashDict = (await allFiles.PMapAllBatched(limiter,
                x => (x, x.Size())).ToList())
            .GroupBy(f => f.Item2)
            .ToDictionary(g => g.Key, g => g.Select(v => v.x));

        logger.LogInformation("Linking archives to downloads");
        var toHash = archives.Where(a => hashDict.ContainsKey(a.Size))
            .SelectMany(a => hashDict[a.Size])
            .ToList();

        onFilesToHash?.Invoke(toHash.Count);
        logger.LogInformation("Found {count} total files, {hashedCount} matching filesize",
            allFiles.Count, toHash.Count);

        var hashResults = await toHash.PMapAll(async e =>
        {
            onFileHashed?.Invoke();
            return (await hashCache.FileHashCachedAsync(e, token), e);
        }).ToList();

        return hashResults
            .OrderByDescending(e => e.Item2.LastModified())
            .GroupBy(e => e.Item1)
            .Select(e => e.First())
            .Where(x => x.Item1 != default)
            .ToDictionary(kv => kv.Item1, kv => kv.e);
    }
}
