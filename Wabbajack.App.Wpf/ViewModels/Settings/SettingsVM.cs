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
using Wabbajack.Paths;
using Wabbajack.RateLimiter;
using Wabbajack.Services.OSIntegrated;
using Wabbajack.Services.OSIntegrated.TokenProviders;
using Wabbajack.Util;
using Wabbajack.ViewModels.Settings;

namespace Wabbajack;

public class SettingsVM : BackNavigatingVM
{
    private readonly ILogger<SettingsVM> _logger;
    private readonly Configuration.MainSettings _settings;
    private readonly SettingsManager _settingsManager;

    public LoginManagerVM Login { get; }
    public PerformanceSettingsVM Performance { get; }
    public AuthorFilesVM AuthorFile { get; }

    public ICommand OpenTerminalCommand { get; }

    public SettingsVM(ILogger<SettingsVM> logger, IServiceProvider provider)
        : base(logger)
    {
        _logger = logger;
        _settings = provider.GetRequiredService<Configuration.MainSettings>();
        _settingsManager = provider.GetRequiredService<SettingsManager>();

        Login = new LoginManagerVM(provider.GetRequiredService<ILogger<LoginManagerVM>>(), this,
            provider.GetRequiredService<IEnumerable<INeedsLogin>>());
        AuthorFile = new AuthorFilesVM(provider.GetRequiredService<ILogger<AuthorFilesVM>>()!,
            provider.GetRequiredService<WabbajackApiTokenProvider>()!, provider.GetRequiredService<Client>()!, this);
        OpenTerminalCommand = ReactiveCommand.CreateFromTask(OpenTerminal);
        Performance = new PerformanceSettingsVM(
            provider.GetRequiredService<IResource<DownloadDispatcher>>(),
            provider.GetRequiredService<SystemParametersConstructor>(),
            provider.GetRequiredService<ResourceSettingsManager>());
        CloseCommand = ReactiveCommand.Create(() =>
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
        try
        {
            var currentPath = Path.GetDirectoryName(Assembly.GetEntryAssembly()!.Location);
            var cliDir = Path.Combine(currentPath, "cli");
            string workingDir = Directory.Exists(cliDir) ? cliDir : currentPath;
            var process = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                WorkingDirectory = workingDir,
                Arguments = $"/k \"wabbajack-cli.exe -h\"",
            };
            Process.Start(process);
        }
        catch (Exception ex)
        {
            _logger.LogError("Error while launching Wabbajack CLI: {ex}", ex);
        }
    }
}
