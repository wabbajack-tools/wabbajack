using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Web.WebView2.Wpf;
using NLog.Extensions.Logging;
using NLog.Targets;
using Octokit;
using Orc.FileAssociation;
using ReactiveUI;
using Wabbajack.CLI.Builder;
using Wabbajack.DTOs;
using Wabbajack.DTOs.Interventions;
using Wabbajack.Interventions;
using Wabbajack.LoginManagers;
using Wabbajack.Models;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Wabbajack.Services.OSIntegrated;
using Wabbajack.UserIntervention;
using Wabbajack.Util;
using Wabbajack.Common;
using Ext = Wabbajack.Common.Ext;
using Wabbajack.Messages;

namespace Wabbajack;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App
{
    private IHost _host;
    private TimerResolution? _timerRes;
    private SingleInstance? _singleInstance;

    private void OnStartup(object sender, StartupEventArgs e)
    {
        _timerRes = new TimerResolution(1);
        _singleInstance = new SingleInstance("Wabbajack-{F8C1E8F0-3E3A-4B3D-9F4A-1E5C6D7E8F9A}");

        if (!_singleInstance.IsFirstInstance)
        {
            // Another instance is running - send the args and exit
            if (e.Args.Length > 0)
            {
                SendArgsToRunningInstance(e.Args);
            }
            Environment.Exit(0);
            return;
        }
        EnsureSafeWorkingDirectoryOrExit();
        if (IsAdmin())
        {
            var messageBox = MessageBox.Show("Don't run Wabbajack as Admin!", "Error", MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK, MessageBoxOptions.DefaultDesktopOnly);
            if (messageBox == MessageBoxResult.OK)
            {
                Environment.Exit(1);
            }
            else
            {
                Environment.Exit(1);
            }
        }

        RxApp.MainThreadScheduler = new DispatcherScheduler(Dispatcher.CurrentDispatcher);
        _host = Host.CreateDefaultBuilder(Array.Empty<string>())
            .ConfigureLogging(AddLogging)
            .ConfigureServices((host, services) =>
            {
                ConfigureServices(services);
            })
            .Build();

        var assoc = _host.Services.GetRequiredService<Wabbajack.Services.OSIntegrated.FileAssociationSelfHealService>();

        if (OperatingSystem.IsWindows())
        {
            assoc.RegisterOrUpdate(enableProtocol: true); // Enable protocol registration
        }

        var webview2 = _host.Services.GetRequiredService<WebView2>();
        var currentDir = (AbsolutePath)Directory.GetCurrentDirectory();
        var webViewDir = currentDir.Combine("WebView2");
        if(webViewDir.DirectoryExists())
        {
            var logger = _host.Services.GetRequiredService<ILogger<App>>();
            logger.LogInformation("Local WebView2 executable folder found. Using folder {0} instead of system binaries!", currentDir.Combine("WebView2"));
            webview2.CreationProperties = new CoreWebView2CreationProperties() { BrowserExecutableFolder = currentDir.Combine("WebView2").ToString() };
        }

        var args = e.Args;

        RxApp.MainThreadScheduler.Schedule(0, (_, _) =>
        {
            // Check for URL protocol
            if (args.Length > 0 && args[0].StartsWith("wabbajack://", StringComparison.OrdinalIgnoreCase))
            {
                OpenUI();

                // Navigate to the gallery so ModListGalleryVM activates and subscribes
                NavigateToGlobal.Send(ScreenType.ModListGallery);

                HandleProtocolUrl(args[0]);
                return Disposable.Empty;
            }
            else if (args.Length == 1)
            {
                var arg = args[0].ToAbsolutePath();
                if (arg.FileExists() && arg.Extension == Ext.Wabbajack)
                {
                    OpenUI();
                    return Disposable.Empty;
                }
            } else if (args.Length > 0)
            {
                var builder = _host.Services.GetRequiredService<CommandLineBuilder>();
                builder.Run(e.Args).ContinueWith(async x =>
                {
                    Environment.Exit(await x);
                });
                return Disposable.Empty;
            }
            else
            {
                OpenUI();
                return Disposable.Empty;
            }

            return Disposable.Empty;
        });
    }

