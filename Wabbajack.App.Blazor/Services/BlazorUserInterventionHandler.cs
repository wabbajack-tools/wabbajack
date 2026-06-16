using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using ReactiveUI;
using Wabbajack.DTOs.Interventions;

namespace Wabbajack.Blazor.Services;

// Surfaces install/compile-time user interventions (confirmations, etc.) as a modal in the shell.
// Raise() is called from background work, so the shell observes Current on the UI scheduler.
public sealed class BlazorUserInterventionHandler : IUserInterventionHandler
{
    private readonly BehaviorSubject<global::Wabbajack.AUserIntervention?> _current = new(null);

    /// <summary>The intervention currently awaiting the user, or null.</summary>
    public IObservable<global::Wabbajack.AUserIntervention?> Current => _current;

    public void Raise(IUserIntervention intervention)
    {
        if (intervention is not global::Wabbajack.AUserIntervention a)
            return;

        _current.OnNext(a);
        // Clear the modal once the user (or anything else) handles it.
        a.WhenAnyValue(x => x.Handled)
            .Where(handled => handled)
            .Take(1)
            .Subscribe(_ => _current.OnNext(null));
    }
}
