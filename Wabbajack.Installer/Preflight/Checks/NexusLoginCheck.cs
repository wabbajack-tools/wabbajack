using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Wabbajack.Common;
using Wabbajack.DTOs;
using Wabbajack.DTOs.DownloadStates;
using Wabbajack.Networking.NexusApi;

namespace Wabbajack.Installer.Preflight.Checks;

/// <summary>
///     Only matters when this install still has Nexus archives to fetch. A free account still passes: those
///     files are fetched by hand later, and the blackboard records <c>IsPremium = false</c> so the download
///     checks route them.
///     <para>
///         It runs after the inventory and after unsupported-archives, not first, because a login the user
///         does not need must not stop their run. A <c>NeedsUser</c> here halts the checklist, and asking
///         before anything has been counted meant halting a user whose downloads folder was already complete,
///         over files nobody was going to fetch, before they had learned whether their game was even
///         installed. Asked here, the question is about <see cref="PreflightBlackboard.Missing" /> - what is
///         actually left to download, minus what unsupported-archives has already ruled out - so a run only
///         stops for a login that is genuinely in the way, and the count in the message is the one the user
///         would recognise.
///     </para>
/// </summary>
public sealed class NexusLoginCheck : IPreflightCheck
{
    public string Id => PreflightCheckIds.NexusLogin;
    public string Title => "Nexus Mods login";
    public int Order => 500;

    public IReadOnlyList<string> DependsOn => new[]
    {
        PreflightCheckIds.ArchiveInventory, PreflightCheckIds.UnsupportedArchives
    };

    public async Task<PreflightResult> Run(PreflightContext ctx, IPreflightProgress progress, CancellationToken token)
    {
        var nexus = ctx.State.Missing.Where(a => IsListNexus(ctx, a)).ToArray();
        if (nexus.Length == 0)
        {
            // Null rather than a recorded "no login": the download plan reads it that way, and probes the
            // account itself if a mirror reroute turns something missing into a Nexus download after all.
            ctx.State.Nexus = null;
            return PreflightResult.Passed(ctx.ModList.Archives.Any(a => IsListNexus(ctx, a))
                ? "Not needed, nothing left to download from Nexus Mods"
                : "Not needed, this list has no Nexus Mods files");
        }

        var status = await ctx.NexusLogin.Probe(token);
        ctx.State.Nexus = status;

        // Everything said from here counts what is still to be fetched, not what the list contains.
        var count = nexus.Length;
        var size = nexus.Sum(a => a.Size).ToFileSizeString();

        if (!status.HasToken)
            return PreflightResult.NeedsUser(
                $"Log in to Nexus Mods to download {Plural.Of(count, "file")} this install still needs ({size})",
                NotALoginDetail(status.Credential), new[] {PreflightAction.Login});

        if (!status.LoggedIn)
            return PreflightResult.NeedsUser("Your Nexus Mods login has expired, log in again", status.Error,
                new[] {PreflightAction.Login});

        var name = status.UserName ?? "Nexus Mods user";
        if (status.IsPremium)
            return PreflightResult.Passed($"Logged in as {name}{Tags(status)}");

        return PreflightResult.Passed(
            $"Logged in as {name}{Tags(status)} - {Plural.Of(count, "Nexus file")} will be downloaded manually ({size})");
    }

    /// <summary>
    ///     A Nexus download the modlist itself asks for, as opposed to one this run's mirror reroute
    ///     introduced by rewriting the archive's state (<c>ArchiveDownloadPipeline.Reroute</c>). The
    ///     distinction only exists on a second pass: the first time this check runs the plan has not been
    ///     computed, so nothing has been rewritten yet and the list is answering for itself. Re-run the
    ///     inventory - pick a game folder, retry a failed scan - and the rewritten states are still there,
    ///     and a list with no Nexus files at all would suddenly halt for a login, contradicting the rule the
    ///     reroute path is built on: a rerouted Nexus download without a login goes to the manual queue with
    ///     its file page, it does not stop the run. Ignoring them here is what keeps the two passes saying
    ///     the same thing. The alternative - not rewriting the shared archive - would mean carrying the
    ///     rerouted state alongside every archive through the split, the screening, the download and the
    ///     <c>.meta</c> file it is written to, which is a great deal of machinery for the same answer.
    /// </summary>
    private static bool IsListNexus(PreflightContext ctx, Archive archive)
    {
        return archive.State is Nexus && !ctx.State.Rerouted.Contains(archive.Name);
    }

    /// <summary>
    ///     What is worth saying about the login beyond the name: whether it is premium, and whether it was
    ///     made with anything other than the ordinary sign-in, so a machine set up by hand does not read the
    ///     same as a user who logged in through the app.
    /// </summary>
    private static string Tags(NexusLoginStatus status)
    {
        var tags = new List<string>();
        if (status.IsPremium) tags.Add("Premium");
        if (status.Credential == NexusCredentialSource.StoredApiKey) tags.Add("API key");
        return tags.Count == 0 ? "" : $" ({string.Join(", ", tags)})";
    }

    /// <summary>
    ///     Why there is no login when something that looks like a credential is nevertheless present. Without
    ///     this the row is the one thing that cannot explain itself: the environment variable does drive the
    ///     Nexus API, so the user has every reason to expect it to count.
    /// </summary>
    private static string? NotALoginDetail(NexusCredentialSource credential)
    {
        return credential == NexusCredentialSource.EnvironmentApiKey
            ? "NEXUS_API_KEY is set in this environment. It drives the Nexus API, but downloads need a login " +
              "stored by this app, so it does not count as being logged in here."
            : null;
    }
}
