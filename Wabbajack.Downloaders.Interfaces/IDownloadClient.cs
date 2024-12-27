using System.Threading;
using System.Threading.Tasks;
using Wabbajack.Hashing.xxHash64;

namespace Wabbajack.Downloaders.Interfaces;

public interface IDownloadClient
{
    public Task<Hash> Download(CancellationToken token);
}
