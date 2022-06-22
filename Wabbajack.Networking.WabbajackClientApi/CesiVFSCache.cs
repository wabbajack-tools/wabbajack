using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wabbajack.DTOs.Vfs;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Networking.Http;
using Wabbajack.VFS.Interfaces;

namespace Wabbajack.Networking.WabbajackClientApi;

public class CesiVFSCache : IVfsCache
{
    private readonly Client _client;
    private readonly ILogger<CesiVFSCache> _logger;

    public CesiVFSCache(ILogger<CesiVFSCache> logger, Client client)
    {
        _logger = logger;
        _client = client;
    }
    
    public async Task<IndexedVirtualFile?> Get(Hash hash, CancellationToken token)
    {
        _logger.LogInformation("Requesting CESI Information for: {Hash}", hash.ToHex());
        try
        {
            return await _client.GetCesiVfsEntry(hash, token);
        }
        catch (HttpException exception)
        {
            return null;
        }
    }

    public async Task Put(IndexedVirtualFile file, CancellationToken token)
    {
        return;
    }
}