    private void HandleProtocolUrl(string url)
    {
        try
        {
            var logger = _host.Services.GetRequiredService<ILogger<App>>();
            logger.LogInformation("Handling protocol URL: {url}", url);

            if (!url.StartsWith("wabbajack://", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("Invalid wabbajack:// URL format");
            }

            // Extract the thing after wabbajack://
            var payload = url.Substring("wabbajack://".Length);
            payload = Uri.UnescapeDataString(payload ?? string.Empty).Trim();

            logger.LogInformation("Protocol payload: {payload}", payload);

            RxApp.MainThreadScheduler.Schedule(() =>
            {
                // Navigate to gallery to show loading UI
                NavigateToGlobal.Send(ScreenType.ModListGallery);

                logger.LogInformation("Sending LoadModlistFromProtocol message for: {payload}", payload);
                LoadModlistFromProtocol.Send(payload);
            });
        }
        catch (Exception ex)
        {
            var logger = _host.Services.GetRequiredService<ILogger<App>>();
            logger.LogError(ex, "Failed to handle protocol URL: {url}", url);

            MessageBox.Show(
                $"Failed to open modlist from URL.\n\nURL: {url}\n\nError: {ex.Message}",
                "Protocol Handler Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private static string GetProcessExePath()
    {
        // get current exe path for updating registry for file assocation
        var p = Process.GetCurrentProcess().MainModule?.FileName;
        if (string.IsNullOrWhiteSpace(p))
            throw new Exception("Process location is unavailable!");
        return p;
    }

    private static bool IsUnderDirectory(string childPath, string parentPath)
    {
        childPath = Path.GetFullPath(childPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)) + Path.DirectorySeparatorChar;
        parentPath = Path.GetFullPath(parentPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)) + Path.DirectorySeparatorChar;
        return childPath.StartsWith(parentPath, StringComparison.OrdinalIgnoreCase);
    }

    private static void EnsureSafeWorkingDirectoryOrExit(ILogger? logger = null)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return;

        var exePath = GetProcessExePath();
        var exeDir = Path.GetDirectoryName(exePath);
        if (string.IsNullOrWhiteSpace(exeDir))
            throw new Exception("Executable directory is unavailable!");

        var windowsDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var system32Dir = Path.Combine(windowsDir, "System32");

        if (IsUnderDirectory(exeDir, system32Dir) || IsUnderDirectory(exeDir, windowsDir))
        {
            MessageBox.Show(
                "Wabbajack refuses to run from Windows system folders (Windows/System32) please use a dedicated Wabbajack folder ( and NOT a folder like Downloads, Desktop etc ).",
                "Unsafe launch location",
                MessageBoxButton.OK,
                MessageBoxImage.Error,
                MessageBoxResult.OK,
                MessageBoxOptions.DefaultDesktopOnly);

            Environment.Exit(1);
        }

        // If the working directory is System32, force it to not.
        var cwd = Directory.GetCurrentDirectory();
        if (IsUnderDirectory(cwd, system32Dir) || IsUnderDirectory(cwd, windowsDir))
        {
            try
            {
                Directory.SetCurrentDirectory(exeDir);
                logger?.LogInformation("Adjusted working directory from {OldCwd} to {NewCwd}", cwd, exeDir);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Wabbajack failed to set a safe working directory. It will now exit.\n\n" + ex,
                    "Failed to set working directory",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error,
                    MessageBoxResult.OK,
                    MessageBoxOptions.DefaultDesktopOnly);

                Environment.Exit(1);
            }
        }
        else
        {
            //  for portable things
            if (!string.Equals(Path.GetFullPath(cwd), Path.GetFullPath(exeDir), StringComparison.OrdinalIgnoreCase))
            {
                Directory.SetCurrentDirectory(exeDir);
                logger?.LogInformation("Normalized working directory to executable directory: {NewCwd}", exeDir);
            }
        }
    }

