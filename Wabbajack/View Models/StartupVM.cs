using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Wabbajack.Lib;

namespace Wabbajack
{
    public class StartupVM : ViewModel
    {
        public MainWindowVM MainWindow { get; }

        public StartupVM(MainWindowVM mainWindow)
        {
            MainWindow = mainWindow;
        }
    }
}
