using System;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using Wabbajack.App.Avalonia.ViewModels;
using Wabbajack.Installer.Preflight;
using Wabbajack.RateLimiter;

namespace Wabbajack.App.Avalonia.ViewModels.Installers.Preflight;

public enum PreflightDetailKind
{
    None,
    BulkDownloads,
    ManualDownloads,
    GameFiles
}

/// <summary>
///     One row on the checklist. Everything here is a projection of the runner's <see cref="CheckStatus" />;
///     the only decision the row makes is which single action to offer as its button.
/// </summary>
public partial class PreflightCheckVM : ViewModel
{
    /// <summary>The host's own action for a check the user stopped: run it again.</summary>
    private const string ResumeActionId = "retry";

    private string _actionId = string.Empty;

    public PreflightCheckVM(CheckStatus status, Func<string, string, CancellationToken, Task> execute,
        CancellationToken token)
    {
        Id = status.Id;
        Title = status.Title;
        StatusText = string.Empty;
        Message = string.Empty;
        ActionLabel = string.Empty;

        ActionCommand = ReactiveCommand.CreateFromTask(
            () => execute(Id, _actionId, token),
            this.WhenAnyValue(x => x.HasAction));

        Apply(status);
    }

    public string Id { get; }
    public string Title { get; }

    /// <summary>
    ///     Which panel this check wants under the checklist. Two of them always want their own; game-files
    ///     only wants one while it is actually offering to fetch something, which is why this follows the
    ///     status rather than being decided once from the id. The rest of the time - while it is running,
    ///     once it has passed, or when what it found is a game install nothing here can repair - the plain
    ///     text panel says more than an empty card would.
    /// </summary>
    [Reactive] public partial PreflightDetailKind DetailKind { get; set; }

    [Reactive] public partial PreflightState State { get; set; }

    /// <summary>Short text for the right-hand side of the row.</summary>
    [Reactive] public partial string StatusText { get; set; }

    /// <summary>The check's full message, for the detail panel.</summary>
    [Reactive] public partial string Message { get; set; }

    /// <summary>The check's long-form detail, if it produced any.</summary>
    [Reactive] public partial string? DetailText { get; set; }

    [Reactive] public partial Percent Progress { get; set; }
    [Reactive] public partial bool HasProgress { get; set; }

    /// <summary>How far a running check has got, as counts; the total is zero while it has none to give.</summary>
    [Reactive] public partial long ProgressCurrent { get; set; }
    [Reactive] public partial long ProgressTotal { get; set; }

    /// <summary>When the current run of this check started, or null when it is not running.</summary>
    [Reactive] public partial DateTime? RunningSince { get; set; }

    /// <summary>
    ///     When the running check last changed what it says it is doing. A check can count through more than one
    ///     phase (archive-inventory checks the install, then hashes), and a time estimate has to start again
    ///     with each.
    /// </summary>
    [Reactive] public partial DateTime? PhaseSince { get; set; }

    private string? _phase;
    [Reactive] public partial bool IsActive { get; set; }
    [Reactive] public partial bool Acknowledged { get; set; }
    [Reactive] public partial string ActionLabel { get; set; }
    [Reactive] public partial bool HasAction { get; set; }

    public ReactiveCommand<Unit, Unit> ActionCommand { get; }

    /// <summary>Passed, or a Warning the user accepted.</summary>
    public bool IsSatisfied => State == PreflightState.Passed || (State == PreflightState.Warning && Acknowledged);

    public void Apply(CheckStatus status)
    {
        if (status.State == PreflightState.Running)
        {
            var now = DateTime.Now;
            RunningSince ??= now;
            if (PhaseSince == null || status.ProgressText != _phase)
            {
                PhaseSince = now;
                _phase = status.ProgressText;
            }
        }
        else
        {
            RunningSince = null;
            PhaseSince = null;
            _phase = null;
        }
        ProgressCurrent = status.ProgressCurrent;
        ProgressTotal = status.ProgressTotal;

        State = status.State;
        DetailKind = Id switch
        {
            PreflightCheckIds.AutomatedDownloads => PreflightDetailKind.BulkDownloads,
            PreflightCheckIds.ManualDownloads => PreflightDetailKind.ManualDownloads,
            PreflightCheckIds.GameFiles when status.Actions.Any(a => a.Id == PreflightAction.RepairGameFiles.Id) =>
                PreflightDetailKind.GameFiles,
            _ => PreflightDetailKind.None
        };

        Message = status.State == PreflightState.Running && !string.IsNullOrWhiteSpace(status.ProgressText)
            ? status.ProgressText
            : status.Message;
        DetailText = status.Detail;
        Acknowledged = status.Acknowledged;
        Progress = status.Progress;
        HasProgress = status.State == PreflightState.Running && status.Progress.Value > 0;

        StatusText = status.State switch
        {
            PreflightState.Pending => "waiting",
            PreflightState.Running => string.IsNullOrWhiteSpace(status.ProgressText) ? "running" : status.ProgressText,
            PreflightState.Skipped => "skipped",
            PreflightState.Cancelled => "stopped",
            PreflightState.Warning when status.Acknowledged => $"{status.Message} (accepted)",
            _ => status.Message
        };

        var action = status.Actions.FirstOrDefault();
        if (action == null && status.State == PreflightState.Cancelled)
            action = new PreflightAction(ResumeActionId, "Resume");

        _actionId = action?.Id ?? string.Empty;
        ActionLabel = action?.Title ?? string.Empty;
        HasAction = action != null;
    }
}
