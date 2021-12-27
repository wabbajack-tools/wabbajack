using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using Wabbajack.Lib;
using Wabbajack.Networking.WabbajackClientApi;
using Wabbajack.Services.OSIntegrated.TokenProviders;
using Wabbajack.View_Models.Settings;

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

        public SettingsVM(ILogger<SettingsVM> logger, MainWindowVM mainWindowVM, ServiceProvider provider)
            : base(logger, mainWindowVM)
        {
            MWVM = mainWindowVM;
            Login = new LoginManagerVM(this);
            Performance = mainWindowVM.Settings.Performance;
            AuthorFile = new AuthorFilesVM(provider.GetService<ILogger<AuthorFilesVM>>()!, 
                provider.GetService<WabbajackApiTokenProvider>()!, provider.GetService<Client>()!, this);
            Filters = mainWindowVM.Settings.Filters;
            OpenTerminalCommand = ReactiveCommand.CreateFromTask(OpenTerminal);
        }

        private async Task OpenTerminal()
        {
            var process = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                WorkingDirectory = Path.GetDirectoryName(Assembly.GetEntryAssembly()!.Location)!
            };
            Process.Start(process);
            await MWVM.ShutdownApplication();
        }
    }
}
