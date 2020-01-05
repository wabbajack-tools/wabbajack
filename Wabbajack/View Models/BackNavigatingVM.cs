using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
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
        IReactiveCommand BackCommand { get; }
    }

    public class BackNavigatingVM : ViewModel, IBackNavigatingVM
    {
        [Reactive]
        public ViewModel NavigateBackTarget { get; set; }
        public IReactiveCommand BackCommand { get; protected set; }

        public BackNavigatingVM(MainWindowVM mainWindowVM)
        {
            BackCommand = ReactiveCommand.Create(
                execute: () => Utils.CatchAndLog(() => mainWindowVM.NavigateTo(NavigateBackTarget)),
                canExecute: this.ConstructCanNavigateBack()
                    .ObserveOnGuiThread());
        }
    }

    public static class IBackNavigatingVMExt
    {
        public static IObservable<bool> ConstructCanNavigateBack(this IBackNavigatingVM vm)
        {
            return vm.WhenAny(x => x.NavigateBackTarget)
                .Select(x => x != null);
        }
    }
}
