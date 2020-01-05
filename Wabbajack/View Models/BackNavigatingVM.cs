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
    public class BackNavigatingVM : ViewModel
    {
        [Reactive]
        public ViewModel NavigateBackTarget { get; set; }
        public ICommand BackCommand { get; }

        public BackNavigatingVM(MainWindowVM mainWindowVM)
        {
            BackCommand = ReactiveCommand.Create(
                execute: () => Utils.CatchAndLog(() => mainWindowVM.ActivePane = NavigateBackTarget),
                canExecute: this.WhenAny(x => x.NavigateBackTarget)
                    .Select(x => x != null)
                    .ObserveOnGuiThread());
        }
    }
}
