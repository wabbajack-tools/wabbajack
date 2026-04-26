using Wabbajack.DTOs.Streams;
using Wabbajack.DTOs.Vfs;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Paths;

namespace Wabbajack.VFS.Interfaces;

public interface IVfsCache
{
    public Task<IndexedVirtualFile?> Get(Hash hash, IStreamFactory sf, CancellationToken token);
    public Task Put(IndexedVirtualFile file, CancellationToken token);
    Task Clean();
}