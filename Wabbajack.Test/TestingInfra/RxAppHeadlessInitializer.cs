using System.Reactive.Concurrency;
using System.Runtime.CompilerServices;
using ReactiveUI;

namespace Wabbajack.Test.TestingInfra;

/// <summary>
/// Pins ReactiveUI's schedulers to <see cref="ImmediateScheduler"/> before any test runs, so
/// ViewModels that route through <c>RxApp.MainThreadScheduler</c> (e.g. ObserveOnGuiThread,
/// ToGuiProperty) resolve synchronously without a WPF Dispatcher. Runs once per test assembly.
/// </summary>
internal static class RxAppHeadlessInitializer
{
    [ModuleInitializer]
    internal static void Init()
    {
        RxApp.MainThreadScheduler = ImmediateScheduler.Instance;
        RxApp.TaskpoolScheduler = ImmediateScheduler.Instance;
    }
}
