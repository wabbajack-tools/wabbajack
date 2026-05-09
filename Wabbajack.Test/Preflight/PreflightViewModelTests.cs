// Wabbajack.Test/Preflight/PreflightViewModelTests.cs
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Input;
using NSubstitute;
using ReactiveUI;
using Wabbajack.Preflight;
using Xunit;

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

    [Fact]
    public void AllChecksPassed_InstallEnabled()
    {
        var checks = new IPreflightCheck[]
        {
            new FakeCheck { Status = PreflightCheckStatus.Passed },
            new FakeCheck { Status = PreflightCheckStatus.Passed },
        };

        var vm = new PreflightViewModel(checks);

        Assert.True(vm.AllPassed);
        Assert.Equal(2, vm.PassedCount);
        Assert.Equal(2, vm.TotalCount);
        Assert.Empty(vm.FailedChecks);
    }

    [Fact]
    public void SomeChecksFailed_InstallDisabled()
    {
        var checks = new IPreflightCheck[]
        {
            new FakeCheck { Status = PreflightCheckStatus.Passed },
            new FakeCheck { Status = PreflightCheckStatus.Failed, FailureMessage = "Oops" },
        };

        var vm = new PreflightViewModel(checks);

        Assert.False(vm.AllPassed);
        Assert.Equal(1, vm.PassedCount);
        Assert.Single(vm.FailedChecks);
    }

    [Fact]
    public void InfoStatusCountsAsPassed_ButStaysVisible()
    {
        var checks = new IPreflightCheck[]
        {
            new FakeCheck { Status = PreflightCheckStatus.Passed },
            new FakeCheck { Status = PreflightCheckStatus.Info, FailureMessage = "Will auto-download" },
        };

        var vm = new PreflightViewModel(checks);

        Assert.True(vm.AllPassed);          // Info counts as passed
        Assert.Equal(2, vm.PassedCount);    // Both counted
        Assert.Single(vm.FailedChecks);     // Info still visible in the list
    }

    [Fact]
    public void CheckTransitionsToPass_UpdatesSummary()
    {
        var failingCheck = new FakeCheck { Status = PreflightCheckStatus.Failed, FailureMessage = "Oops" };
        var checks = new IPreflightCheck[]
        {
            new FakeCheck { Status = PreflightCheckStatus.Passed },
            failingCheck,
        };

        var vm = new PreflightViewModel(checks);
        Assert.False(vm.AllPassed);

        // Transition to passed
        failingCheck.Status = PreflightCheckStatus.Passed;

        Assert.True(vm.AllPassed);
        Assert.Equal(2, vm.PassedCount);
    }
}
