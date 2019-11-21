namespace Wabbajack.Lib.Downloaders
{
    public interface IDownloader
    {
        AbstractDownloadState GetDownloaderState(dynamic archiveINI);
        
        /// <summary>
        /// Called before any downloads are inacted by the installer;
        /// </summary>
        void Prepare();
    }

}
