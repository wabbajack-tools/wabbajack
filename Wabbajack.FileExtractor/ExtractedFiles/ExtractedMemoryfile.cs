using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Wabbajack.DTOs.Streams;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;

namespace Wabbajack.FileExtractor.ExtractedFiles;

public class ExtractedMemoryFile : IExtractedFile
{
    private readonly IStreamFactory _factory;

    public ExtractedMemoryFile(IStreamFactory factory)
    {
        _factory = factory;
    }


    public ValueTask<Stream> GetStream()
    {
        return _factory.GetStream();
    }

    public DateTime LastModifiedUtc => _factory.LastModifiedUtc;
    public IPath Name => _factory.Name;

    public async ValueTask Move(AbsolutePath newPath, CancellationToken token)
    {
        await using var stream = await _factory.GetStream();
        await newPath.WriteAllAsync(stream, token);
    }

    public bool CanMove { get; set; } = true;
}