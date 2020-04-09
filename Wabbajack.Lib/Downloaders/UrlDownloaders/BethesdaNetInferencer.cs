using System;
using System.Threading.Tasks;
#nullable enable

namespace Wabbajack.Lib.Downloaders.UrlDownloaders
{
    public class BethesdaNetInferencer : IUrlInferencer
    {
        public async Task<AbstractDownloadState?> Infer(Uri uri)
        {
            return BethesdaNetDownloader.StateFromUrl(uri);
        }
    }
}
