// Wabbajack.App.Wpf/Preflight/PreflightViewModel.cs
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Windows.Input;
using ReactiveUI;
using ReactiveUI.SourceGenerators;

namespace Wabbajack.Preflight;

public partial class PreflightViewModel : ReactiveObject, IDisposable
{
    private readonly CompositeDisposable _disposable = new();
    private readonly IPreflightCheck[] _checks;

    // Modlist info
    public string ModlistName { get; init; } = "";
    public string ModlistVersion { get; init; } = "";
    public string ModlistAuthor { get; init; } = "";
    public string? ReadmeUrl { get; init; }

    // Path pickers — set by InstallationVM to share the same FilePickerVM instances
    public FilePickerVM? InstallLocation { get; init; }
    public FilePickerVM? DownloadLocation { get; init; }

    // Summary
    [Reactive] public partial int PassedCount { get; set; }
    [Reactive] public partial int TotalCount { get; set; }
    [Reactive] public partial bool AllPassed { get; set; }
    [Reactive] public partial IReadOnlyList<IPreflightCheck> FailedChecks { get; set; }

    // Commands
    public ICommand? ViewReadmeCommand { get; }
    [Reactive] public partial ICommand? InstallCommand { get; set; }

    public PreflightViewModel(IReadOnlyList<IPreflightCheck> checks)
    {
        _checks = checks.ToArray();
        TotalCount = _checks.Length;
        FailedChecks = Array.Empty<IPreflightCheck>();

        // Set up readme command
        ViewReadmeCommand = ReactiveCommand.Create(
            () => Process.Start(new ProcessStartInfo(ReadmeUrl!) { UseShellExecute = true }),
            this.WhenAnyValue(x => x.ReadmeUrl).Select(url => !string.IsNullOrWhiteSpace(url)));

        // Observe every check's Status property
        var statusObservables = _checks.Select(check =>
            check.WhenAnyValue(c => c.Status).Select(_ => System.Reactive.Unit.Default));

        Observable.Merge(statusObservables)
            .StartWith(System.Reactive.Unit.Default)
            .Subscribe(_ => Recompute())
            .DisposeWith(_disposable);
    }

    private void Recompute()
    {
        PassedCount = _checks.Count(c => c.Status == PreflightCheckStatus.Passed);
        AllPassed = PassedCount == TotalCount;
        FailedChecks = _checks.Where(c => c.Status != PreflightCheckStatus.Passed).ToList();
    }

    public void Dispose()
    {
        _disposable.Dispose();
        foreach (var check in _checks)
            check.Dispose();
    }
}
