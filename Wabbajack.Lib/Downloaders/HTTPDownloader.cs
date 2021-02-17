using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Wabbajack.Common;
using Wabbajack.Common.Exceptions;
using Wabbajack.Common.Serialization.Json;
using Wabbajack.Lib.Validation;


namespace Wabbajack.Lib.Downloaders
{
    public class HTTPDownloader : IDownloader, IUrlDownloader
    {
        public async Task<AbstractDownloadState?> GetDownloaderState(dynamic archiveINI, bool quickMode)
        {
            var url = archiveINI?.General?.directURL;
            return GetDownloaderState(url, archiveINI);
        }

        public AbstractDownloadState? GetDownloaderState(string uri)
        {
            return GetDownloaderState(uri, null);
        }

        public AbstractDownloadState? GetDownloaderState(string url, dynamic? archiveINI)
        {
            if (url != null)
            {
                var tmp = new State(url);
                if (archiveINI?.General?.directURLHeaders != null)
                {
                    tmp.Headers.AddRange(archiveINI?.General.directURLHeaders.Split('|'));
                }
                return tmp;
            }

            return null;
        }

        public async Task Prepare()
        {
        }

        [JsonName("HttpDownloader")]
        public class State : AbstractDownloadState, IUpgradingState
        {
            public string Url { get; }

            public List<string> Headers { get; } = new List<string>();

            [JsonIgnore]
            public Wabbajack.Lib.Http.Client? Client { get; set; }

            [JsonIgnore]
            public override object[] PrimaryKey => new object[] { Url };

            public State(string url)
            {
                Url = url;
            }

            public override bool IsWhitelisted(ServerWhitelist whitelist)
            {
                return whitelist.AllowedPrefixes.Any(p => Url.StartsWith(p));
            }

            public override Task<bool> Download(Archive a, AbsolutePath destination)
            {
                return DoDownload(a, destination, true);
            }

            public async Task<bool> DoDownload(Archive a, AbsolutePath destination, bool download, CancellationToken? token = null)
            {
                if (download)
                {
                    destination.Parent.CreateDirectory();
                }

                await using var fs = download ? await destination.Create() : null;
                var client = Client ?? await ClientAPI.GetClient();
                client.Headers.Add(("User-Agent", Consts.UserAgent));

                foreach (var header in Headers)
                {
                    var idx = header.IndexOf(':');
                    var k = header.Substring(0, idx);
                    var v = header.Substring(idx + 1);
                    client.Headers.Add((k, v));
                }

                long totalRead = 0;
                const int bufferSize = 1024 * 32 * 8;

                Utils.Status($"Starting Download {a.Name ?? Url}", Percent.Zero);
                var response = await client.GetAsync(Url, errorsAsExceptions:false, retry:false, token:token);
                TOP:

                if (!response.IsSuccessStatusCode) 
                {
                    response.Dispose();
                    return false;
                }

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
                long headerContentSize = 0;
                if (response.Content.Headers.Contains("Content-Length"))
                {
                    headerVar = response.Content.Headers.GetValues("Content-Length").FirstOrDefault();
                    if (headerVar != null)
                        long.TryParse(headerVar, out headerContentSize);
                }

                if (!download)
                {
                    await stream.DisposeAsync();
                    response.Dispose();
                    if (a.Size != 0 && headerContentSize != 0)
                        return a.Size == headerContentSize;
                    return true;
                }

                var supportsResume = response.Headers.AcceptRanges.FirstOrDefault(f => f == "bytes") != null;

                var contentSize = headerVar != null ? long.Parse(headerVar) : 1;

                await using (var webs = stream)
                {
                    var buffer = new byte[bufferSize];
                    int readThisCycle = 0;

                    while (!(token ?? CancellationToken.None).IsCancellationRequested)
                    {
                        int read = 0;
                        try
                        {
                            read = await webs.ReadAsync(buffer, 0, bufferSize);
                        }
                        catch (Exception)
                        {
                            if (readThisCycle == 0)
                            {
                                await stream.DisposeAsync();
                                response.Dispose();
                                throw;
                            }

                            if (totalRead < contentSize)
                            {
                                if (!supportsResume)
                                {
                                    await stream.DisposeAsync();
                                    response.Dispose();
                                    throw;
                                }

                                Utils.Log(
                                    $"Abort during download, trying to resume {Url} from {totalRead.ToFileSizeString()}");

                                var msg = new HttpRequestMessage(HttpMethod.Get, Url);
                                msg.Headers.Range = new RangeHeaderValue(totalRead, null);
                                response.Dispose();
                                response = await client.SendAsync(msg);
                                goto TOP;
                            }

                            break;
                        }

                        readThisCycle += read;

                        if (read == 0) break;
                        Utils.Status($"Downloading {a.Name}", Percent.FactoryPutInRange(totalRead, contentSize));

                        fs!.Write(buffer, 0, read);
                        totalRead += read;
                    }
                }
                response.Dispose();
                return true;
            }

            public override async Task<bool> Verify(Archive a, CancellationToken? token)
            {
                return await DoDownload(a, ((RelativePath)"").RelativeToEntryPoint(), false, token: token);
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
                if (Headers.Count > 0)
                    return new [] {"[General]",
                          $"directURL={Url}",
                          $"directURLHeaders={string.Join("|", Headers)}"};
                else
                    return new [] {"[General]", $"directURL={Url}"};

            }

            public override async Task<(Archive? Archive, TempFile NewFile)> FindUpgrade(Archive a, Func<Archive, Task<AbsolutePath>> downloadResolver)
            {
                var tmpFile = new TempFile();
                
                var newArchive = new Archive(this) {Name = a.Name};

                Utils.Log($"Downloading via HTTP to find Upgrade for {Url}");

                try
                {
                    if (!await Download(newArchive, tmpFile.Path))
                        return default;
                }
                catch (HttpException ex)
                {
                    Utils.Log($"Error finding upgrade via HTTP to find Upgrade for {Url} {ex}");
                    return default;
                }

                var hash = await tmpFile.Path.FileHashAsync();
                if (hash == null) return default;
                newArchive.Hash = hash.Value;
                newArchive.Size = tmpFile.Path.Size;

                if (newArchive.Hash == a.Hash || a.Size > 2_500_000_000 || newArchive.Size > 2_500_000_000)
                {
                    return default;
                }

                return (newArchive, tmpFile);

            }

            public override async Task<bool> ValidateUpgrade(Hash srcHash, AbstractDownloadState newArchiveState)
            {
                var httpState = (State)newArchiveState;

                if (new Uri(httpState.Url).Host.EndsWith(".mediafire.com"))
                    return false;
                
                return httpState.Url == Url;
            }
        }
    }
}
