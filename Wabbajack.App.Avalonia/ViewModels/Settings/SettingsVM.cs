using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive.Concurrency;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using Wabbajack.App.Avalonia.LoginManagers;
using Wabbajack.App.Avalonia.Services;
using Wabbajack.App.Avalonia.Util;
using Wabbajack.DTOs.Logins;
using Wabbajack.Services.OSIntegrated;
using Wabbajack.Services.OSIntegrated.TokenProviders;

namespace Wabbajack.App.Avalonia.ViewModels.Settings;

/// <summary>The Settings screen: logins, performance, the CLI and reset buttons, the about card, CDN uploads.</summary>
public partial class SettingsVM : ViewModel
{
    private readonly ILogger<SettingsVM> _logger;

    public SettingsVM(ILogger<SettingsVM> logger, IServiceProvider provider)
    {
        _logger = logger;

        LoginVM = new LoginManagerVM(provider.GetRequiredService<IEnumerable<INeedsLogin>>());
        PerformanceVM = new PerformanceSettingsVM(provider.GetRequiredService<ResourceSettingsManager>());
        AboutVM = provider.GetRequiredService<AboutVM>();

        LaunchCLICommand = ReactiveCommand.CreateFromTask(LaunchCLI);
        ResetCommand = ReactiveCommand.Create(Reset);
        OpenFileUploadCommand = ReactiveCommand.Create(OpenFileUpload);
        BrowseUploadsCommand = ReactiveCommand.Create(() =>
            UIUtils.OpenWebsite(new Uri($"{Links.BuildServer}author_controls/login/{ApiToken?.AuthorKey}")));

        Task.Run(async () =>
        {
            try
            {
                var token = await provider.GetRequiredService<WabbajackApiTokenProvider>().Get();
                RxApp.MainThreadScheduler.Schedule(() =>
                {
                    ApiToken = token;
                    IsAuthor = !string.IsNullOrEmpty(token?.AuthorKey);
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not read the Wabbajack API token");
            }
        });
    }

    public LoginManagerVM LoginVM { get; }
    public PerformanceSettingsVM PerformanceVM { get; }
    public AboutVM AboutVM { get; }

    public ICommand LaunchCLICommand { get; }
    public ICommand ResetCommand { get; }
    public ICommand OpenFileUploadCommand { get; }
    public ICommand BrowseUploadsCommand { get; }

    [Reactive] public partial WabbajackApiState? ApiToken { get; private set; }

    /// <summary>The Wabbajack CDN card is only for list authors, who are the ones holding an author key.</summary>
    [Reactive] public partial bool IsAuthor { get; private set; }

    // The file upload pane has not been ported yet; the button is in place and does nothing until it is.
    private void OpenFileUpload() => _logger.LogInformation("The file upload pane has not been ported yet");

    /// <summary>The CLI ships in a "cli" folder beside the app when installed, and beside it in a build.</summary>
    private static string CliFolder()
    {
        var currentPath = AppContext.BaseDirectory;
        var cliDir = Path.Combine(currentPath, "cli");
        return Directory.Exists(cliDir) ? cliDir : currentPath;
    }

    private void Reset()
    {
        try
        {
            _logger.LogInformation("Resetting Wabbajack!");
            var workingDir = CliFolder();
            _logger.LogInformation("Launching CLI from directory {workingDir}", workingDir);
            Process.Start(new ProcessStartInfo
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

    private Task LaunchCLI()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "cmd.exe",
                WorkingDirectory = CliFolder(),
                Arguments = "/k \"wabbajack-cli.exe -h\""
            });
        }
        catch (Exception ex)
        {
            _logger.LogError("Error while launching Wabbajack CLI: {ex}", ex);
        }

        return Task.CompletedTask;
    }
}

/// <summary>The Logins card: one tile per registered login.</summary>
public class LoginManagerVM(IEnumerable<INeedsLogin> logins) : ViewModel
{
    public LoginTargetVM[] Logins { get; } = logins.Select(l => new LoginTargetVM(l)).ToArray();
}

public class LoginTargetVM(INeedsLogin login) : ViewModel
{
    public INeedsLogin Login { get; } = login;
}
