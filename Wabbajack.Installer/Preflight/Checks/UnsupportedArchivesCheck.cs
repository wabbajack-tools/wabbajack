using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Wabbajack.DTOs;
using Wabbajack.DTOs.DownloadStates;

namespace Wabbajack.Installer.Preflight.Checks;

/// <summary>
///     Missing archives nothing can fetch. Creation Club content has to be installed through the game;
///     legacy sources no longer exist. These are removed from <c>Missing</c> so the download checks do not
///     try them, and the check fails so the user knows why the install cannot proceed.
/// </summary>
public class UnsupportedArchivesCheck : IPreflightCheck
{
    public string Id => PreflightCheckIds.UnsupportedArchives;
    public string Title => "Unsupported archives";
    public int Order => 500;
    public IReadOnlyList<string> DependsOn => new[] {PreflightCheckIds.ArchiveInventory};

    public Task<PreflightResult> Run(PreflightContext ctx, IPreflightProgress progress, CancellationToken token)
    {
        var unsupported = new List<(Archive Archive, string Reason)>();
        foreach (var archive in ctx.State.Missing)
        {
            var reason = UnsupportedReason(archive);
            if (reason == null) continue;
            unsupported.Add((archive, reason));
            progress.Archive(archive, ArchiveState.Unsupported, reason);
        }

        if (unsupported.Count == 0)
            return Task.FromResult(PreflightResult.Passed("No unsupported archives"));

        var removed = unsupported.Select(u => u.Archive).ToHashSet();
        ctx.State.Missing = ctx.State.Missing.Where(a => !removed.Contains(a)).ToList();
        ctx.State.RemainingDownloadBytes = ctx.State.Missing.Sum(a => a.Size);

        var creationClub = unsupported.Count(u => u.Archive.State is Bethesda);
        var other = unsupported.Count - creationClub;

        var parts = new List<string>();
        if (creationClub > 0)
            parts.Add($"{creationClub} Creation Club items must be installed through the game before installing this list");
        if (other > 0)
            parts.Add($"{other} archives come from an unsupported source and cannot be downloaded");

        var detail = string.Join(Environment.NewLine, unsupported.Select(u => $"{u.Archive.Name}: {u.Reason}"));
        return Task.FromResult(PreflightResult.Failed(string.Join(". ", parts), detail));
    }

    /// <summary>
    ///     Why nothing can fetch this archive, or null when something can. Extend here when a new class of
    ///     source is ruled out; for example an archive with no page a user could be sent to.
    /// </summary>
    protected virtual string? UnsupportedReason(Archive archive)
    {
        return archive.State switch
        {
            Bethesda => "Creation Club content, install it through the game",
            DeprecatedLoversLab => "Legacy LoversLab source, no longer available",
            TESAlliance => "TES Alliance source, no longer available",
            _ => null
        };
    }
}
