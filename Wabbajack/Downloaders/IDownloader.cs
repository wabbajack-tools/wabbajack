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
        /// <summary>
        /// Setup the module. Run at the start of the application lifecycle
        /// </summary>
        void Init();

        AbstractDownloadState GetDownloaderState(dynamic archive_ini);
    }
}
