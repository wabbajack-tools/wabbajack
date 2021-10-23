using System;
using System.Reactive.Disposables;
using Avalonia.Threading;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace Wabbajack.App.Models;

public class LoadingLock : ReactiveObject, IDisposable
{
    private readonly CompositeDisposable _disposable;

    public LoadingLock()
    {
        _disposable = new CompositeDisposable();

        this.WhenAnyValue(vm => vm.LoadLevel)
            .Subscribe(v => IsLoading = v > 0)
            .DisposeWith(_disposable);
    }

    [Reactive] public int LoadLevel { get; private set; }

    [Reactive] public bool IsLoading { get; private set; }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _disposable.Dispose();
    }

    public IDisposable WithLoading()
    {
        Dispatcher.UIThread.Post(() => { LoadLevel++; }, DispatcherPriority.Background);
        return Disposable.Create(() =>
        {
            Dispatcher.UIThread.Post(() => { LoadLevel--; }, DispatcherPriority.Background);
        });
    }
}