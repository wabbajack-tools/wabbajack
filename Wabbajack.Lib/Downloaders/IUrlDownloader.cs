namespace Wabbajack.Lib.Downloaders
{
    public interface IUrlDownloader : IDownloader
    {
        AbstractDownloadState? GetDownloaderState(string url);
    }
}
