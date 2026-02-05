using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Wabbajack.DTOs;

namespace Wabbajack.Downloaders.Interfaces;

public interface IProxyable : IUrlDownloader
{
    public Task<T> DownloadStream<T>(Archive archive, Func<Stream, Task<T>> fn, CancellationToken token);
}