    private void OpenUI()
    {
        MainWindow mainWindow = null;
        try
        {
            mainWindow = _host.Services.GetRequiredService<MainWindow>();
        }
        catch (Exception ex)
        {
            if (ex is SQLiteException sqlException)
            {
                // Attempt to clear read-only flag off Wabbajack directory
                if (sqlException.ResultCode == SQLiteErrorCode.CantOpen)
                {
                    // First MessageBox in App.OnStartup does not trigger: https://github.com/dotnet/wpf/issues/10067
                    MessageBox.Show("");
                    var result = MessageBox.Show($"Wabbajack cannot read or write to settings files inside %localappdata%/Wabbajack! Let Wabbajack adjust permissions?", "Failed to start Wabbajack", MessageBoxButton.YesNo, MessageBoxImage.Question);
                    if (result == MessageBoxResult.Yes)
                    {
                        GrantFullControlOverDir(KnownFolders.WabbajackAppLocal);
                        Restart();
                    }
                }
            }

            if (mainWindow == null)
            {
                MessageBox.Show($"Wabbajack failed to start! Full exception: {ex}", "Failed to start Wabbajack", MessageBoxButton.OK, MessageBoxImage.Error);
                throw;
            }
        }
        mainWindow!.Show();
    }

    private static void Restart()
    {
        try
        {
            var currentPath = Path.GetDirectoryName(Assembly.GetEntryAssembly()!.Location);
            var cliDir = Path.Combine(currentPath, "cli");
            string workingDir = Directory.Exists(cliDir) ? cliDir : currentPath;
            Process.Start(new ProcessStartInfo()
            {
                FileName = "wabbajack-cli.exe",
                Arguments = "restart",
                CreateNoWindow = true
            });
        }
        catch (Exception ex)
        {
        }
    }

    private bool GrantFullControlOverDir(AbsolutePath path)
    {
        try
        {
            if (path.DirectoryExists())
            {
                var dir = new DirectoryInfo(path.ToString());
                var dirSecurity = dir.GetAccessControl();
                AuthorizationRuleCollection rules = dirSecurity.GetAccessRules(true, true, typeof(NTAccount));
                foreach (FileSystemAccessRule rule in rules)
                {
                    if (rule.AccessControlType != AccessControlType.Deny) continue;

                    dirSecurity.RemoveAccessRule(rule);
                    dirSecurity.AddAccessRule(new FileSystemAccessRule(rule.IdentityReference, FileSystemRights.FullControl, InheritanceFlags.ObjectInherit | InheritanceFlags.ContainerInherit, PropagationFlags.None, AccessControlType.Allow));
                    dir.SetAccessControl(dirSecurity);
                }
                return true;
            }
            return false;
        }
        catch (Exception)
        {
            return false;
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _singleInstance?.Dispose();
        _timerRes?.Dispose();
        base.OnExit(e);
    }

    private void SendArgsToRunningInstance(string[] args)
    {
        try
        {
            using var pipeClient = new System.IO.Pipes.NamedPipeClientStream(".", "WabbajackProtocolPipe", System.IO.Pipes.PipeDirection.Out);
            pipeClient.Connect(1000); // 1 second timeout

            using var writer = new System.IO.StreamWriter(pipeClient);
            writer.WriteLine(string.Join("|", args));
            writer.Flush();
        }
        catch (Exception ex)
        {
            // Couldn't send to existing instance just exit
            System.Diagnostics.Debug.WriteLine($"Failed to send args to running instance: {ex.Message}");
        }
    }

    private static bool IsAdmin()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return false;

        try
        {
            var identity = WindowsIdentity.GetCurrent();
            var owner = identity.Owner;
            if (owner is not null) return owner.IsWellKnown(WellKnownSidType.BuiltinAdministratorsSid);

            var principle = new WindowsPrincipal(identity);
            return principle.IsInRole(WindowsBuiltInRole.Administrator);

        }
        catch (Exception)
        {
            return false;
        }
    }

