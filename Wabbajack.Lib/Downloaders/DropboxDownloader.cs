using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace Wabbajack.Lib.Downloaders
{
    public class DropboxDownloader : IDownloader, IUrlDownloader
    {
        public AbstractDownloadState GetDownloaderState(dynamic archive_ini)
        {
            var urlstring = archive_ini?.General?.directURL;
            return GetDownloaderState(urlstring);
        }

        public AbstractDownloadState GetDownloaderState(string url)
        {
            if (url == null) return null;
            var uri = new UriBuilder(url);
            if (uri.Host != "www.dropbox.com") return null;
            var query = HttpUtility.ParseQueryString(uri.Query);

            if (query.GetValues("dl").Length > 0)
                query.Remove("dl");

            query.Set("dl", "1");

            uri.Query = query.ToString();

            return new HTTPDownloader.State()
            {
                Url = uri.ToString().Replace("dropbox.com:443/", "dropbox.com/")
            };
        }

        public void Prepare()
        {
        }
    }
}
