using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;

namespace Wabbajack.CLI.Services;

/// <summary>
///     Maintains a concurrent cache of all the files we've downloaded, indexed by Hash.
/// </summary>
public class ArchiveManager
{
    private readonly AbsolutePath _location;
    private ILogger _logger;

    public ArchiveManager(ILogger logger, AbsolutePath location)
    {
        _logger = logger;
        _location = location;
    }

    private AbsolutePath ArchivePath(Hash hash)
    {
        return _location.Combine(hash.ToHex());
    }

    public async Task Ingest(AbsolutePath file, CancellationToken token)
    {
        await using var tempPath = new TemporaryPath(_location.Combine("___" + Guid.NewGuid()));
        await using var fOut = tempPath.Path.Open(FileMode.Create, FileAccess.Write);
        await using var fIn = file.Open(FileMode.Open);
        var hash = await fIn.HashingCopy(fOut, token);
        fIn.Close();
        fOut.Close();
        if (hash == default) return;

        var newPath = ArchivePath(hash);
        if (HaveArchive(hash)) return;

        await tempPath.Path.MoveToAsync(newPath, false, token);
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

    public AbsolutePath GetPath(Hash hash)
    {
        if (!TryGetPath(hash, out var path))
            throw new FileNotFoundException($"Cannot find file for hash {hash}");
        return path;
    }
}