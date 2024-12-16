using Newtonsoft.Json;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Reactive.Disposables;
using System.Runtime.CompilerServices;
using Wabbajack.Models;

namespace Wabbajack;

public class ViewModel : ReactiveObject, IDisposable, IActivatableViewModel
{
    private readonly Lazy<CompositeDisposable> _compositeDisposable = new();
    [JsonIgnore]
    public CompositeDisposable CompositeDisposable => _compositeDisposable.Value;

    [JsonIgnore] public LoadingLock LoadingLock { get; } = new();

    public virtual void Dispose()
    {
        if (_compositeDisposable.IsValueCreated)
        {
            _compositeDisposable.Value.Dispose();
        }
    }

    protected void RaiseAndSetIfChanged<T>(
        ref T item,
        T newItem,
        [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(item, newItem)) return;
        item = newItem;
        this.RaisePropertyChanged(propertyName);
    }

    public ViewModelActivator Activator { get; } = new();
}
