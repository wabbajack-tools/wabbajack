using System;
using System.Runtime.CompilerServices;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using ReactiveUI;
using System.Reactive.Concurrency;
using Wabbajack;
using Wabbajack.Blazor.Services;
using Wabbajack.DTOs;
using Wabbajack.DTOs.Interventions;
using Wabbajack.Networking.WabbajackClientApi;
using Wabbajack.Services.OSIntegrated;
using Wabbajack.Services.OSIntegrated.Services;

namespace Wabbajack.Blazor.Test;

// Offline DI + fixtures for component tests, mirroring the old Avalonia TestVm harness: AddOSIntegrated
// with the local cache + stubbed game folders, the Blazor platform-service stubs, and the screen VMs.
// No test depends on network completing — VMs that load data do so fire-and-forget; we assert on the
// static markup / synchronous command behaviour only.
internal static class TestSupport
{
    public const string TileTitle = "Fake Test Modlist";

    // ReactiveCommands and ToProperty deliver on RxApp.MainThreadScheduler. Pin it to immediate so
    // command execution and the resulting messages are synchronous within a test.
    [ModuleInitializer]
    public static void Init() => RxApp.MainThreadScheduler = ImmediateScheduler.Instance;

    public static void Register(IServiceCollection s)
    {
        s.AddLogging();
        // Use the Blazor intervention handler (the shell injects it); interventions are never raised in tests.
        s.AddSingleton<Wabbajack.Blazor.Services.BlazorUserInterventionHandler>();
        s.AddSingleton<IUserInterventionHandler>(sp => sp.GetRequiredService<Wabbajack.Blazor.Services.BlazorUserInterventionHandler>());
        s.AddOSIntegrated(o =>
        {
            o.UseLocalCache = true;
            o.UseStubbedGameFolders = true;
        });
        s.AddSingleton<IImageService, BlazorImageService>();
        s.AddSingleton<IDialogService, StubDialogService>();
        s.AddSingleton<IFileSelector, StubFileSelector>();
        s.AddSingleton<IClipboardService, StubClipboardService>();
        s.AddSingleton<ISystemParameters, BlazorSystemParameters>();

        // ModListGalleryVM needs the concrete GameLocator; stubbed mode only registers IGameLocator.
        // GameLocator's ctor just takes a logger and doesn't scan until queried, so it's offline-safe.
        s.AddSingleton<Wabbajack.Downloaders.GameFile.GameLocator>();

        s.AddSingleton<NavigationVM>();
        s.AddSingleton<ModListDetailsVM>();
        s.AddSingleton<FileUploadVM>();
        s.AddTransient<HomeVM>();
        s.AddTransient<ModListGalleryVM>();
        s.AddSingleton<InfoVM>();

        // Settings + deps (mirrors the app).
        s.AddSingleton<Wabbajack.Networking.GitHub.Client>();
        s.AddSingleton(_ => new Octokit.GitHubClient(new Octokit.ProductHeaderValue("wabbajack")));
        s.AddSingleton<Wabbajack.LoginManagers.INeedsLogin, Wabbajack.LoginManagers.NexusLoginManager>();
        s.AddTransient<SettingsVM>();
        s.AddTransient<AboutVM>();

        // Compiler screens + deps not provided by AddOSIntegrated (ResourceMonitor, LogStream).
        // ResourceMonitor's ctor calls RxApp.MainThreadScheduler.ScheduleRecurringAction(1s, ...). With
        // our process-global scheduler pinned to ImmediateScheduler a *recurring* schedule degenerates
        // into an infinite synchronous loop, hanging construction. Build it with the scheduler temporarily
        // on the task pool (real delays, off-thread), then restore Immediate for the rest of the suite.
        s.AddSingleton<Wabbajack.Models.ResourceMonitor>(sp =>
        {
            var prev = RxApp.MainThreadScheduler;
            RxApp.MainThreadScheduler = TaskPoolScheduler.Default;
            try
            {
                return new Wabbajack.Models.ResourceMonitor(
                    sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<Wabbajack.Models.ResourceMonitor>>(),
                    sp.GetServices<Wabbajack.RateLimiter.IResource>());
            }
            finally
            {
                RxApp.MainThreadScheduler = prev;
            }
        });
        s.AddSingleton<Wabbajack.Models.LogStream>();
        s.AddTransient<CompilerHomeVM>();
        s.AddSingleton<CompilerMainVM>();
        s.AddTransient<CompilerDetailsVM>();
        s.AddTransient<CompilerFileManagerVM>();

        // Installer: singleton so it's subscribed to LoadModlistForInstalling before navigation (matches
        // the app). ResourceMonitor registered above with the scheduler-deadlock workaround.
        s.AddSingleton<InstallationVM>();
    }

    // Fake modlist metadata used across tests (tile rendering, navigation data-transfer).
    public static ModlistMetadata FakeMetadata() => new()
    {
        Title = TileTitle,
        Author = "Test Author",
        Game = Game.SkyrimSpecialEdition,
        NSFW = false,
        RepositoryName = "wj-tests",
        Links = new LinksObject { MachineURL = "fake-machine-url" },
        DownloadMetadata = new DownloadMetadata
        {
            SizeOfArchives = 1024L * 1024 * 1024,
            SizeOfInstalledFiles = 2L * 1024 * 1024 * 1024,
        },
    };

    // Builds a single gallery tile VM from fake metadata + the offline service graph.
    public static GalleryModListMetadataVM CreateTile(IServiceProvider sp)
    {
        var metadata = FakeMetadata();

        return new GalleryModListMetadataVM(
            NullLogger<GalleryModListMetadataVM>.Instance,
            sp.GetRequiredService<ModListGalleryVM>(),
            metadata,
            sp.GetRequiredService<ModListDownloadMaintainer>(),
            summary: null,
            sp.GetRequiredService<Client>(),
            CancellationToken.None,
            sp.GetRequiredService<IImageService>());
    }
}
