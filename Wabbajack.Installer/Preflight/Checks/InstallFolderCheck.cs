using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Wabbajack.Installer.Preflight.Rules;
using Wabbajack.Paths;

namespace Wabbajack.Installer.Preflight.Checks;

public sealed class InstallFolderCheck : IPreflightCheck
{
    public string Id => PreflightCheckIds.InstallFolder;
    public string Title => "Installation folder";
    public int Order => 150;
    public IReadOnlyList<string> DependsOn => Array.Empty<string>();
    public bool NeedsUserCanBeAcknowledged => true;

    public Task<PreflightResult> Run(PreflightContext ctx, IPreflightProgress progress, CancellationToken token)
    {
        var destinations = ctx.ModList.Directives.Select(d => d.To).ToHashSet();

        var evidence = InstallFolderProbe.Scan(new InstallFolderProbeInput(
            ctx.ModList.Name,
            ctx.Config.Install,
            ctx.Config.Downloads,
            ctx.Config.ModlistArchive,
            destinations.Contains,
            destinations.Count,
            new HashSet<AbsolutePath>(),
            ctx.Config.PreviousInstallLocation,
            ctx.ModList.Version?.ToString()));

        var verdict = InstallFolderRules.Classify(evidence);
        ctx.State.InstallFolder = verdict;
        ctx.Config.PreflightInstallFolder = verdict;

        return Task.FromResult(verdict.Kind switch
        {
            InstallFolderKind.Unrecognized => PreflightResult.Failed(verdict.Message, verdict.Detail,
                new[] {PreflightAction.Retry}),
            InstallFolderKind.ModListInstall when !verdict.IsSameModList => PreflightResult.NeedsUser(
                verdict.Message, verdict.Detail, new[] {PreflightAction.ContinueAnyway}),
            _ => PreflightResult.Passed(verdict.Message, verdict.Detail)
        });
    }
}
