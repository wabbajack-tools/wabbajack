using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wabbajack.DTOs;
using Wabbajack.Installer.Preflight.Rules;

namespace Wabbajack.Installer.Preflight.Checks;

/// <summary>
///     Refuses a modlist that takes any file from a host on Bans.yml. It runs first and asks about every archive
///     in the list, not just the missing ones: a ban is a judgement on the list, and a user who already has the
///     file is installing it just the same. Nothing the user can do on this machine changes the answer, so it
///     fails rather than asking them to act.
///     <para>
///         A ban list that will not load lets the install go ahead. It is one file on GitHub, and every install
///         stopping whenever raw.githubusercontent.com does would be a worse failure than the one it guards
///         against; the compiler's own validation is the other side of this.
///     </para>
/// </summary>
public class BannedSourcesCheck : IPreflightCheck
{
    public string Id => PreflightCheckIds.BannedSources;
    public string Title => "Banned sources";
    public int Order => 50;
    public IReadOnlyList<string> DependsOn => Array.Empty<string>();

    public async Task<PreflightResult> Run(PreflightContext ctx, IPreflightProgress progress, CancellationToken token)
    {
        IReadOnlyList<string> bans;
        try
        {
            bans = BannedSources.Normalize((await ctx.DownloadPolicy.Bans(token)).Bans);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            ctx.Logger.LogWarning(ex, "Could not load the list of banned sources");
            return PreflightResult.Passed("Could not load the list of banned sources",
                $"The install will go ahead without it: {ex.Message}");
        }

        var banned = new List<(Archive Archive, string Ban)>();
        foreach (var archive in ctx.ModList.Archives)
        {
            var ban = BannedSources.BanFor(archive.State, bans);
            if (ban == null) continue;
            banned.Add((archive, ban));
            progress.Archive(archive, ArchiveState.Unsupported, $"From {ban}, which Wabbajack does not install from");
        }

        if (banned.Count == 0)
            return PreflightResult.Passed("No files from banned sources");

        foreach (var (archive, ban) in banned)
            ctx.Logger.LogError("{Archive} comes from {Ban}, which is banned", archive.Name, ban);

        var hosts = banned.Select(b => b.Ban).Distinct().ToList();
        var detail = "Wabbajack will not install lists that take files from " + string.Join(", ", hosts) +
                     ". Sources are banned for rehosting other people's work or for copyright violations, so " +
                     "the list's author has to replace these files:" + Environment.NewLine +
                     string.Join(Environment.NewLine, banned.Select(b => $"{b.Archive.Name}: {b.Ban}"));
        return PreflightResult.Failed(
            $"This list takes {Plural.Of(banned.Count, "file")} from a banned source and cannot be installed",
            detail);
    }
}
