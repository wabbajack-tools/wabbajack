using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.ServiceModel.Configuration;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Wabbajack.Common;
using Wabbajack.Validation;
using File = Alphaleonis.Win32.Filesystem.File;

namespace Wabbajack.Downloaders
{
    public class HTTPDownloader : IDownloader
    {

        public AbstractDownloadState GetDownloaderState(dynamic archive_ini)
        {
            var url = archive_ini?.General?.directURL;

            if (url != null)
            {
                var tmp = new State
                {
                    Url = url
                };
                if (archive_ini?.General?.directURLHeaders != null)
                {
                    tmp.Headers = new List<string>();
                    tmp.Headers.AddRange(archive_ini?.General.directURLHeaders.Split('|'));
                }
                return tmp;
            }

            return null;
        }

        public class State : AbstractDownloadState
        {
            public string Url { get; set; }

            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public List<string> Headers { get; set; }

            public override bool IsWhitelisted(ServerWhitelist whitelist)
            {
                return whitelist.AllowedPrefixes.Any(p => Url.StartsWith(p));
            }

            public override void Download(Archive a, string destination)
            {
                DoDownload(a, destination, true);
            }

            public bool DoDownload(Archive a, string destination, bool download)
            {
                var client = new HttpClient();
                client.DefaultRequestHeaders.Add("User-Agent", Consts.UserAgent);

                if (Headers != null)
                    foreach (var header in Headers)
                    {
                        var idx = header.IndexOf(':');
                        var k = header.Substring(0, idx);
                        var v = header.Substring(idx + 1);
                        client.DefaultRequestHeaders.Add(k, v);
                    }

                long total_read = 0;
                var buffer_size = 1024 * 32;

                var response = client.GetSync(Url);
                var stream = response.Content.ReadAsStreamAsync();
                try
                {
                    stream.Wait();
                }
                catch (Exception ex)
                {
                }

                ;
                if (stream.IsFaulted)
                {
                    Utils.Log($"While downloading {Url} - {stream.Exception.ExceptionToString()}");
                    return false;
                }

                if (!download)
                    return true;

                var header_var = "1";
                if (response.Content.Headers.Contains("Content-Length"))
                    header_var = response.Content.Headers.GetValues("Content-Length").FirstOrDefault();

                var content_size = header_var != null ? long.Parse(header_var) : 1;


                using (var webs = stream.Result)
                using (var fs = File.OpenWrite(destination))
                {
                    var buffer = new byte[buffer_size];
                    while (true)
                    {
                        var read = webs.Read(buffer, 0, buffer_size);
                        if (read == 0) break;
                        Utils.Status($"Downloading {a.Name}", (int)(total_read * 100 / content_size));

                        fs.Write(buffer, 0, read);
                        total_read += read;
                    }
                }

                return true;
            }

            public override bool Verify()
            {
                return DoDownload(new Archive {Name = ""}, "", false);
            }
        }
    }
}
