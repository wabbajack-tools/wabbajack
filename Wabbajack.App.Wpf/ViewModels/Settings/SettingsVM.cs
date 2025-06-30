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
using ReactiveUI.Fody.Helpers;
using Wabbajack.Common;
using Wabbajack.Downloaders;
using Wabbajack.DTOs.Logins;
using Wabbajack.LoginManagers;
using Wabbajack.Messages;
using Wabbajack.RateLimiter;
using Wabbajack.Services.OSIntegrated;
using Wabbajack.Services.OSIntegrated.TokenProviders;
using Wabbajack.Util;

namespace Wabbajack;

public class SettingsVM : ViewModel
{
    private readonly ILogger<SettingsVM> _logger;
    private readonly Configuration.MainSettings _settings;
    private readonly SettingsManager _settingsManager;

    public LoginManagerVM LoginVM { get; }
    public PerformanceSettingsVM PerformanceVM { get; }
    public AboutVM AboutVM { get; }

    public ICommand LaunchCLICommand { get; }
    public ICommand ResetCommand { get; }
    public ICommand OpenFileUploadCommand { get; }
    public ICommand BrowseUploadsCommand { get; private set; }
    [Reactive] public WabbajackApiState ApiToken { get; private set; }

    public SettingsVM(ILogger<SettingsVM> logger, IServiceProvider provider)
    {
        _logger = logger;
        _settings = provider.GetRequiredService<Configuration.MainSettings>();
        _settingsManager = provider.GetRequiredService<SettingsManager>();
        Task.Run(async () =>
        {
            ApiToken = await provider.GetRequiredService<WabbajackApiTokenProvider>().Get();
            BrowseUploadsCommand = ReactiveCommand.Create(async () =>
            {
                var authorApiKey = ApiToken?.AuthorKey;
                UIUtils.OpenWebsite(new Uri($"{Consts.WabbajackBuildServerUri}author_controls/login/{authorApiKey}"));
            });
        });

        LoginVM = new LoginManagerVM(provider.GetRequiredService<ILogger<LoginManagerVM>>(), this,
            provider.GetRequiredService<IEnumerable<INeedsLogin>>());
        LaunchCLICommand = ReactiveCommand.CreateFromTask(LaunchCLI);
        ResetCommand = ReactiveCommand.Create(Reset);
        OpenFileUploadCommand = ReactiveCommand.Create(OpenFileUpload);
        PerformanceVM = new PerformanceSettingsVM(
            provider.GetRequiredService<IResource<DownloadDispatcher>>(),
            provider.GetRequiredService<SystemParametersConstructor>(),
            provider.GetRequiredService<ResourceSettingsManager>());
        AboutVM = provider.GetRequiredService<AboutVM>();
    }

    private void OpenFileUpload() => ShowFloatingWindow.Send(FloatingScreenType.FileUpload);

    private void Reset()
    {
        try
        {
            _logger.LogInformation("Resetting Wabbajack!");
            var currentPath = Path.GetDirectoryName(Assembly.GetEntryAssembly()!.Location);
            var cliDir = Path.Combine(currentPath, "cli");
            string workingDir = Directory.Exists(cliDir) ? cliDir : currentPath;
            _logger.LogInformation("Launching CLI from directory {workingDir}", workingDir);
            Process.Start(new ProcessStartInfo()
            {
                FileName = Path.Combine(workingDir, "wabbajack-cli.exe"),
                Arguments = "reset",
                CreateNoWindow = true,
                WorkingDirectory = workingDir
            });
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to reset Wabbajack: {ex}", ex);
        }
    }

    private async Task LaunchCLI()
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
