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
    private readonly ILogger _logger;

    public PreflightActionDispatcher(PreflightRunner runner, NexusLoginManager nexusLogin, ILogger logger)
    {
        _runner = runner;
        _nexusLogin = nexusLogin;
        _logger = logger;
    }

    /// <summary>Must be called on the UI thread: the folder prompt and the login window are modal UI.</summary>
    public async Task Execute(string checkId, string actionId, CancellationToken token)
    {
        switch (actionId)
        {
            case "login":
                // The nexus-login check is re-run when NexusLoginManager.LoggedIn changes, so nothing waits
                // on the browser window here.
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
