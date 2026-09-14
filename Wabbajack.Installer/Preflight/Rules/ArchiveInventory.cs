using System;
using System.Collections.Generic;
using System.IO;
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
    ///     logged and left out rather than thrown on, unless <paramref name="throwOnMissingOtherGame" /> is
    ///     set: the installer keeps its historical behaviour of failing on an <c>OtherGames</c> entry that
    ///     is not installed, while preflight reports the missing game and continues.
    /// </summary>
    public static IReadOnlyCollection<AbsolutePath> GameFolders(InstallerConfiguration config, IGameLocator locator,
        ILogger logger, bool throwOnMissingOtherGame = false)
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
            if (throwOnMissingOtherGame)
                AddIfValid(locator.GameLocation(g));
            else if (locator.TryFindLocation(g, out var other))
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
        // Size and last-modified come off the directory scan rather than being asked for again per file.
        // A downloads folder is tens of thousands of entries and each of Size(), LastModified() and
        // FileExists() is a separate call into the file system; on a network share or a spinning disk that
        // is most of what this method used to cost.
        var allFiles = Enumerate(downloads)
            .Concat(gameFolders.Where(p => p.DirectoryExists()).SelectMany(Enumerate))
            .ToList();

        logger.LogInformation("Getting archive sizes");
        var hashDict = allFiles
            .GroupBy(f => f.Size)
            .ToDictionary(g => g.Key, g => g.ToList());

        logger.LogInformation("Linking archives to downloads");
        // Two archives of the same size point at the same candidate files, so the same file would
        // otherwise be queued - and hashed - once per archive that matches its size.
        var seen = new HashSet<AbsolutePath>();
        var toHash = new List<FoundFile>();
        foreach (var archive in archives)
        {
            if (!hashDict.TryGetValue(archive.Size, out var candidates)) continue;
            foreach (var candidate in candidates)
                if (seen.Add(candidate.Path))
                    toHash.Add(candidate);
        }

        onFilesToHash?.Invoke(toHash.Count);
        logger.LogInformation("Found {count} total files, {hashedCount} matching filesize",
            allFiles.Count, toHash.Count);

        // Under the installer's limiter, as the rest of the pass is: unbounded hashing starts a read per
        // candidate file at once, which on one disk is slower than doing them a few at a time.
        var hashResults = await toHash.PMapAll(limiter, async e =>
        {
            var hash = await hashCache.FileHashCachedAsync(e.Path, token);
            // After the hash, not before: reported first, the whole queue is announced as done the moment
            // it is built and the check then sits at 100% for as long as the hashing actually takes.
            onFileHashed?.Invoke();
            return (Hash: hash, File: e);
        }).ToList();

        return hashResults
            .OrderByDescending(e => e.File.LastModified)
            .GroupBy(e => e.Hash)
            .Select(e => e.First())
            .Where(x => x.Hash != default)
            .ToDictionary(kv => kv.Hash, kv => kv.File.Path);
    }

    /// <summary>What one directory scan already knows about a file, so nothing has to be asked again.</summary>
    private readonly record struct FoundFile(AbsolutePath Path, long Size, DateTime LastModified);

    /// <summary>
    ///     Every file under <paramref name="folder" />, skipping any whose path does not survive the
    ///     <see cref="AbsolutePath" /> round-trip (UNC and device paths such as <c>\\.\nul</c>), which is
    ///     what the <c>FileExists</c> filter here used to be for.
    /// </summary>
    private static IEnumerable<FoundFile> Enumerate(AbsolutePath folder)
    {
        foreach (var info in new DirectoryInfo(folder.ToString())
                     .EnumerateFiles("*", SearchOption.AllDirectories))
        {
            var path = info.FullName.ToAbsolutePath();
            if (!path.FileExists()) continue;
            yield return new FoundFile(path, info.Length, info.LastWriteTime);
        }
    }
}
