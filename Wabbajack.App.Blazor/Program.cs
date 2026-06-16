using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NLog.Extensions.Logging;
using NLog.Targets;
using Photino.Blazor;
using Wabbajack;
using Wabbajack.Blazor;
using Wabbajack.Blazor.Services;
using Wabbajack.DTOs.Interventions;
using Wabbajack.Models;
using Wabbajack.Paths.IO;
using Wabbajack.Services.OSIntegrated;

namespace Wabbajack.Blazor;

// Spike host: a cross-platform Photino.Blazor shell that renders ONLY the modlist gallery by reusing
// the existing Wabbajack.App.Core ViewModels. See docs plan "Photino.Blazor frontend for Wabbajack".
internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        var builder = PhotinoBlazorAppBuilder.CreateDefault(args);

        // Logging: write a rolling file (so there's an actual log to read/open), console, and the
        // in-app LogStream. Mirrors the WPF app's NLog setup.
        var logStream = new LogStream { Name = "ui", Layout = "${message:withexception=false}" };
        builder.Services.AddSingleton(logStream);
        builder.Services.AddLogging(logging =>
        {
            var config = new NLog.Config.LoggingConfiguration();

            var logFolder = KnownFolders.LauncherAwarePath.Combine("logs");
            if (!logFolder.DirectoryExists()) logFolder.CreateDirectory();

            var fileTarget = new FileTarget("file")
            {
                FileName = logFolder.Combine("Wabbajack.current.log").ToString(),
                ArchiveFileName = logFolder.Combine("Wabbajack.{##}.log").ToString(),
                ArchiveOldFileOnStartup = true,
                MaxArchiveFiles = 10,
                Layout = "${processtime} [${level:uppercase=true}] (${logger}) ${message:withexception=true}",
                Header = "############ Wabbajack log file - ${longdate} ############"
            };

            config.AddRuleForAllLevels(fileTarget);
            config.AddRuleForAllLevels(new ConsoleTarget("console"));
            config.AddRuleForAllLevels(logStream);

            logging.ClearProviders();
            logging.AddFilter("System.Net.Http.HttpClient", LogLevel.Warning);
            logging.SetMinimumLevel(LogLevel.Information);
            logging.AddNLog(config);
        });

        // Reuse the exact backend graph the Avalonia/WPF heads use.
        builder.Services.AddOSIntegrated();
        // Surface interventions as a modal (instead of throwing); the shell observes it.
        builder.Services.AddSingleton<Wabbajack.Blazor.Services.BlazorUserInterventionHandler>();
        builder.Services.AddSingleton<IUserInterventionHandler>(sp =>
            sp.GetRequiredService<Wabbajack.Blazor.Services.BlazorUserInterventionHandler>());

        // Platform abstractions Core needs, backed by Photino's native dialogs / the webview clipboard.
        // (The Stub* variants exist only for headless tests.)
        builder.Services.AddSingleton<PhotinoWindowHolder>();
        builder.Services.AddSingleton<IImageService, BlazorImageService>();
        builder.Services.AddSingleton<IDialogService, PhotinoDialogService>();
        builder.Services.AddSingleton<IFileSelector, PhotinoFileSelector>();
        builder.Services.AddSingleton<IClipboardService, PhotinoClipboardService>();
        builder.Services.AddSingleton<ISystemParameters, BlazorSystemParameters>();

        // Shell + screen VMs. NavigationVM is a singleton so the sidebar and the shell share one
        // ActiveScreen. The floating ModListDetailsVM is a singleton so it is already subscribed to
        // LoadModlistForDetails before a gallery tile sends it (otherwise MetadataVM would be null and
        // the panel would NRE — the same ordering bug we hit on the other heads).
        builder.Services.AddSingleton<NavigationVM>();
        builder.Services.AddSingleton<ModListDetailsVM>();
        builder.Services.AddSingleton<FileUploadVM>();
        builder.Services.AddTransient<HomeVM>();
        builder.Services.AddTransient<ModListGalleryVM>();
        // Singleton + created at shell init so it's subscribed to LoadInfoScreen before the message arrives.
        builder.Services.AddSingleton<InfoVM>();

        // Settings + its dependencies (mirrors the WPF/Avalonia app registrations).
        builder.Services.AddSingleton<Wabbajack.Networking.GitHub.Client>();
        builder.Services.AddSingleton(_ => new Octokit.GitHubClient(new Octokit.ProductHeaderValue("wabbajack")));
        builder.Services.AddSingleton<Wabbajack.LoginManagers.INeedsLogin, Wabbajack.LoginManagers.NexusLoginManager>();
        builder.Services.AddTransient<SettingsVM>();
        builder.Services.AddTransient<AboutVM>();

        // Compiler screens + deps. ResourceMonitor isn't registered by AddOSIntegrated (LogStream was
        // registered above as the NLog UI target).
        builder.Services.AddSingleton<Wabbajack.Models.ResourceMonitor>();
        builder.Services.AddTransient<CompilerHomeVM>();
        // Singleton + created at shell init: CompilerHome navigates to CompilerMain then sends
        // LoadCompilerSettings, so CompilerMain must already be subscribed (same lifetime requirement
        // as the installer).
        builder.Services.AddSingleton<CompilerMainVM>();
        builder.Services.AddTransient<CompilerDetailsVM>();
        builder.Services.AddTransient<CompilerFileManagerVM>();

        // Installer: singleton + created at shell init so it's subscribed to LoadModlistForInstalling /
        // LoadLastLoadedModlist BEFORE the gallery sends them and navigates (otherwise the message is
        // missed and it sticks on "Loading... Please wait"). ResourceMonitor/LogStream registered above.
        builder.Services.AddSingleton<InstallationVM>();

        builder.RootComponents.Add<App>("#app");

        var app = builder.Build();

        // Hand the window to the platform services now that it exists (native file/dialog pickers).
        app.Services.GetRequiredService<PhotinoWindowHolder>().Window = app.MainWindow;

        app.MainWindow
            .SetTitle("Wabbajack (Blazor spike)")
            .SetSize(1280, 800);

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            Console.Error.WriteLine($"[spike] Unhandled: {e.ExceptionObject}");

        app.Run();
    }
}
