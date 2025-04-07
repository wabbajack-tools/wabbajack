using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace Wabbajack.Models;

public class LoadingLock : ReactiveObject, IDisposable
{
    private readonly CompositeDisposable _disposable;
    
    [Reactive]
    public ValidationResult? ErrorState { get; set; }

    public LoadingLock()
    {
        _disposable = new CompositeDisposable();

        this.WhenAnyValue(vm => vm.LoadLevel)
            .StartWith(0)
            .Subscribe(v => IsLoading = v > 0)
            .DisposeWith(_disposable);
        
        this.WhenAnyValue(vm => vm.LoadLevel)
            .StartWith(0)
            .Subscribe(v => IsNotLoading = v == 0)
            .DisposeWith(_disposable);
    }

    [Reactive] public int LoadLevel { get; private set; }

    [Reactive] public bool IsLoading { get; private set; }
    
    [Reactive] public bool IsNotLoading { get; private set; }

    public IObservable<bool> IsLoadingObservable => this.WhenAnyValue(ll => ll.IsLoading);
    public IObservable<bool> IsNotLoadingObservable => this.WhenAnyValue(ll => ll.IsNotLoading);

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _disposable.Dispose();
    }

    public LockContext WithLoading()
    {
        RxApp.MainThreadScheduler.Schedule(0, (_, _) => { LoadLevel++;
            return Disposable.Empty;
        });
        return new LockContext(this);
    }

    public class LockContext : IDisposable
    {
        private readonly LoadingLock _parent;
        private bool _disposed;

        public LockContext(LoadingLock parent)
        {
            _parent = parent;
            _disposed = false;
        }

        public void Succeed()
        {
            _parent.ErrorState = ValidationResult.Success;
            Dispose();
        }

        public void Fail()
        {
            _parent.ErrorState = ValidationResult.Failure;
            Dispose();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            
            RxApp.MainThreadScheduler.Schedule(0, (_, _) => { _parent.LoadLevel--;
                return Disposable.Empty;
            });
        }
    }
}