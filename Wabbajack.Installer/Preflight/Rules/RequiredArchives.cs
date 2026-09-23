using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Wabbajack.Common;
using Wabbajack.DTOs;
using Wabbajack.DTOs.Directives;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Wabbajack.RateLimiter;
using Wabbajack.VFS;

namespace Wabbajack.Installer.Preflight.Rules;

/// <summary>
///     Which archives an install will actually read once what is already on disk is taken into account.
///     These are the non-destructive halves of <c>AInstaller.OptimizeModlist</c>, split so the installer can
///     run its deletion phases between them while preflight runs them back to back as a dry run. Both
///     callers share this code so the two cannot drift.
/// </summary>
public static class RequiredArchives
{
    public sealed class Plan
    {
        public Plan(Dictionary<RelativePath, Directive> indexed, HashSet<RelativePath> bsasToNotBuild,
            HashSet<AbsolutePath> bsaPathsToNotBuild)
        {
            Indexed = indexed;
            BsasToNotBuild = bsasToNotBuild;
            BsaPathsToNotBuild = bsaPathsToNotBuild;
        }

        /// <summary>Directives still to install, keyed by destination.</summary>
        public Dictionary<RelativePath, Directive> Indexed { get; }

        /// <summary>TempIDs of BSAs that already exist with the right hash.</summary>
        public HashSet<RelativePath> BsasToNotBuild { get; }

        /// <summary>Full paths of those BSAs, for the deletion rules to leave alone.</summary>
        public HashSet<AbsolutePath> BsaPathsToNotBuild { get; }
    }

    /// <summary>
    ///     Drops every CreateBSA whose output already exists with the expected hash, along with the
    ///     FromArchive directives that only exist to feed it.
    /// </summary>
    public static async Task<Plan> PruneBuiltBsas(ModList modList, AbsolutePath install, FileHashCache hashCache,
        CancellationToken token)
    {
        var indexed = modList.Directives.ToDictionary(d => d.To);

        var bsasToBuild = await modList.Directives
            .OfType<CreateBSA>()
            .PMapAll(async b =>
            {
                var file = install.Combine(b.To);
                if (!file.FileExists())
                    return (true, b);
                return (b.Hash != await hashCache.FileHashCachedAsync(file, token), b);
            })
            .ToArray();

        var bsasToNotBuild = bsasToBuild
            .Where(b => b.Item1 == false).Select(t => t.b.TempID).ToHashSet();

        var bsaPathsToNotBuild = bsasToBuild
            .Where(b => b.Item1 == false).Select(t => t.b.To.RelativeTo(install))
            .ToHashSet();

        // Both of these were being rebuilt inside the filter: the prefix string once per directive, and one
        // path per not-built BSA per directive under the build folder. A list has hundreds of thousands of
        // directives, so they are built once here instead.
        var creationDir = Consts.BSACreationDir.ToString();
        var notBuiltFolders = bsasToNotBuild
            .Select(b => install.Combine(Consts.BSACreationDir, b))
            .ToList();

        indexed = indexed.Values
            .Where(d =>
            {
                return d switch
                {
                    CreateBSA bsa => !bsasToNotBuild.Contains(bsa.TempID),
                    FromArchive a when a.To.StartsWith(creationDir) => !FeedsABsaToNotBuild(a, install,
                        notBuiltFolders),
                    _ => true
                };
            }).ToDictionary(d => d.To);

        return new Plan(indexed, bsasToNotBuild, bsaPathsToNotBuild);
    }

    /// <summary>Whether this directive only exists to feed a BSA that is not going to be built.</summary>
    private static bool FeedsABsaToNotBuild(FromArchive directive, AbsolutePath install,
        List<AbsolutePath> notBuiltFolders)
    {
        if (notBuiltFolders.Count == 0) return false;

        var destination = directive.To.RelativeTo(install);
        foreach (var folder in notBuiltFolders)
            if (destination.InFolder(folder))
                return true;

        return false;
    }

    /// <summary>
    ///     Removes from <paramref name="indexed" /> every directive whose destination already holds a file
    ///     with the expected hash.
    /// </summary>
    /// <param name="onChecked">
    ///     Called with (checked, total) after each directive, from any thread. An update run hashes every file
    ///     already installed here, so on a big list this is the longest quiet stretch preflight has.
    /// </param>
    public static async Task PruneUnmodified(Dictionary<RelativePath, Directive> indexed, AbsolutePath install,
        FileHashCache hashCache, IResource<IInstaller> limiter, CancellationToken token,
        Action<long, long>? onChecked = null)
    {
        var existingfiles = install.DirectoryExists()
            ? install.EnumerateFiles().ToHashSet()
            : new HashSet<AbsolutePath>();

        var total = (long) indexed.Count;
        var done = 0L;

        await indexed.Values.PMapAllBatchedAsync(limiter, async d =>
            {
                // Bit backwards, but we want to return null for
                // all files we *want* installed. We return the files
                // to remove from the install list.
                try
                {
                    var path = install.Combine(d.To);
                    if (!existingfiles.Contains(path)) return null;

                    return await hashCache.FileHashCachedAsync(path, token) == d.Hash ? d : null;
                }
                finally
                {
                    onChecked?.Invoke(Interlocked.Increment(ref done), total);
                }
            })
            .Do(d =>
            {
                if (d != null)
                {
                    indexed.Remove(d.To);
                }
            });
    }

    /// <summary>The hashes of every archive the given directives extract from.</summary>
    public static HashSet<Hash> RequiredHashes(IEnumerable<Directive> directives)
    {
        return directives.OfType<FromArchive>()
            .GroupBy(d => d.ArchiveHashPath.Hash)
            .Select(d => d.Key)
            .ToHashSet();
    }

    /// <summary>
    ///     Dry run of the whole pruning: the subset of <paramref name="modList" />'s archives an install into
    ///     <paramref name="install" /> would read. Touches nothing on disk beyond the hash cache.
    /// </summary>
    public static async Task<Archive[]> Compute(ModList modList, AbsolutePath install, FileHashCache hashCache,
        IResource<IInstaller> limiter, CancellationToken token, Action<long, long>? onChecked = null)
    {
        var plan = await PruneBuiltBsas(modList, install, hashCache, token);
        await PruneUnmodified(plan.Indexed, install, hashCache, limiter, token, onChecked);
        var required = RequiredHashes(plan.Indexed.Values);
        return modList.Archives.Where(a => required.Contains(a.Hash)).ToArray();
    }
}
