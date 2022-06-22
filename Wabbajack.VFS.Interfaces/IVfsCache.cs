using Wabbajack.DTOs.Vfs;
using Wabbajack.Hashing.xxHash64;

namespace Wabbajack.VFS.Interfaces;

public interface IVfsCache
{
    public Task<IndexedVirtualFile?> Get(Hash hash, CancellationToken token);
    public Task Put(IndexedVirtualFile file, CancellationToken token);
}