using System;

namespace Wabbajack.Lib.Downloaders.UrlDownloaders
{
    public interface IUrlInferencer
    {
        AbstractDownloadState Infer(Uri uri);
    }
}
