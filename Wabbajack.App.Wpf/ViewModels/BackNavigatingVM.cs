using System;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Wabbajack.Common;
using Wabbajack.Messages;

namespace Wabbajack;

public interface IBackNavigatingVM : IReactiveObject
{
    ViewModel NavigateBackTarget { get; set; }
    ICommand CloseCommand { get; }
    
    Subject<bool> IsBackEnabledSubject { get; }
    IObservable<bool> IsBackEnabled { get; }
}

public class BackNavigatingVM : ViewModel, IBackNavigatingVM, IClosableVM
{
    [Reactive]
    public ViewModel NavigateBackTarget { get; set; }
    public ICommand CloseCommand { get; protected set; }
    
    [Reactive]
    public bool IsActive { get; set; }
    
    public Subject<bool> IsBackEnabledSubject { get; } = new Subject<bool>();
    public IObservable<bool> IsBackEnabled { get; }

    public BackNavigatingVM(ILogger logger)
    {
        IsBackEnabled = IsBackEnabledSubject.StartWith(true);
        CloseCommand = ReactiveCommand.Create(
            execute: () => logger.CatchAndLog(() =>
            {
                NavigateBack.Send();
                Unload();
            }),
            canExecute: this.ConstructCanNavigateBack()
                .ObserveOnGuiThread());
        
        this.WhenActivated(disposables =>
        {
            IsActive = true;
            Disposable.Create(() => IsActive = false).DisposeWith(disposables);
        });
    }

    public virtual void Unload()
    {
    }
}

public static class IBackNavigatingVMExt
{
    public static IObservable<bool> ConstructCanNavigateBack(this IBackNavigatingVM vm)
    {
        return vm.WhenAny(x => x.NavigateBackTarget)
            .CombineLatest(vm.IsBackEnabled)
            .Select(x => x.First != null && x.Second);
    }
    
    public static IObservable<bool> ConstructIsActive(this IBackNavigatingVM vm, MainWindowVM mwvm)
    {
        return mwvm.WhenAny(x => x.ActivePane)
            .Select(x => object.ReferenceEquals(vm, x));
    }
}
