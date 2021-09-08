using System.Threading;
using System.Threading.Tasks;

namespace Wabbajack.Lib.Downloaders
{
    public interface ICancellableDownloader : IDownloader
    {
        Task Prepare(CancellationToken cancellationToken);
    }
}
