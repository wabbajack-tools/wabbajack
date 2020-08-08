using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using ReactiveUI;
using Wabbajack.Lib;

namespace Wabbajack
{
    public class SettingsVM : BackNavigatingVM
    {
        public MainWindowVM MWVM { get; }
        public LoginManagerVM Login { get; }
        public PerformanceSettings Performance { get; }
        public FiltersSettings Filters { get; }
        public AuthorFilesVM AuthorFile { get; }

        public ICommand OpenTerminalCommand { get; }

        public SettingsVM(MainWindowVM mainWindowVM)
            : base(mainWindowVM)
        {
            MWVM = mainWindowVM;
            Login = new LoginManagerVM(this);
            Performance = mainWindowVM.Settings.Performance;
            AuthorFile = new AuthorFilesVM(this);
            Filters = mainWindowVM.Settings.Filters;
            OpenTerminalCommand = ReactiveCommand.CreateFromTask(() => OpenTerminal());
        }

        private async Task OpenTerminal()
        {
            var process = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                WorkingDirectory = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location)
            };
            Process.Start(process);
            await MWVM.ShutdownApplication();
        }
    }
}