    private void AddLogging(ILoggingBuilder loggingBuilder)
    {
        var config = new NLog.Config.LoggingConfiguration();

        var logFolder = KnownFolders.LauncherAwarePath.Combine("logs");
        if (!logFolder.DirectoryExists())
            logFolder.CreateDirectory();

        var fileTarget = new FileTarget("file")
        {
            FileName = logFolder.Combine("Wabbajack.current.log").ToString(),
            ArchiveFileName = logFolder.Combine("Wabbajack.{##}.log").ToString(),
            ArchiveOldFileOnStartup = true,
            MaxArchiveFiles = 10,
            Layout = "${processtime} [${level:uppercase=true}] (${logger}) ${message:withexception=true}",
            Header = "############ Wabbajack log file - ${longdate} ############"
        };

        var consoleTarget = new ConsoleTarget("console");

        var uiTarget = new LogStream
        {
            Name = "ui",
            Layout = "${message:withexception=false}",
        };

        loggingBuilder.Services.AddSingleton(uiTarget);

        config.AddRuleForAllLevels(fileTarget);
        config.AddRuleForAllLevels(consoleTarget);
        config.AddRuleForAllLevels(uiTarget);

        loggingBuilder.ClearProviders();
        loggingBuilder.AddFilter("System.Net.Http.HttpClient", LogLevel.Warning);
        loggingBuilder.SetMinimumLevel(LogLevel.Information);
        loggingBuilder.AddNLog(config);
    }

    private static IServiceCollection ConfigureServices(IServiceCollection services)
    {
        services.AddOSIntegrated();

        // Orc.FileAssociation
        services.AddSingleton<IApplicationRegistrationService>(new ApplicationRegistrationService());
        services.AddSingleton<FileAssociationSelfHealService>();

        // Singletons
        services.AddSingleton<CefService>();
        services.AddSingleton<IUserInterventionHandler, UserInterventionHandler>();
        services.AddSingleton<ImageCacheManager>();
        services.AddSingleton<SystemParametersConstructor>();
        services.AddSingleton<LauncherUpdater>();
        services.AddSingleton<ResourceMonitor>();
        services.AddSingleton<Networking.GitHub.Client>();
        services.AddSingleton(s => new GitHubClient(new ProductHeaderValue("wabbajack")));

        var currentDir = (AbsolutePath)Directory.GetCurrentDirectory();
        var webViewDir = currentDir.Combine("webview2");
        services.AddSingleton<WebView2>();
        services.AddSingleton<BrowserWindow>();

        // ViewModels
        services.AddTransient<MainWindow>();
        services.AddTransient<MainWindowVM>();
        services.AddTransient<NavigationVM>();
        services.AddTransient<HomeVM>();
        services.AddTransient<ModListGalleryVM>();
        services.AddTransient<CompilerHomeVM>();
        services.AddTransient<CompilerDetailsVM>();
        services.AddTransient<CompilerFileManagerVM>();
        services.AddTransient<CompilerMainVM>();
        services.AddTransient<InstallationVM>();
        services.AddTransient<SettingsVM>();
        services.AddTransient<WebBrowserVM>();
        services.AddTransient<InfoVM>();
        services.AddTransient<ModListDetailsVM>();
        services.AddTransient<FileUploadVM>();
        services.AddTransient<MegaLoginVM>();
        services.AddTransient<AboutVM>();

        // Login Handlers
        services.AddTransient<VectorPlexusLoginHandler>();
        services.AddTransient<NexusLoginHandler>();
        services.AddTransient<LoversLabLoginHandler>();

        // Login Managers

        //Disabled LL because it is currently not used and broken due to the way LL butchers their API
        //services.AddAllSingleton<INeedsLogin, LoversLabLoginManager>();
        services.AddAllSingleton<INeedsLogin, NexusLoginManager>();
        services.AddAllSingleton<INeedsLogin, MegaLoginManager>();
        //Disabled VP due to frequent login issues & because the only file that really got downloaded there has a mirror
        //services.AddAllSingleton<INeedsLogin, VectorPlexusLoginManager>();
        services.AddSingleton<ManualDownloadHandler>();
        services.AddSingleton<ManualBrowserDownloadHandler>();
        services.AddSingleton<NexusCollectionDownloader>();
        // Verbs
        services.AddSingleton<CommandLineBuilder>();
        services.AddCLIVerbs();
        return services;
    }
}
