using System.Threading.Tasks;

namespace Wabbajack.Lib.Downloaders
{
    public interface IDownloader
    {
        Task<AbstractDownloadState?> GetDownloaderState(dynamic archiveINI, bool quickMode = false);
        
        /// <summary>
        /// Called before any downloads are inacted by the installer;
        /// </summary>
        Task Prepare();
    }
}
