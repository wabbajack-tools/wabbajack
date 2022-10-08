using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wabbajack.Common;
using Wabbajack.Compiler.PatchCache;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Wabbajack.RateLimiter;

namespace Wabbajack.Compiler;

public class BinaryPatchCache : IBinaryPatchCache
{
    private readonly AbsolutePath _location;
    private readonly ILogger<BinaryPatchCache> _logger;

    public BinaryPatchCache(ILogger<BinaryPatchCache> logger, AbsolutePath location)
    {
        _logger = logger;
        _location = location;
        if (!_location.DirectoryExists())
            _location.CreateDirectory();
    }

    public AbsolutePath PatchLocation(Hash srcHash, Hash destHash)
    {
        return _location.Combine($"{srcHash.ToHex()}_{destHash.ToHex()}.octodiff");
    }

    public async Task<CacheEntry> CreatePatch(Stream srcStream, Hash srcHash, Stream destStream, Hash destHash, IJob? job)
    {

        var location = PatchLocation(srcHash, destHash);
        if (location.FileExists())
            return new CacheEntry(srcHash, destHash, location.Size(), this);
        
        await using var sigStream = new MemoryStream();
        var tempName = _location.Combine(Guid.NewGuid().ToString()).WithExtension(Ext.Temp);
        try
        {

            {
                await using var patchStream = tempName.Open(FileMode.Create, FileAccess.ReadWrite, FileShare.None);
                OctoDiff.Create(srcStream, destStream, sigStream, patchStream, job);
            }
            await tempName.MoveToAsync(location, true, CancellationToken.None);
        }
        finally
        {
            if (tempName.FileExists())
                tempName.Delete();
        }

        return new CacheEntry(srcHash, destHash, location.Size(), this);
    }


    public async Task<CacheEntry?> GetPatch(Hash fromHash, Hash toHash)
    {
        var location = PatchLocation(fromHash, toHash);
        if (location.FileExists())
            return new CacheEntry(fromHash, toHash, location.Size(), this);
        return null;
    }

    public async Task<byte[]> GetData(CacheEntry entry)
    {
        var location = PatchLocation(entry.From, entry.To);
        if (location.FileExists())
            return await location.ReadAllBytesAsync();
        return Array.Empty<byte>();
    }
}