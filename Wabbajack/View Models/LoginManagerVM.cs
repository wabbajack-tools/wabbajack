using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Wabbajack.Lib;
using Wabbajack.Lib.Downloaders;

namespace Wabbajack
{
    public class LoginManagerVM : BackNavigatingVM
    {
        public List<INeedsLogin> Downloaders { get; }

        public LoginManagerVM(MainWindowVM mainWindowVM)
            : base(mainWindowVM)
        {
            Downloaders = DownloadDispatcher.Downloaders.OfType<INeedsLogin>().ToList();
        }
    }
}
