using System;
using Wabbajack.DTOs.DownloadStates;

namespace Wabbajack.Downloaders.Interfaces;

/// <summary>
///     Signifies that this downloader can be parsed/unparsed from a Url
/// </summary>
public interface IUrlDownloader : IDownloader
{
    public IDownloadState? Parse(Uri uri);
    public Uri UnParse(IDownloadState state);
}