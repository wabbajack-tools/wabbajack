using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wabbajack.Common;
using Wabbajack.DTOs.Streams;
using Wabbajack.DTOs.Vfs;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Networking.Http;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Wabbajack.VFS.Interfaces;

namespace Wabbajack.Networking.WabbajackClientApi;

public class CesiVFSCache : IVfsCache
{
    private readonly Client _client;
    private readonly ILogger<CesiVFSCache> _logger;
    private const int Threshold = 1024 * 1024 * 128;

    public CesiVFSCache(ILogger<CesiVFSCache> logger, Client client)
    {
        _logger = logger;
        _client = client;
    }
    
    public async Task<IndexedVirtualFile?> Get(Hash hash, IStreamFactory sf, CancellationToken token)
    {
        if (sf is not NativeFileStreamFactory nf)
            return null;
        if (nf.FullPath.Size() < Threshold) return null;
        
        try
        {
            var result = await _client.GetCesiVfsEntry(hash, token);
            _logger.LogInformation("Requesting CESI Information for: {Hash} - Found", hash.ToHex());
            return result;
        }
        catch (Exception exception)
        {
            _logger.LogInformation("Requesting CESI Information for: {Hash} - Not Found", hash.ToHex());
            return null;
        }
    }

    public async Task Put(IndexedVirtualFile file, CancellationToken token)
    {
        return;
    }

    public async Task Clean()
    {
        return;
    }
}