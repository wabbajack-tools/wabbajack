using System;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using Wabbajack.Installer.Preflight;
using Wabbajack.RateLimiter;

namespace Wabbajack;

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
    [Reactive] public partial bool IsActive { get; set; }
    [Reactive] public partial bool Acknowledged { get; set; }
    [Reactive] public partial string ActionLabel { get; set; }
    [Reactive] public partial bool HasAction { get; set; }

    public ReactiveCommand<Unit, Unit> ActionCommand { get; }

    /// <summary>Passed, or a Warning the user accepted.</summary>
    public bool IsSatisfied => State == PreflightState.Passed || (State == PreflightState.Warning && Acknowledged);

    public void Apply(CheckStatus status)
    {
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
