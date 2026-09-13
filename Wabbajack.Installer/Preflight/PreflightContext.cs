using Microsoft.Extensions.Logging;
using Wabbajack.Downloaders;
using Wabbajack.Downloaders.GameFile;
using Wabbajack.DTOs;
using Wabbajack.Networking.WabbajackClientApi;
using Wabbajack.RateLimiter;
using Wabbajack.VFS;

namespace Wabbajack.Installer.Preflight;

/// <summary>
///     Everything a check needs: the install being prepared, the services to inspect it with, and the
///     <see cref="State" /> blackboard checks share.
/// </summary>
public sealed class PreflightContext
{
    public PreflightContext(InstallerConfiguration config, PreflightOptions options, IGameLocator gameLocator,
        FileHashCache hashCache, DownloadDispatcher dispatcher, IResource<DownloadDispatcher> downloadLimiter,
        Client wjClient, INexusLoginProbe nexusLogin, IDownloadPolicySource downloadPolicy,
        IManualDownloadAcquirer acquirer, IResource<IInstaller> limiter, ILogger logger)
    {
        Config = config;
        Options = options;
        GameLocator = gameLocator;
        HashCache = hashCache;
        Dispatcher = dispatcher;
        DownloadLimiter = downloadLimiter;
        WjClient = wjClient;
        NexusLogin = nexusLogin;
        DownloadPolicy = downloadPolicy;
        Acquirer = acquirer;
        Limiter = limiter;
        Logger = logger;
    }

    public InstallerConfiguration Config { get; }
    public ModList ModList => Config.ModList;
    public ModlistMetadata? Metadata => Config.Metadata;
    public PreflightOptions Options { get; }
    public IGameLocator GameLocator { get; }
    public FileHashCache HashCache { get; }
    public DownloadDispatcher Dispatcher { get; }

    /// <summary>
    ///     The limiter the dispatcher runs downloads under. Checks read its jobs for byte progress; the
    ///     dispatcher owns the throughput.
    /// </summary>
    public IResource<DownloadDispatcher> DownloadLimiter { get; }

    public Client WjClient { get; }
    public INexusLoginProbe NexusLogin { get; }
    public IDownloadPolicySource DownloadPolicy { get; }

    /// <summary>
    ///     Watches for the archives the user fetches by hand. Hosts drive it (skip, add a file, change the
    ///     watched folder) while manual-downloads is running.
    /// </summary>
    public IManualDownloadAcquirer Acquirer { get; }

    public IResource<IInstaller> Limiter { get; }
    public ILogger Logger { get; }
    public PreflightBlackboard State { get; } = new();
}
