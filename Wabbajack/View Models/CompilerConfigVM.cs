using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Wabbajack.Lib;

namespace Wabbajack
{
    public class CompilerConfigVM : ViewModel
    {
        private MainWindowVM _mainWindow;

        public CompilerConfigVM(MainWindowVM mainWindow)
        {
            _mainWindow = mainWindow;
        }
    }
}
