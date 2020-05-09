using System;
using System.Threading.Tasks;

namespace Wabbajack.Lib.Downloaders.UrlDownloaders
{
    public class WabbajackCDNInfluencer : IUrlInferencer
    {
        public async Task<AbstractDownloadState?> Infer(Uri uri)
        {
            return WabbajackCDNDownloader.StateFromUrl(uri);
        }
    }
}
