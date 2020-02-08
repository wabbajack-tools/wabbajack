using System;

namespace Wabbajack.Lib.Downloaders.UrlDownloaders
{
    public class BethesdaNetInferencer : IUrlInferencer
    {
        public AbstractDownloadState Infer(Uri uri)
        {
            return BethesdaNetDownloader.StateFromUrl(uri);
        }
    }
}
