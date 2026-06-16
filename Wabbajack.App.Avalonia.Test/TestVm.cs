using System;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Wabbajack;
using Wabbajack.DTOs;
using Wabbajack.DTOs.Interventions;
using Wabbajack.Models;
using Wabbajack.Networking.WabbajackClientApi;
using Wabbajack.Services.OSIntegrated;
using Wabbajack.Services.OSIntegrated.Services;

namespace WabbajackAvalonia.Test;

// Minimal offline DI container that builds Core ViewModels exactly like the WJ test harness:
// AddOSIntegrated with the local cache + stubbed game folders, and a throwing intervention handler.
public static class TestVm
{
    private static readonly IServiceProvider Sp = Build();

    private static IServiceProvider Build()
    {
        var s = new ServiceCollection();
        s.AddLogging();
        s.AddSingleton<IUserInterventionHandler, ThrowingUserInterventionHandler>();
        s.AddOSIntegrated(o =>
        {
            o.UseLocalCache = true;
            o.UseStubbedGameFolders = true;
        });
        s.AddTransient<global::Wabbajack.HomeVM>();
        s.AddTransient<global::Wabbajack.NavigationVM>();
        s.AddTransient<global::Wabbajack.SettingsVM>();
        s.AddTransient<global::Wabbajack.AboutVM>();

        // InstallationVM dependencies that AddOSIntegrated does not provide. ResourceMonitor/LogStream
        // (Wabbajack.Models) are pure offline models. The platform services reuse the Avalonia
        // implementations (offline-safe to construct: they only touch the UI/OS when invoked, which the
        // Configuration-state render tests never do). IDialogService is an inline no-op double.
        s.AddSingleton<global::Wabbajack.Models.ResourceMonitor>();
        s.AddSingleton<global::Wabbajack.Models.LogStream>();
        s.AddSingleton<global::Wabbajack.ISystemParameters, global::Wabbajack.AvaloniaSystemParameters>();
        s.AddSingleton<global::Wabbajack.IFileSelector, global::Wabbajack.AvaloniaFileSelector>();
        s.AddSingleton<global::Wabbajack.IImageService, NoOpImageService>();
        s.AddSingleton<global::Wabbajack.IDialogService, NoOpDialogService>();
        s.AddTransient<global::Wabbajack.InstallationVM>();
        // Compiler VMs. All their constructor deps are already provided by AddOSIntegrated (DTOSerializer,
        // SettingsManager, Client, CompilerSettingsInferencer, ITokenProvider<NexusOAuthState>, HttpClient,
        // DownloadDispatcher) plus the offline ResourceMonitor/LogStream/IFileSelector registered above.
        // None of these VMs start a compile or touch the network in their constructors (default
        // State == Configuration), so they construct fully offline.
        s.AddTransient<global::Wabbajack.CompilerHomeVM>();
        s.AddTransient<global::Wabbajack.CompilerMainVM>();
        s.AddTransient<global::Wabbajack.CompilerDetailsVM>();
        s.AddTransient<global::Wabbajack.CompilerFileManagerVM>();
        // AboutVM depends on the GitHub Client (+ its Octokit GitHubClient). AddOSIntegrated does not
        // register these, so wire them up exactly as the WPF app does (App.xaml.cs). Construction is
        // offline-safe: no network in the ctors, and AboutVM only fetches contributors on activation.
        s.AddSingleton<global::Wabbajack.Networking.GitHub.Client>();
        s.AddSingleton(_ => new Octokit.GitHubClient(new Octokit.ProductHeaderValue("wabbajack")));

        // Floating-window overlay + Info screen VMs (Wave 6). All offline:
        //   InfoVM            -> ILogger only.
        //   FileUploadVM      -> ILogger, WabbajackApiTokenProvider, Client (from AddOSIntegrated),
        //                        IClipboardService (no-op double below), IFileSelector (registered above).
        //   ModListDetailsVM  -> ILogger, IServiceProvider, Client (all offline). MetadataVM is supplied
        //                        via the LoadModlistForDetails message in the ModListDetails() factory.
        // None of these touch the network in their constructors.
        s.AddSingleton<global::Wabbajack.IClipboardService, NoOpClipboardService>();
        s.AddTransient<global::Wabbajack.InfoVM>();
        s.AddTransient<global::Wabbajack.FileUploadVM>();
        s.AddTransient<global::Wabbajack.ModListDetailsVM>();
        return s.BuildServiceProvider();
    }

