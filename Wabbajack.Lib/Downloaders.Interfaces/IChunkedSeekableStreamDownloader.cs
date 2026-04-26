using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Wabbajack.DTOs;

namespace Wabbajack.Downloaders.Interfaces;

public interface IChunkedSeekableStreamDownloader
{
    public ValueTask<Stream> GetChunkedSeekableStream(Archive archive, CancellationToken token);
}