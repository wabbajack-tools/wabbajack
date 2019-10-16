using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wabbajack.Lib.Downloaders
{
    public interface IUrlDownloader : IDownloader
    {
        AbstractDownloadState GetDownloaderState(string url);
    }
}
