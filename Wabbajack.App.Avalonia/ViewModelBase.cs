using System;
using System.Collections.Generic;
using System.Reactive.Disposables;
using System.Runtime.CompilerServices;
using ReactiveUI;
using Wabbajack.App.Avalonia.Models;

namespace Wabbajack.App.Avalonia.ViewModels;

public class ViewModelBase : ReactiveObject, IDisposable, IActivatableViewModel
{
    public CompositeDisposable CompositeDisposable { get; } = new();

    public LoadingLock LoadingLock { get; } = new();

    public ViewModelActivator Activator { get; } = new();

    public virtual void Dispose()
    {
        GC.SuppressFinalize(this);
        CompositeDisposable.Dispose();
        LoadingLock.Dispose();
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
}
