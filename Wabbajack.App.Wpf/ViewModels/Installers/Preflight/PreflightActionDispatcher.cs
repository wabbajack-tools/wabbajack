using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using Wabbajack.Installer.Preflight;
using Wabbajack.LoginManagers;
using Wabbajack.Paths.IO;

namespace Wabbajack;

/// <summary>
///     The one place in the WPF app that knows what a <see cref="PreflightAction" /> id means in terms of
///     dialogs and login windows. Everything else asks the runner.
/// </summary>
public sealed class PreflightActionDispatcher
{
    private readonly PreflightRunner _runner;
    private readonly NexusLoginManager _nexusLogin;
    private readonly GameFilesVM _gameFiles;
    private readonly ILogger _logger;

    public PreflightActionDispatcher(PreflightRunner runner, NexusLoginManager nexusLogin, GameFilesVM gameFiles,
        ILogger logger)
    {
        _runner = runner;
        _nexusLogin = nexusLogin;
        _gameFiles = gameFiles;
        _logger = logger;
    }

    /// <summary>Must be called on the UI thread: the folder prompt and the login window are modal UI.</summary>
    public async Task Execute(string checkId, string actionId, CancellationToken token)
    {
        switch (actionId)
        {
            case "login":
                // The nexus-login check is re-run when NexusLoginManager refreshes its token, so nothing waits
                // on the browser window here. TriggerLogin takes no canExecute for this call's sake: the check
                // offers this action for an expired or revoked login as well as a missing one, and the tile
                // reads both of those as logged in.
                _nexusLogin.TriggerLogin.Execute(null);
                break;

            case "browse-game-folder":
                var picker = new FilePickerVM
                {
                    PathType = FilePickerVM.PathTypeOptions.Folder,
                    ExistCheckOption = FilePickerVM.CheckOptions.On,
                    PromptTitle = "Select the folder the game is installed in"
                };
                await ((ReactiveCommand<Unit, Unit>) picker.SetTargetPathCommand).Execute();
                if (picker.TargetPath == default || !picker.TargetPath.DirectoryExists()) break;

                _runner.SetGameFolder(picker.TargetPath);
                await _runner.RunAll(token);
                break;

            case "retry":
            case "rescan":
                var result = await _runner.RunCheck(checkId, token);
                if (result.State is PreflightState.Passed or PreflightState.Warning)
                    await _runner.RunAll(token);
                break;

            case "download-by-hand":
                // An automated download can still turn out to need a browser, which puts an archive back in
                // the manual queue after that check has already passed. The action belongs to whichever check
                // discovered it, so this one names the check it sends the user back to.
                var manual = await _runner.RunCheck(PreflightCheckIds.ManualDownloads, token);
                if (manual.State is PreflightState.Passed or PreflightState.Warning)
                    await _runner.RunAll(token);
                break;

            case "repair-game-files":
                // The card does the whole sequence, because all of it is one thing the user asked for: the
                // login if there is not one, the fetch, and then the check again - which is what decides
                // whether the run can carry on, and re-runs the rest of the checklist if it can.
                await _gameFiles.Repair(token);
                break;

            case "continue-anyway":
                _runner.Acknowledge(checkId);
                if (_runner.Checks.Any(c => c.State == PreflightState.Pending))
                    await _runner.RunAll(token);
                break;

            default:
                _logger.LogWarning("Preflight check {Check} offered an action this app does not know: {Action}",
                    checkId, actionId);
                break;
        }
    }
}
