using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Wabbajack.Common;
using Wabbajack.Lib;

namespace Wabbajack
{
    public interface IBackNavigatingVM : IReactiveObject
    {
        ViewModel NavigateBackTarget { get; set; }
        ReactiveCommand<Unit, Unit> BackCommand { get; }
        
        Subject<bool> IsBackEnabledSubject { get; }
        IObservable<bool> IsBackEnabled { get; }
    }

    public class BackNavigatingVM : ViewModel, IBackNavigatingVM
    {
        [Reactive]
        public ViewModel NavigateBackTarget { get; set; }
        public ReactiveCommand<Unit, Unit> BackCommand { get; protected set; }

        protected ObservableAsPropertyHelper<bool> _IsActive;
        public bool IsActive => _IsActive.Value;
        
        public Subject<bool> IsBackEnabledSubject { get; } = new Subject<bool>();
        public IObservable<bool> IsBackEnabled { get; }

        public BackNavigatingVM(MainWindowVM mainWindowVM)
        {
            IsBackEnabled = IsBackEnabledSubject.StartWith(true);
            BackCommand = ReactiveCommand.Create(
                execute: () => Utils.CatchAndLog(() =>
                {
                    mainWindowVM.NavigateTo(NavigateBackTarget);
                    Unload();
                }),
                canExecute: this.ConstructCanNavigateBack()
                    .ObserveOnGuiThread());

            _IsActive = this.ConstructIsActive(mainWindowVM)
                .ToGuiProperty(this, nameof(IsActive));
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
}
