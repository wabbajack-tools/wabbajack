using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Wabbajack.Validation;

namespace Wabbajack.Downloaders
{
    interface IDownloader
    {
        AbstractDownloadState GetDownloaderState(dynamic archive_ini);
    }
}
