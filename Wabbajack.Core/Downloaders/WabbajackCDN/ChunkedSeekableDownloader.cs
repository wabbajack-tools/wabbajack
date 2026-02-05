using System.Threading;
using System.Threading.Tasks;
using Wabbajack.Common;
using Wabbajack.DTOs.CDN;
using Wabbajack.DTOs.DownloadStates;
using Wabbajack.Networking.Http;

namespace Wabbajack.Downloaders;

public class ChunkedSeekableDownloader : AChunkedBufferingStream
{
    private readonly FileDefinition _definition;
    private readonly WabbajackCDNDownloader _downloader;
    private readonly WabbajackCDN _state;

    public ChunkedSeekableDownloader(WabbajackCDN state, FileDefinition definition, WabbajackCDNDownloader downloader) : base(21, definition.Size, 8)
    {
        _state = state;
        _downloader = downloader;
        _definition = definition;
    }

    public override async Task<byte[]> LoadChunk(long offset, int size)
    {
        var idx = offset >> 21;
        return await _downloader.GetPart(_state, _definition.Parts[idx], CancellationToken.None); 
    }
}