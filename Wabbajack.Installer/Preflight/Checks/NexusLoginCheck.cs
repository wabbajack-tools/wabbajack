using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Wabbajack.Common;
using Wabbajack.DTOs.DownloadStates;

namespace Wabbajack.Installer.Preflight.Checks;

/// <summary>
///     Only matters when the list has Nexus archives. A free account still passes: those files are fetched
///     by hand later, and the blackboard records <c>IsPremium = false</c> so the download checks route them.
/// </summary>
public sealed class NexusLoginCheck : IPreflightCheck
{
    public string Id => PreflightCheckIds.NexusLogin;
    public string Title => "Nexus Mods login";
    public int Order => 100;
    public IReadOnlyList<string> DependsOn => Array.Empty<string>();

    public async Task<PreflightResult> Run(PreflightContext ctx, IPreflightProgress progress, CancellationToken token)
    {
        var nexus = ctx.ModList.Archives.Where(a => a.State is Nexus).ToArray();
        if (nexus.Length == 0)
        {
            ctx.State.Nexus = null;
            return PreflightResult.Passed("Not needed, this list has no Nexus Mods files");
        }

        var status = await ctx.NexusLogin.Probe(token);
        ctx.State.Nexus = status;

        var count = nexus.Length;
        var size = nexus.Sum(a => a.Size).ToFileSizeString();

        if (!status.HasToken)
            return PreflightResult.NeedsUser($"Log in to Nexus Mods to download {count} files ({size})",
                actions: new[] {PreflightAction.Login});

        if (!status.LoggedIn)
            return PreflightResult.NeedsUser("Your Nexus Mods login has expired, log in again", status.Error,
                new[] {PreflightAction.Login});

        var name = status.UserName ?? "Nexus Mods user";
        if (status.IsPremium)
            return PreflightResult.Passed($"Logged in as {name} (Premium)");

        return PreflightResult.Passed(
            $"Logged in as {name} - {count} Nexus files will be downloaded manually ({size})");
    }
}
