using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Wabbajack.Common;
using Wabbajack.DTOs;
using Wabbajack.DTOs.DownloadStates;
using Wabbajack.Installer.Preflight.Rules;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;

namespace Wabbajack.Installer.Preflight.Checks;

/// <summary>
///     Works out which archives this install needs (a dry run of the installer's pruning, so updating an
///     existing install does not demand archives it will never read) and which of those are already on
///     disk. Always passes: what is missing is for the download checks to deal with.
/// </summary>
public sealed class ArchiveInventoryCheck : IPreflightCheck
{
    public string Id => PreflightCheckIds.ArchiveInventory;
    public string Title => "Archives on disk";
    public int Order => 300;
    public IReadOnlyList<string> DependsOn => new[] {PreflightCheckIds.GameInstalled};

    public async Task<PreflightResult> Run(PreflightContext ctx, IPreflightProgress progress, CancellationToken token)
    {
        ctx.Config.Downloads.CreateDirectory();

        progress.Report(0, 0, "Working out which archives this install needs");
        var required = await RequiredArchives.Compute(ctx.ModList, ctx.Config.Install, ctx.HashCache, ctx.Limiter,
            token, onChecked: (done, total) =>
                progress.Report(done, total, "Checking what is already installed"));
        ctx.State.RequiredArchives = required;

        var gameFolders = new List<AbsolutePath>();
        if (ctx.State.GameFolder != default) gameFolders.Add(ctx.State.GameFolder);
        gameFolders.AddRange(ctx.State.OtherGameFolders.Values.Where(p => p != default));

        var total = 0;
        var done = 0L;
        var byHash = await ArchiveInventory.Scan(required, ctx.Config.Downloads, gameFolders, ctx.HashCache,
            ctx.Limiter, ctx.Logger, token,
            onFilesToHash: count =>
            {
                total = count;
                progress.Report(0, count, "Hashing archives");
            },
            onFileHashed: () => progress.Report(Interlocked.Increment(ref done), total, "Hashing archives"));

        ctx.State.HashedArchives.Clear();
        var missing = new List<Archive>();
        var present = 0;
        foreach (var archive in required)
        {
            if (byHash.TryGetValue(archive.Hash, out var path))
            {
                present++;
                ctx.State.HashedArchives[archive.Name] = path;
                progress.Archive(archive, ArchiveState.Present, path.ToString());
            }
            else
            {
                progress.Archive(archive, ArchiveState.Missing);
                if (archive.State is not GameFileSource)
                    missing.Add(archive);
            }
        }

        ctx.State.Missing = missing;
        ctx.State.RemainingDownloadBytes = missing.Sum(a => a.Size);

        var message = present == required.Length
            ? $"All {Plural.Of(required.Length, "archive")} present"
            : $"{present} of {Plural.Of(required.Length, "archive")} present, {missing.Count} to download " +
              $"({ctx.State.RemainingDownloadBytes.ToFileSizeString()})";
        return PreflightResult.Passed(message);
    }
}
