using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Wabbajack.Lib.Validation;

namespace Wabbajack.Lib.Downloaders
{
    public interface IDownloader
    {
        AbstractDownloadState GetDownloaderState(dynamic archive_ini);
        
        /// <summary>
        /// Called before any downloads are inacted by the installer;
        /// </summary>
        void Prepare();
    }

}