    public static global::Wabbajack.HomeVM Home() => Sp.GetRequiredService<global::Wabbajack.HomeVM>();

    public static global::Wabbajack.NavigationVM Navigation() => Sp.GetRequiredService<global::Wabbajack.NavigationVM>();

    public static global::Wabbajack.SettingsVM Settings() => Sp.GetRequiredService<global::Wabbajack.SettingsVM>();

    public static global::Wabbajack.InstallationVM Installer() => Sp.GetRequiredService<global::Wabbajack.InstallationVM>();

    public static global::Wabbajack.CompilerHomeVM CompilerHome() => Sp.GetRequiredService<global::Wabbajack.CompilerHomeVM>();

    public static global::Wabbajack.CompilerMainVM CompilerMain() => Sp.GetRequiredService<global::Wabbajack.CompilerMainVM>();

    public static global::Wabbajack.InfoVM Info() => Sp.GetRequiredService<global::Wabbajack.InfoVM>();

    public static global::Wabbajack.FileUploadVM FileUpload() => Sp.GetRequiredService<global::Wabbajack.FileUploadVM>();

    // Resolves a ModListDetailsVM and publishes a LoadModlistForDetails carrying a fake tile so the VM's
    // MetadataVM is populated (the view binds MetadataVM.* and would NRE otherwise). The VM subscribes to
    // the message in its constructor, so the send must happen after it is resolved.
    public static global::Wabbajack.ModListDetailsVM ModListDetails()
    {
        var vm = Sp.GetRequiredService<global::Wabbajack.ModListDetailsVM>();
        Wabbajack.Messages.LoadModlistForDetails.Send(ModlistTile());
        return vm;
    }

    // Title used by the fake tile metadata; GalleryTests asserts a TextBlock renders this exact text.
    public const string TileTitle = "Fake Test Modlist";

    // Builds a BaseModListMetadataVM directly from FAKE metadata + offline services + a no-op image
    // service, so the gallery tile can be rendered and exercised fully offline (no network).
    public static BaseModListMetadataVM ModlistTile()
    {
        var metadata = new ModlistMetadata
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

        return new BaseModListMetadataVM(
            NullLogger<BaseModListMetadataVM>.Instance,
            metadata,
            Sp.GetRequiredService<ModListDownloadMaintainer>(),
            summary: null,
            Sp.GetRequiredService<Client>(),
            CancellationToken.None,
            new NoOpImageService());
    }

    // Deterministic, offline image service double: never touches the network and yields a null image,
    // which the tile ctor handles via its fire-and-forget error path. The tile still renders.
    private sealed class NoOpImageService : IImageService
    {
        public IObservable<object?> DownloadImage(IObservable<string?> urls, Action<Exception> onError,
            LoadingLock loadingLock) => Observable.Return<object?>(null);

        public object? FromStream(System.IO.Stream stream) => null;
    }

    // Offline IClipboardService double: never touches the system clipboard. FileUploadVM only invokes it
    // from CopyUrlCommand, which the render/close tests never execute.
    private sealed class NoOpClipboardService : IClipboardService
    {
        public Task SetTextAsync(string text) => Task.CompletedTask;
    }

    // Offline IDialogService double: never shows UI. The installer only invokes these when an install is
    // started (confirmation) or fails (error); the Configuration-state render tests never reach that.
    private sealed class NoOpDialogService : IDialogService
    {
        public void ShowError(string message, string title) { }

        public Task<bool> ShowConfirmation(string title, string message) => Task.FromResult(true);
    }
}
