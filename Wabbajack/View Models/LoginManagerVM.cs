using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using ReactiveUI;
using Wabbajack.Lib;
using Wabbajack.Lib.Downloaders;

namespace Wabbajack
{
    public class LoginManagerVM : ViewModel
    {
        private MainWindowVM mainWindowVM;
        public ICommand BackCommand { get; }
        public List<INeedsLogin> Downloaders { get; }

        public LoginManagerVM(MainWindowVM mainWindowVM)
        {
            BackCommand = ReactiveCommand.Create(() => mainWindowVM.NavigateBack());
            Downloaders = DownloadDispatcher.Downloaders.OfType<INeedsLogin>().ToList();
        }
    }
}
