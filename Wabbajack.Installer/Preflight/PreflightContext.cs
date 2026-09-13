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
        FileHashCache hashCache, DownloadDispatcher dispatcher, Client wjClient, INexusLoginProbe nexusLogin,
        IDownloadPolicySource downloadPolicy, IResource<IInstaller> limiter, ILogger logger)
    {
        Config = config;
        Options = options;
        GameLocator = gameLocator;
        HashCache = hashCache;
        Dispatcher = dispatcher;
        WjClient = wjClient;
        NexusLogin = nexusLogin;
        DownloadPolicy = downloadPolicy;
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
    public Client WjClient { get; }
    public INexusLoginProbe NexusLogin { get; }
    public IDownloadPolicySource DownloadPolicy { get; }
    public IResource<IInstaller> Limiter { get; }
    public ILogger Logger { get; }
    public PreflightBlackboard State { get; } = new();
}
