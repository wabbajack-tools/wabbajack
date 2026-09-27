using System;
using System.Collections.Generic;
using System.Reactive.Disposables;
using System.Runtime.CompilerServices;
using ReactiveUI;
using Wabbajack.App.Avalonia.Models;

namespace Wabbajack.App.Avalonia.ViewModels;

/// <summary>
/// The WPF app's ViewModel base, kept under the same name and shape so view models carry over with as
/// little change as possible: a bag of disposables that goes with the view model, a loading counter, and
/// ReactiveUI activation.
/// </summary>
public class ViewModel : ReactiveObject, IDisposable, IActivatableViewModel
{
    private readonly Lazy<CompositeDisposable> _compositeDisposable = new();

    public CompositeDisposable CompositeDisposable => _compositeDisposable.Value;

    public LoadingLock LoadingLock { get; } = new();

    public ViewModelActivator Activator { get; } = new();

    public virtual void Dispose()
    {
        if (_compositeDisposable.IsValueCreated)
            _compositeDisposable.Value.Dispose();
    }

    protected void RaiseAndSetIfChanged<T>(ref T item, T newItem, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(item, newItem)) return;
        item = newItem;
        this.RaisePropertyChanged(propertyName);
    }
}
