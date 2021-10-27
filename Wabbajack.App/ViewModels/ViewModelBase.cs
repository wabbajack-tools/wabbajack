using System;
using System.Reactive.Disposables;
using ReactiveUI;
using ReactiveUI.Validation.Helpers;

namespace Wabbajack.App.ViewModels;

public class ViewModelBase : ReactiveValidationObject, IActivatableViewModel, IDisposable
{
    protected readonly CompositeDisposable VMDisposables = new();
    public ViewModelActivator Activator { get; protected set; }

    public void Dispose()
    {
        VMDisposables.Dispose();
        Activator.Dispose();
    }
}