using System;
using System.Reactive.Disposables;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace Wabbajack.Models;

public class LoadingLock : ReactiveObject, IDisposable
{
    private readonly CompositeDisposable _disposable;
    
    public ErrorResponse? ErrorState { get; set; }

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
            _parent.ErrorState = ErrorResponse.Success;
            Dispose();
        }

        public void Fail()
        {
            _parent.ErrorState = ErrorResponse.Failure;
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