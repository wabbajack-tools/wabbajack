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
using Wabbajack.App.Avalonia.Messages;
using Wabbajack.App.Avalonia.Services;
using Wabbajack.App.Avalonia.Util;
using Wabbajack.DTOs.Logins;
using Wabbajack.Paths;
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

    private static void OpenFileUpload() => ShowFloatingWindow.Send(FloatingScreenType.FileUpload);


    private void Reset()
    {
        try
        {
            _logger.LogInformation("Resetting Wabbajack!");
            var workingDir = AppFolders.CliFolder;
            _logger.LogInformation("Launching CLI from directory {workingDir}", workingDir);
            Process.Start(new ProcessStartInfo
            {
                FileName = Path.Combine(workingDir, AppFolders.CliFileName),
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
            if (OperatingSystem.IsWindows())
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    WorkingDirectory = AppFolders.CliFolder,
                    Arguments = "/k \"wabbajack-cli.exe -h\""
                });
            }
            else if (!OpenTerminalWithCliHelp())
            {
                // No terminal to be found; the folder is where the user would start it from.
                UIUtils.OpenFolder(AppFolders.CliFolder.ToAbsolutePath());
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("Error while launching Wabbajack CLI: {ex}", ex);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    ///     What cmd /k does on Windows: a terminal in the CLI's folder showing its help, left open for the user.
    ///     xdg-terminal-exec is the desktop-neutral way to ask for one; false when it is not installed.
    /// </summary>
    private static bool OpenTerminalWithCliHelp()
    {
        var info = new ProcessStartInfo("xdg-terminal-exec")
        {
            UseShellExecute = false,
            WorkingDirectory = AppFolders.CliFolder
        };
        foreach (var arg in new[] {"sh", "-c", $"./{AppFolders.CliFileName} -h; exec \"${{SHELL:-sh}}\""})
            info.ArgumentList.Add(arg);
        try
        {
            Process.Start(info)?.Dispose();
            return true;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return false;
        }
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
