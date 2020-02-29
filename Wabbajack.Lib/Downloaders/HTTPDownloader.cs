using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection.Emit;
using System.Threading.Tasks;
using Ceras;
using SharpCompress.Common;
using Wabbajack.Common;
using Wabbajack.Lib.Exceptions;
using Wabbajack.Lib.Validation;
using File = Alphaleonis.Win32.Filesystem.File;

namespace Wabbajack.Lib.Downloaders
{
    public class HTTPDownloader : IDownloader, IUrlDownloader
    {

        public async Task<AbstractDownloadState> GetDownloaderState(dynamic archiveINI)
        {
            var url = archiveINI?.General?.directURL;
            return GetDownloaderState(url, archiveINI);
        }

        public AbstractDownloadState GetDownloaderState(string uri)
        {
            return GetDownloaderState(uri, null);
        }

        public AbstractDownloadState GetDownloaderState(string url, dynamic archiveINI)
        {
            if (url != null)
            {
                var tmp = new State
                {
                    Url = url
                };
                if (archiveINI?.General?.directURLHeaders != null)
                {
                    tmp.Headers = new List<string>();
                    tmp.Headers.AddRange(archiveINI?.General.directURLHeaders.Split('|'));
                }
                return tmp;
            }

            return null;
        }

        public async Task Prepare()
        {
        }

        [MemberConfig(TargetMember.All)]
        public class State : AbstractDownloadState
        {
            public string Url { get; set; }

            public List<string> Headers { get; set; }

            [Exclude]
            public Common.Http.Client Client { get; set; }

            public override object[] PrimaryKey { get => new object[] {Url};}

            public override bool IsWhitelisted(ServerWhitelist whitelist)
            {
                return whitelist.AllowedPrefixes.Any(p => Url.StartsWith(p));
            }

            public override Task<bool> Download(Archive a, string destination)
            {
                return DoDownload(a, destination, true);
            }

            public async Task<bool> DoDownload(Archive a, string destination, bool download)
            {
                if (download)
                {
                    var parent = Directory.GetParent(destination);
                    if (!Directory.Exists(parent.FullName))
                        Directory.CreateDirectory(parent.FullName);
                }

                using (var fs = download ? File.Open(destination, FileMode.Create) : null)
                {
                    var client = Client ?? new Common.Http.Client();
                    client.Headers.Add(("User-Agent", Consts.UserAgent));

                    if (Headers != null)
                        foreach (var header in Headers)
                        {
                            var idx = header.IndexOf(':');
                            var k = header.Substring(0, idx);
                            var v = header.Substring(idx + 1);
                            client.Headers.Add((k, v));
                        }

                    long totalRead = 0;
                    var bufferSize = 1024 * 32;

                    Utils.Status($"Starting Download {a?.Name ?? Url}", Percent.Zero);
                    var response = await client.GetAsync(Url);
TOP:

                    if (!response.IsSuccessStatusCode)
                        throw new HttpException((int)response.StatusCode, response.ReasonPhrase);

                    Stream stream;
                    try
                    {
                        stream = await response.Content.ReadAsStreamAsync();
                    }
                    catch (Exception ex)
                    {
                        Utils.Error(ex, $"While downloading {Url}");
                        return false;
                    }


                    var headerVar = a.Size == 0 ? "1" : a.Size.ToString();
                    long header_content_size = 0;
                    if (response.Content.Headers.Contains("Content-Length"))
                    {
                        headerVar = response.Content.Headers.GetValues("Content-Length").FirstOrDefault();
                        if (headerVar != null)
                            long.TryParse(headerVar, out header_content_size);
                    }

                    if (!download)
                    {
                        if (a.Size != 0 && header_content_size != 0)
                            return a.Size == header_content_size;
                        return true;
                    }

                    var supportsResume = response.Headers.AcceptRanges.FirstOrDefault(f => f == "bytes") != null;

                    var contentSize = headerVar != null ? long.Parse(headerVar) : 1;

                    using (var webs = stream)
                    {
                        var buffer = new byte[bufferSize];
                        int readThisCycle = 0;

                        while (true)
                        {
                            int read = 0;
                            try
                            {
                                read = await webs.ReadAsync(buffer, 0, bufferSize);
                            }
                            catch (Exception ex)
                            {
                                if (readThisCycle == 0)
                                    throw ex;

                                if (totalRead < contentSize)
                                {
                                    if (supportsResume)
                                    {
                                        Utils.Log(
                                            $"Abort during download, trying to resume {Url} from {totalRead.ToFileSizeString()}");

                                        var msg = new HttpRequestMessage(HttpMethod.Get, Url);
                                        msg.Headers.Range = new RangeHeaderValue(totalRead, null);
                                        response.Dispose();
                                        response = await client.SendAsync(msg);
                                        goto TOP;
                                    }
                                    throw ex;
                                }

                                break;
                            }

                            readThisCycle += read;

                            if (read == 0) break;
                            Utils.Status($"Downloading {a.Name}", Percent.FactoryPutInRange(totalRead, contentSize));

                            fs.Write(buffer, 0, read);
                            totalRead += read;
                        }
                    }
                    response.Dispose();
                    return true;
                }
            }

            public override async Task<bool> Verify(Archive a)
            {
                return await DoDownload(a, "", false);
            }

            public override IDownloader GetDownloader()
            {
                return DownloadDispatcher.GetInstance<HTTPDownloader>();
            }

            public override string GetManifestURL(Archive a)
            {
                return Url;
            }

            public override string[] GetMetaIni()
            {
                if (Headers != null)
                    return new [] {"[General]",
                          $"directURL={Url}",
                          $"directURLHeaders={string.Join("|", Headers)}"};
                else
                    return new [] {"[General]", $"directURL={Url}"};

            }
        }
    }
}
