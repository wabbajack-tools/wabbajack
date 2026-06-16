using System;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Runtime.CompilerServices;
using ReactiveUI;

namespace Wabbajack;

/// <summary>
/// Awaitable thread-hopping helpers for keeping heavy work off the UI thread while still being able
/// to touch bound view-model state safely. Built on ReactiveUI's schedulers so they work on any UI
/// framework (WPF/Avalonia) without referencing the platform directly.
///
/// <code>
///   await Dispatch.NotOnUI();   // continue on a thread-pool thread
///   // ...heavy CPU / IO work, build view models...
///   await Dispatch.OnUI();      // continue on the UI thread
///   // ...assign bound properties, mutate observable collections...
/// </code>
/// </summary>
public static class Dispatch
{
    /// <summary>Resumes the awaiting method on the UI thread.</summary>
    public static SchedulerHop OnUI() => new(RxApp.MainThreadScheduler);

    /// <summary>Resumes the awaiting method on a thread-pool (non-UI) thread.</summary>
    public static SchedulerHop NotOnUI() => new(RxApp.TaskpoolScheduler);
}

/// <summary>An awaitable that resumes its continuation on the supplied scheduler.</summary>
public readonly struct SchedulerHop : INotifyCompletion
{
    private readonly IScheduler _scheduler;

    public SchedulerHop(IScheduler scheduler) => _scheduler = scheduler;

    public SchedulerHop GetAwaiter() => this;

    // Always yield so the continuation is genuinely (re)scheduled onto the target scheduler,
    // even when we already happen to be on it.
    public bool IsCompleted => false;

    public void OnCompleted(Action continuation) =>
        _scheduler.Schedule(continuation, static (_, cont) =>
        {
            cont();
            return Disposable.Empty;
        });

    public void GetResult() { }
}
