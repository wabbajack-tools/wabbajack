using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ReactiveUI;
using Wabbajack.Lib;

namespace Wabbajack
{
    public class SettingsVM : BackNavigatingVM
    {
        public MainWindowVM MWVM { get; }
        public LoginManagerVM LoginManagerVM { get; }

        public SettingsVM(MainWindowVM mainWindowVM)
            : base(mainWindowVM)
        {
            MWVM = mainWindowVM;
            LoginManagerVM = new LoginManagerVM(this);
        }
    }
}
