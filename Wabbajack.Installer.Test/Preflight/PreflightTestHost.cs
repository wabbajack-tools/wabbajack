#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Wabbajack.Downloaders;
using Wabbajack.Downloaders.GameFile;
using Wabbajack.Downloaders.Interfaces;
using Wabbajack.Downloaders.VerificationCache;
using Wabbajack.DTOs;
using Wabbajack.DTOs.DownloadStates;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Installer.Preflight;
using Wabbajack.Installer.Preflight.Rules;
using Wabbajack.Installer.Test.Preflight.Fakes;
using Wabbajack.Networking.WabbajackClientApi;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Wabbajack.RateLimiter;
using Wabbajack.VFS;

namespace Wabbajack.Installer.Test.Preflight;

/// <summary>
///     A throwaway install: its own temp root, its own hash cache and fakes for every seam. The registered
///     TemporaryFileManager is a singleton another test class disposes, so each host owns one instead.
/// </summary>
public sealed class PreflightTestHost : IDisposable
{
    /// <summary>
    ///     FileHashCache keeps its SQLite file open for the life of the object and cannot be disposed, so a
    ///     cache under the temp root would make TemporaryFileManager.Dispose retry for ten seconds. Caches
    ///     live here instead; each new host sweeps what earlier runs left behind.
    /// </summary>
    private static readonly AbsolutePath CacheRoot = KnownFolders.EntryPoint.Combine("preflight-test-caches");

    private readonly IServiceProvider _provider;

    static PreflightTestHost()
    {
        CacheRoot.CreateDirectory();
        foreach (var stale in CacheRoot.EnumerateFiles())
        {
            try
            {
                stale.Delete();
            }
            catch (Exception)
            {
                // Still open by another test in this process.
            }
        }
    }

    public PreflightTestHost(IServiceProvider provider)
    {
        _provider = provider;
        Manager = new TemporaryFileManager(KnownFolders.EntryPoint.Combine(Guid.NewGuid().ToString()));
        HashLimiter = new Resource<FileHashCache>("Test hashing", 2);
        Cache = new FileHashCache(CacheRoot.Combine(Guid.NewGuid() + ".sqlite"), HashLimiter);
        Limiter = new Resource<IInstaller>("Test installer", 4);
        DownloadLimiter = provider.GetRequiredService<IResource<DownloadDispatcher>>();
        Server = provider.GetRequiredService<FakeDownloadServer>();

        // The registered downloaders are singletons every test in the process shares, so a test that needs
        // one of them to behave differently gets its own. Only the Nexus fake has anything to say: whether
        // it prepares stands in for whether a usable Nexus login is stored.
        NexusDownloader = new FakeNexusDownloader(Server, provider.GetRequiredService<NexusDownloader>());
        Dispatcher = new DownloadDispatcher(provider.GetRequiredService<ILogger<DownloadDispatcher>>(),
            provider.GetServices<IDownloader>().Where(d => d is not FakeNexusDownloader).Append(NexusDownloader),
            DownloadLimiter, provider.GetRequiredService<Client>(),
            provider.GetRequiredService<IVerificationCache>());
        Acquirer = new ManualDownloadAcquirer(NullLogger<ManualDownloadAcquirer>.Instance, Cache, HashLimiter,
            Dispatcher, FastAcquirerOptions());

        GameFolder = Manager.CreateFolder().Path;
        Locator.Games[Game.SkyrimSpecialEdition] = GameFolder;
        foreach (var required in Game.SkyrimSpecialEdition.MetaData().RequiredFiles)
            GameFolder.Combine(required).WriteAllTextAsync("").Wait();

        Config = new InstallerConfiguration
        {
            Install = Manager.CreateFolder().Path,
            Downloads = Manager.CreateFolder().Path,
            Game = Game.SkyrimSpecialEdition,
            ModlistArchive = Manager.CreateFile(new Extension(".wabbajack")).Path,
            ModList = new ModList
            {
                Name = "Preflight test",
                GameType = Game.SkyrimSpecialEdition
            }
        };
    }

    public TemporaryFileManager Manager { get; }
    public FileHashCache Cache { get; }
    public IResource<FileHashCache> HashLimiter { get; }
    public IResource<IInstaller> Limiter { get; }
    public DownloadDispatcher Dispatcher { get; }
    public IResource<DownloadDispatcher> DownloadLimiter { get; }

    /// <summary>This host's own Nexus downloader, the one <see cref="Dispatcher" /> hands Nexus archives to.</summary>
    public FakeNexusDownloader NexusDownloader { get; }

