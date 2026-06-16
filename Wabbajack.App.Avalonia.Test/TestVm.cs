using System;
using System.Reactive.Linq;
using System.Threading;
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
        return s.BuildServiceProvider();
    }

    public static global::Wabbajack.HomeVM Home() => Sp.GetRequiredService<global::Wabbajack.HomeVM>();

    public static global::Wabbajack.NavigationVM Navigation() => Sp.GetRequiredService<global::Wabbajack.NavigationVM>();

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
}
