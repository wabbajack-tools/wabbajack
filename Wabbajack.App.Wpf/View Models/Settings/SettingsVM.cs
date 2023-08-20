using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using Wabbajack.Common;
using Wabbajack.Downloaders;
using Wabbajack.LoginManagers;
using Wabbajack.Messages;
using Wabbajack.Networking.WabbajackClientApi;
using Wabbajack.RateLimiter;
using Wabbajack.Services.OSIntegrated;
using Wabbajack.Services.OSIntegrated.TokenProviders;
using Wabbajack.Util;
using Wabbajack.View_Models.Settings;

namespace Wabbajack
{
    public class SettingsVM : BackNavigatingVM
    {
        private readonly Configuration.MainSettings _settings;
        private readonly SettingsManager _settingsManager;

        public LoginManagerVM Login { get; }
        public PerformanceSettings Performance { get; }
        public FiltersSettings Filters { get; }
        public AuthorFilesVM AuthorFile { get; }

        public ICommand OpenTerminalCommand { get; }

        public SettingsVM(ILogger<SettingsVM> logger, IServiceProvider provider)
            : base(logger)
        {
            _settings = provider.GetRequiredService<Configuration.MainSettings>();
            _settingsManager = provider.GetRequiredService<SettingsManager>();

            Login = new LoginManagerVM(provider.GetRequiredService<ILogger<LoginManagerVM>>(), this,
                provider.GetRequiredService<IEnumerable<INeedsLogin>>());
            AuthorFile = new AuthorFilesVM(provider.GetRequiredService<ILogger<AuthorFilesVM>>()!,
                provider.GetRequiredService<WabbajackApiTokenProvider>()!, provider.GetRequiredService<Client>()!, this);
            OpenTerminalCommand = ReactiveCommand.CreateFromTask(OpenTerminal);
            Performance = new PerformanceSettings(
                _settings,
                provider.GetRequiredService<IResource<DownloadDispatcher>>(),
                provider.GetRequiredService<SystemParametersConstructor>());
            BackCommand = ReactiveCommand.Create(() =>
            {
                NavigateBack.Send();
                Unload();
            });
        }

        public override void Unload()
        {
            _settingsManager.Save(Configuration.MainSettings.SettingsFileName, _settings).FireAndForget();

            base.Unload();
        }

        private async Task OpenTerminal()
        {
            var process = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                WorkingDirectory = Path.GetDirectoryName(Assembly.GetEntryAssembly()!.Location)!
            };
            Process.Start(process);
        }
    }
}
