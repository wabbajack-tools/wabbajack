using System;
using System.IO;
using System.Threading.Tasks;
using Wabbajack.DTOs.Streams;
using Wabbajack.Paths;

namespace Wabbajack.Common;

public class MemoryStreamFactory : IStreamFactory
{
    private readonly MemoryStream _data;

    public MemoryStreamFactory(MemoryStream data, IPath path, DateTime lastModified)
    {
        _data = data;
        Name = path;
        LastModifiedUtc = lastModified;
    }

    public ValueTask<Stream> GetStream()
    {
        return new ValueTask<Stream>(new MemoryStream(_data.GetBuffer(), 0, (int) _data.Length));
    }

    public DateTime LastModifiedUtc { get; }
    public IPath Name { get; }
}