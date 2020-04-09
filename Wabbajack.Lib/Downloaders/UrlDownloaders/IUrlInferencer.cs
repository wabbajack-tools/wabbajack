using System;
using System.Threading.Tasks;
#nullable enable

namespace Wabbajack.Lib.Downloaders.UrlDownloaders
{
    public interface IUrlInferencer
    {
        Task<AbstractDownloadState?> Infer(Uri uri);
    }
}
