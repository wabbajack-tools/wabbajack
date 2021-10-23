using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Wabbajack.BuildServer;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Wabbajack.VFS;

namespace Wabbajack.Server.Services;

/// <summary>
///     Maintains a concurrent cache of all the files we've downloaded, indexed by Hash.
/// </summary>
public class ArchiveMaintainer
{
    private readonly ILogger<ArchiveMaintainer> _logger;
    private readonly AppSettings _settings;

    public ArchiveMaintainer(ILogger<ArchiveMaintainer> logger, AppSettings settings)
    {
        _settings = settings;
        _logger = logger;
        _logger.Log(LogLevel.Information, "Creating Archive Maintainer");
    }

    private AbsolutePath ArchivePath(Hash hash)
    {
        return _settings.ArchivePath.Combine(hash.ToHex());
    }

    public async Task Ingest(AbsolutePath file)
    {
        var hash = await file.Hash(CancellationToken.None);
        if (hash == default) return;

        var newPath = ArchivePath(hash);
        if (HaveArchive(hash))
        {
            file.Delete();
            return;
        }

        await file.MoveToAsync(newPath, true, CancellationToken.None);
    }

    public bool HaveArchive(Hash hash)
    {
        return ArchivePath(hash).FileExists();
    }

    public bool TryGetPath(Hash hash, out AbsolutePath path)
    {
        path = ArchivePath(hash);
        return path.FileExists();
    }
}

public static class ArchiveMaintainerExtensions
{
    public static IServiceCollection UseArchiveMaintainer(this IServiceCollection b)
    {
        return b.AddSingleton<ArchiveMaintainer>();
    }
}