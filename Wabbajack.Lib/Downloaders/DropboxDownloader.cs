using System;
using System.Threading.Tasks;
using System.Web;
using Wabbajack.Common;

namespace Wabbajack.Lib.Downloaders
{
    public class DropboxDownloader : IDownloader, IUrlDownloader
    {
        public async Task<AbstractDownloadState> GetDownloaderState(dynamic archiveINI)
        {
            var urlstring = archiveINI?.General?.directURL;
            return GetDownloaderState(urlstring);
        }

        public AbstractDownloadState GetDownloaderState(string url)
        {
            try
            {
                if (url == null) return null;
                var uri = new UriBuilder(url);
                if (uri.Host != "www.dropbox.com") return null;
                var query = HttpUtility.ParseQueryString(uri.Query);

                if (query.GetValues("dl")?.Length > 0)
                    query.Remove("dl");

                query.Set("dl", "1");

                uri.Query = query.ToString();

                return new HTTPDownloader.State()
                {
                    Url = uri.ToString().Replace("dropbox.com:443/", "dropbox.com/")
                };
            }
            catch (Exception)
            {
                Utils.Error($"Error downloading Dropbox link: {url}");
                throw;
            }
        }

        public async Task Prepare()
        {
        }
    }
}
