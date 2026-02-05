using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Spectre.Console;
using Wabbajack.Installer;

namespace Wabbajack.CLI.Console;

/// <summary>
/// Bridges StatusUpdate events from installers/compilers to Spectre.Console progress display.
/// </summary>
public class StatusUpdateBridge
{
    private readonly IAnsiConsole _console;

    public StatusUpdateBridge()
    {
        _console = AnsiConsole.Console;
    }

    public StatusUpdateBridge(IAnsiConsole console)
    {
        _console = console;
    }

    /// <summary>
    /// Runs an operation with a live progress display, bridging StatusUpdate events to progress bars.
    /// </summary>
    public async Task<T> WithProgressAsync<T>(
        string title,
        Func<Action<StatusUpdate>, Task<T>> operation,
        CancellationToken token = default)
    {
        T result = default!;

        await _console.Progress()
            .AutoClear(false)
            .HideCompleted(false)
            .Columns(
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new SpinnerColumn())
            .StartAsync(async ctx =>
            {
                var overallTask = ctx.AddTask($"[bold]{Markup.Escape(title)}[/]", maxValue: 100);
                var stepTask = ctx.AddTask("Starting...", maxValue: 100);

                void HandleStatusUpdate(StatusUpdate update)
                {
                    overallTask.Value = update.StepsProgress.Value * 100;
                    stepTask.Description = $"[{update.CurrentStep}] {Markup.Escape(update.StatusText)}";
                    stepTask.Value = update.StepProgress.Value * 100;
                }

                result = await operation(HandleStatusUpdate);

                overallTask.Value = 100;
                stepTask.Value = 100;
                stepTask.Description = "Complete";
            });

        return result;
    }

    /// <summary>
    /// Runs an operation with a live progress display for multiple concurrent downloads.
    /// </summary>
    public async Task WithMultiProgressAsync(
        string title,
        int maxTasks,
        Func<IMultiProgressReporter, Task> operation,
        CancellationToken token = default)
    {
        await _console.Progress()
            .AutoClear(false)
            .HideCompleted(true)
            .Columns(
                new TaskDescriptionColumn { Alignment = Justify.Left },
                new ProgressBarColumn(),
                new PercentageColumn(),
                new TransferSpeedColumn(),
                new SpinnerColumn())
            .StartAsync(async ctx =>
            {
                var reporter = new MultiProgressReporter(ctx, maxTasks);
                await operation(reporter);
            });
    }
}

/// <summary>
/// Interface for reporting progress on multiple concurrent operations.
/// </summary>
public interface IMultiProgressReporter
{
    /// <summary>
    /// Starts tracking a new operation.
    /// </summary>
    IOperationProgress StartOperation(string description, long totalBytes);
}

/// <summary>
/// Tracks progress of a single operation.
/// </summary>
public interface IOperationProgress : IDisposable
{
    void UpdateProgress(long currentBytes);
    void SetDescription(string description);
    void Complete();
    void Fail(string? reason = null);
}

internal class MultiProgressReporter : IMultiProgressReporter
{
    private readonly ProgressContext _ctx;
    private readonly SemaphoreSlim _semaphore;
    private readonly object _lock = new();
    private readonly Dictionary<ProgressTask, OperationProgress> _activeTasks = new();

    public MultiProgressReporter(ProgressContext ctx, int maxConcurrent)
    {
        _ctx = ctx;
        _semaphore = new SemaphoreSlim(maxConcurrent, maxConcurrent);
    }

    public IOperationProgress StartOperation(string description, long totalBytes)
    {
        var task = _ctx.AddTask(Markup.Escape(description), maxValue: totalBytes > 0 ? totalBytes : 100);
        if (totalBytes == 0)
        {
            task.IsIndeterminate = true;
        }

        var progress = new OperationProgress(task, this);
        lock (_lock)
        {
            _activeTasks[task] = progress;
        }
        return progress;
    }

    internal void RemoveTask(ProgressTask task)
    {
        lock (_lock)
        {
            _activeTasks.Remove(task);
        }
    }
}

internal class OperationProgress : IOperationProgress
{
    private readonly ProgressTask _task;
    private readonly MultiProgressReporter _reporter;
    private bool _disposed;

    public OperationProgress(ProgressTask task, MultiProgressReporter reporter)
    {
        _task = task;
        _reporter = reporter;
    }

    public void UpdateProgress(long currentBytes)
    {
        if (_disposed) return;
        _task.Value = currentBytes;
    }

    public void SetDescription(string description)
    {
        if (_disposed) return;
        _task.Description = Markup.Escape(description);
    }

    public void Complete()
    {
        if (_disposed) return;
        _task.Value = _task.MaxValue;
        _task.StopTask();
    }

    public void Fail(string? reason = null)
    {
        if (_disposed) return;
        _task.Description = reason != null
            ? $"[red]{Markup.Escape(_task.Description)} - {Markup.Escape(reason)}[/]"
            : $"[red]{Markup.Escape(_task.Description)} - Failed[/]";
        _task.StopTask();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _reporter.RemoveTask(_task);
    }
}
