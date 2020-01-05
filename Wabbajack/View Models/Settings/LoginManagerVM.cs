using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using Wabbajack.Lib.Downloaders;

namespace Wabbajack
{
    public class LoginManagerVM : BackNavigatingVM
    {
        public List<INeedsLogin> Downloaders { get; }

        public LoginManagerVM(SettingsVM settingsVM)
            : base(settingsVM.MWVM)
        {
            Downloaders = DownloadDispatcher.Downloaders.OfType<INeedsLogin>().ToList();
        }
    }
}