    /// <summary>The bytes the fake downloaders serve; shared across the process, so key by unique URLs.</summary>
    public FakeDownloadServer Server { get; }

    /// <summary>A real acquirer on fast timings, disposed with the host.</summary>
    public ManualDownloadAcquirer Acquirer { get; }

    public FakeGameLocator Locator { get; } = new();

    /// <summary>
    ///     The game file restorer the context is built with, or null for a host that has no way to fetch
    ///     game files - which is what every test that is not about the repair wants.
    /// </summary>
    public IGameFileRestorer? Restorer { get; set; }

    public FakeNexusLoginProbe Nexus { get; } = new();
    public FakeDownloadPolicySource Policy { get; } = new();
    public InstallerConfiguration Config { get; }
    public AbsolutePath GameFolder { get; }

    /// <summary>No metrics and short retry pauses; everything else as production.</summary>
    public static PreflightOptions DefaultOptions()
    {
        return new PreflightOptions {SendMetrics = false, DownloadRetryDelay = TimeSpan.FromMilliseconds(20)};
    }

    public static ManualDownloadAcquirerOptions FastAcquirerOptions()
    {
        return new ManualDownloadAcquirerOptions
        {
            PollInterval = TimeSpan.FromMilliseconds(100),
            StableInterval = TimeSpan.FromMilliseconds(50),
            StableSamples = 2,
            LockRetryDelay = TimeSpan.FromMilliseconds(50),
            LockRetryCap = TimeSpan.FromMilliseconds(200)
        };
    }

    public PreflightContext Context(PreflightOptions? options = null, IManualDownloadAcquirer? acquirer = null)
    {
        return new PreflightContext(Config, options ?? DefaultOptions(), Locator, Cache, Dispatcher, DownloadLimiter,
            _provider.GetRequiredService<Client>(), Nexus, Policy, acquirer ?? Acquirer, Limiter, NullLogger.Instance,
            Restorer);
    }

    /// <summary>
    ///     What archive-inventory leaves behind for a modlist whose archives are all still needed: the
    ///     required set, and where on disk each one was found. Goes through the real
    ///     <see cref="ArchiveInventory.Scan" /> - the same call <c>AInstaller.HashArchives</c> makes - so a
    ///     check reading the result is reading exactly what the installer would see. Tests that want the
    ///     pruning itself have <c>RequiredArchivesTests</c>; this is for the checks that run after it.
    /// </summary>
    public async Task Inventory(PreflightContext ctx, CancellationToken token = default)
    {
        ctx.State.RequiredArchives = ctx.ModList.Archives;

        var folders = new List<AbsolutePath>();
        if (ctx.State.GameFolder != default) folders.Add(ctx.State.GameFolder);
        folders.AddRange(ctx.State.OtherGameFolders.Values.Where(p => p != default));

        ctx.Config.Downloads.CreateDirectory();
        var byHash = await ArchiveInventory.Scan(ctx.ModList.Archives, ctx.Config.Downloads, folders, Cache,
            Limiter, NullLogger.Instance, token);

        ctx.State.HashedArchives.Clear();
        foreach (var archive in ctx.ModList.Archives)
            if (byHash.TryGetValue(archive.Hash, out var path))
                ctx.State.HashedArchives[archive.Name] = path;
    }

    /// <summary>An archive for these bytes, without writing them anywhere.</summary>
    public static async Task<Archive> ArchiveFor(string name, string content, IDownloadState? state = null)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        return new Archive
        {
            Name = name,
            Size = bytes.Length,
            Hash = await bytes.Hash(),
            State = state ?? new Http {Url = new Uri("https://example.invalid/" + name)}
        };
    }

    /// <summary>Writes the bytes under <paramref name="folder" /> and returns the matching archive.</summary>
    public static async Task<Archive> WriteArchive(AbsolutePath folder, string name, string content,
        IDownloadState? state = null)
    {
        await WriteFile(folder.Combine(name), content);
        return await ArchiveFor(name, content, state);
    }

    public static async Task<AbsolutePath> WriteFile(AbsolutePath path, string content)
    {
        path.Parent.CreateDirectory();
        await path.WriteAllBytesAsync(Encoding.UTF8.GetBytes(content));
        return path;
    }

    public static async Task<Hash> HashOf(string content)
    {
        return await Encoding.UTF8.GetBytes(content).Hash();
    }

    public void Dispose()
    {
        Acquirer.DisposeAsync().AsTask().GetAwaiter().GetResult();
        Manager.Dispose();
    }
}
