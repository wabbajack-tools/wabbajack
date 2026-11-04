using System;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using ReactiveUI;
using ReactiveUI.SourceGenerators;

namespace Wabbajack.App.Avalonia.Models;

/// <summary>
/// Counts work in flight so a view can show that something is loading. Taken with <see cref="WithLoading" />
/// and released by disposing what it returns; the count only changes on the UI thread.
/// </summary>
public partial class LoadingLock : ReactiveObject, IDisposable
{
    private readonly CompositeDisposable _disposable = new();

    public LoadingLock()
    {
        this.WhenAnyValue(vm => vm.LoadLevel)
            .StartWith(0)
            .Subscribe(v =>
            {
                IsLoading = v > 0;
                IsNotLoading = v == 0;
            })
            .DisposeWith(_disposable);
    }

    /// <summary>Null until something has finished; then whether the last thing to finish failed.</summary>
    [Reactive] public partial bool? Failed { get; set; }

    [Reactive] public partial int LoadLevel { get; private set; }
    [Reactive] public partial bool IsLoading { get; private set; }
    [Reactive] public partial bool IsNotLoading { get; private set; }

    public IObservable<bool> IsLoadingObservable => this.WhenAnyValue(ll => ll.IsLoading);
    public IObservable<bool> IsNotLoadingObservable => this.WhenAnyValue(ll => ll.IsNotLoading);

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _disposable.Dispose();
    }

    public LockContext WithLoading()
    {
        RxApp.MainThreadScheduler.Schedule(() => LoadLevel++);
        return new LockContext(this);
    }

    public class LockContext(LoadingLock parent) : IDisposable
    {
        private bool _disposed;

        public void Succeed()
        {
            parent.Failed = false;
            Dispose();
        }

        public void Fail()
        {
            parent.Failed = true;
            Dispose();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            RxApp.MainThreadScheduler.Schedule(() => parent.LoadLevel--);
        }
    }
}
