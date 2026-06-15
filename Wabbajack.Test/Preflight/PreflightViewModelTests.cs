// Wabbajack.Test/Preflight/PreflightViewModelTests.cs
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Input;
using NSubstitute;
using ReactiveUI;
using Wabbajack.Preflight;

namespace Wabbajack.Preflight.Test;

public class PreflightViewModelTests
{
    private class FakeCheck : ReactiveObject, IPreflightCheck
    {
        private PreflightCheckStatus _status;
        public string Title { get; init; } = "Fake";
        public PreflightCheckStatus Status
        {
            get => _status;
            set => this.RaiseAndSetIfChanged(ref _status, value);
        }
        public string? FailureMessage { get; set; }
        public ICommand? ActionCommand => null;
        public string? ActionLabel => null;
        public IReadOnlyList<PreflightSubItem>? SubItems => null;
        public void Dispose() { }
    }

    [Test]
    public async Task AllChecksPassed_InstallEnabled()
    {
        var checks = new IPreflightCheck[]
        {
            new FakeCheck { Status = PreflightCheckStatus.Passed },
            new FakeCheck { Status = PreflightCheckStatus.Passed },
        };

        var vm = new PreflightViewModel(checks);

        await Assert.That(vm.AllPassed).IsTrue();
        await Assert.That(vm.PassedCount).IsEqualTo(2);
        await Assert.That(vm.TotalCount).IsEqualTo(2);
        await Assert.That(vm.FailedChecks).IsEmpty();
    }

    [Test]
    public async Task SomeChecksFailed_InstallDisabled()
    {
        var checks = new IPreflightCheck[]
        {
            new FakeCheck { Status = PreflightCheckStatus.Passed },
            new FakeCheck { Status = PreflightCheckStatus.Failed, FailureMessage = "Oops" },
        };

        var vm = new PreflightViewModel(checks);

        await Assert.That(vm.AllPassed).IsFalse();
        await Assert.That(vm.PassedCount).IsEqualTo(1);
        await Assert.That(vm.FailedChecks).HasSingleItem();
    }

    [Test]
    public async Task InfoStatusCountsAsPassed_ButStaysVisible()
    {
        var checks = new IPreflightCheck[]
        {
            new FakeCheck { Status = PreflightCheckStatus.Passed },
            new FakeCheck { Status = PreflightCheckStatus.Info, FailureMessage = "Will auto-download" },
        };

        var vm = new PreflightViewModel(checks);

        await Assert.That(vm.AllPassed).IsTrue();          // Info counts as passed
        await Assert.That(vm.PassedCount).IsEqualTo(2);    // Both counted
        await Assert.That(vm.FailedChecks).HasSingleItem();     // Info still visible in the list
    }

    [Test]
    public async Task CheckTransitionsToPass_UpdatesSummary()
    {
        var failingCheck = new FakeCheck { Status = PreflightCheckStatus.Failed, FailureMessage = "Oops" };
        var checks = new IPreflightCheck[]
        {
            new FakeCheck { Status = PreflightCheckStatus.Passed },
            failingCheck,
        };

        var vm = new PreflightViewModel(checks);
        await Assert.That(vm.AllPassed).IsFalse();

        // Transition to passed
        failingCheck.Status = PreflightCheckStatus.Passed;

        await Assert.That(vm.AllPassed).IsTrue();
        await Assert.That(vm.PassedCount).IsEqualTo(2);
    }
}
