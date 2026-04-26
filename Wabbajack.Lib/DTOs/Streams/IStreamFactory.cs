using System;
using System.IO;
using System.Threading.Tasks;
using Wabbajack.Paths;

namespace Wabbajack.DTOs.Streams;

/// <summary>
///     A generic way of specifying a file-like source. Could be a in memory object
///     a file on disk, or a file inside an archive.
/// </summary>
public interface IStreamFactory
{
    DateTime LastModifiedUtc { get; }

    IPath Name { get; }
    ValueTask<Stream> GetStream();
}