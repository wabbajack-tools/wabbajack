using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ReactiveUI;
using Wabbajack.Lib;

namespace Wabbajack
{
    public class SettingsVM : ViewModel
    {
        public MainWindowVM MWVM { get; }
        public LoginManagerVM LoginManagerVM { get; }
        public IReactiveCommand BackCommand { get; } 

        public SettingsVM(MainWindowVM mainWindowVM)
        {
            MWVM = mainWindowVM;
            BackCommand = ReactiveCommand.Create(() => MWVM.NavigateBack());
            LoginManagerVM = new LoginManagerVM(this);
        }
    }
}
