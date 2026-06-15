using System;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using ReactiveUI;
using Wabbajack.Messages;

namespace Wabbajack.ViewModels.Test;

/// <summary>
/// Proof that ViewModels can be tested headlessly: resolved from DI, activated, and driven via
/// their ReactiveCommands with no WPF Dispatcher. HomeVM is chosen because it is free of WPF
/// runtime types, so these tests are also valid on Linux.
/// </summary>
[ClassConstructor<VmClassConstructor>]
public class HomeVMTests
{
    private readonly ViewModelTestHarness _harness;

    public HomeVMTests(ViewModelTestHarness harness) => _harness = harness;

    [Test]
    public async Task HeadlessSchedulerIsImmediate()
    {
        await Assert.That(ReferenceEquals(RxApp.MainThreadScheduler, ImmediateScheduler.Instance)).IsTrue();

        // Anything observed on the "GUI thread" resolves synchronously under ImmediateScheduler.
        int? observed = null;
        using var _ = Observable.Return(42).ObserveOn(RxApp.MainThreadScheduler).Subscribe(x => observed = x);
        await Assert.That(observed).IsEqualTo(42);
    }

    [Test]
    public async Task ResolvesFromDiWithCommandsWired()
    {
        var vm = _harness.Resolve<HomeVM>();

        await Assert.That(vm).IsNotNull();
        await Assert.That(vm.BrowseCommand).IsNotNull();
        await Assert.That(vm.GetHelpCommand).IsNotNull();
    }

    [Test]
    public async Task BrowseCommandSendsNavigationMessage()
    {
        var vm = _harness.Resolve<HomeVM>();

        ScreenType? navigatedTo = null;
        using var sub = MessageBus.Current.Listen<NavigateToGlobal>().Subscribe(m => navigatedTo = m.Screen);

        vm.BrowseCommand.Execute(null);

        await Assert.That(navigatedTo).IsEqualTo(ScreenType.ModListGallery);
    }
}
