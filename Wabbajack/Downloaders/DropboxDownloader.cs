using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace Wabbajack.Downloaders
{
    public class DropboxDownloader : IDownloader
    {
        public void Init()
        {

        }

        public AbstractDownloadState GetDownloaderState(dynamic archive_ini)
        {
            var urlstring = archive_ini?.General?.directURL;
            if (urlstring == null) return null;
            var uri = new UriBuilder((string)urlstring);
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
    }
}
