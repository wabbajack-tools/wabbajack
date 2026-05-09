// Wabbajack.App.Wpf/Preflight/NexusLoginCheck.cs
using System;
using System.Collections.Generic;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Windows.Input;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using Wabbajack.LoginManagers;

namespace Wabbajack.Preflight;

public partial class NexusLoginCheck : ReactiveObject, IPreflightCheck
{
    private readonly CompositeDisposable _disposable = new();

    public string Title => "Nexus Mods login";
    [Reactive] public partial PreflightCheckStatus Status { get; set; }
    [Reactive] public partial string? FailureMessage { get; set; }
    public ICommand? ActionCommand { get; }
    public string? ActionLabel => Status == PreflightCheckStatus.Failed ? "Log In" : null;
    public IReadOnlyList<PreflightSubItem>? SubItems => null;

    public NexusLoginCheck(INeedsLogin nexusLogin)
    {
        ActionCommand = nexusLogin.TriggerLogin;

        // Evaluate immediately
        UpdateStatus(nexusLogin.LoggedIn);

        // Observe changes reactively
        nexusLogin.WhenAnyValue(x => x.LoggedIn)
            .Subscribe(loggedIn => UpdateStatus(loggedIn))
            .DisposeWith(_disposable);
    }

    private void UpdateStatus(bool loggedIn)
    {
        if (loggedIn)
        {
            Status = PreflightCheckStatus.Passed;
            FailureMessage = null;
        }
        else
        {
            Status = PreflightCheckStatus.Failed;
            FailureMessage = "Log in to Nexus Mods to download mods automatically";
        }
        this.RaisePropertyChanged(nameof(ActionLabel));
    }

    public void Dispose() => _disposable.Dispose();
}
