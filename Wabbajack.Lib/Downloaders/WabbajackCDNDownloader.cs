using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.IO.MemoryMappedFiles;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Wabbajack.Common;
using Wabbajack.Common.Exceptions;
using Wabbajack.Common.Serialization.Json;
using Wabbajack.Lib.AuthorApi;
using Wabbajack.Lib.Validation;

namespace Wabbajack.Lib.Downloaders
{
    
    public class WabbajackCDNDownloader : IDownloader, IUrlDownloader
    {
        public static Dictionary<string, string> DomainRemaps = new Dictionary<string, string>
        {
            {"wabbajack.b-cdn.net", "authored-files.wabbajack.org"},
            {"wabbajack-mirror.b-cdn.net", "mirror.wabbajack.org"},
            {"wabbajack-patches.b-cdn.net", "patches.wabbajack.org"},
            {"wabbajacktest.b-cdn.net", "test-files.wabbajack.org"}
        };

        
        public string[]? Mirrors;
        public long TotalRetries;
        
        public async Task<AbstractDownloadState?> GetDownloaderState(dynamic archiveINI, bool quickMode = false)
        {
            var url = (Uri)DownloaderUtils.GetDirectURL(archiveINI);
            return url == null ? null : StateFromUrl(url);
        }

        public async Task Prepare()
        {
        }

        public AbstractDownloadState? GetDownloaderState(string url)
        {
            return StateFromUrl(new Uri(url));
        }


        public static AbstractDownloadState? StateFromUrl(Uri url)
        {
            if (DomainRemaps.ContainsKey(url.Host) || DomainRemaps.ContainsValue(url.Host))
            {
                return new State(url);
            }
            return null;
        }

        [JsonName("WabbajackCDNDownloader+State")]
        public class State : AbstractDownloadState
        {
            public Uri Url { get; set; }
            public State(Uri url)
            {
                Url = url;
            }

            public override object[] PrimaryKey => new object[] {Url};
            public override bool IsWhitelisted(ServerWhitelist whitelist)
            {
                return true;
            }

            public override async Task<bool> Download(Archive a, AbsolutePath destination)
            {
                destination.Parent.CreateDirectory();
                var definition = await GetDefinition();
                await using var fs = await destination.Create();
                using var mmfile = MemoryMappedFile.CreateFromFile(fs, null, definition.Size, MemoryMappedFileAccess.ReadWrite, HandleInheritability.None, false);
                var client = new Wabbajack.Lib.Http.Client();
                
                if (!DomainRemaps.ContainsKey(Url.Host)) 
                    client.Headers.Add(("Host", Url.Host));
                
                using var queue = new WorkQueue();
                await definition.Parts.PMap(queue, async part =>
                {
                    Utils.Status($"Downloading {a.Name}", Percent.FactoryPutInRange(definition.Parts.Length - part.Index, definition.Parts.Length));
                    await using var ostream = mmfile.CreateViewStream(part.Offset, part.Size);

                    if (DomainRemaps.TryGetValue(Url.Host, out var remap))
                    {
                        var builder = new UriBuilder(Url) {Host = remap};
                        using var response = await client.GetAsync($"{builder}/parts/{part.Index}");
                        if (!response.IsSuccessStatusCode)
                            throw new HttpException((int)response.StatusCode, response.ReasonPhrase ?? "Unknown");
                        await response.Content.CopyToAsync(ostream);
                        
                    }
                    else
                    {
                        using var response = await GetWithMirroredRetry(client, $"{Url}/parts/{part.Index}");
                        if (!response.IsSuccessStatusCode)
                            throw new HttpException((int)response.StatusCode, response.ReasonPhrase ?? "Unknown");
                        await response.Content.CopyToAsync(ostream);

                    }

                });
                return true;
            }

            public override async Task<bool> Verify(Archive archive, CancellationToken? token)
            {
                var definition = await GetDefinition(token);
                return true;
            }

            private async Task<HttpResponseMessage> GetWithMirroredRetry(Http.Client client, string url)
            {
                int retries = 0;
                var downloader = DownloadDispatcher.GetInstance<WabbajackCDNDownloader>();
                if (downloader.Mirrors != null)
                    url = ReplaceHost(downloader.Mirrors, url);
                
                TOP:

                try
                {
                    return await client.GetAsync(url, retry: false);
                }
                catch (Exception ex)
                {
                    if (retries > 5)
                    {
                        Utils.Log($"Tried to read from {retries} CDN servers, giving up");
                        throw;
                    }
                    Utils.Log($"Error reading {url} retying with a mirror");
                    Utils.Log(ex.ToString());
                    downloader.Mirrors ??= await ClientAPI.GetCDNMirrorList();
                    url = ReplaceHost(downloader.Mirrors, url);
                    retries += 1;
                    Interlocked.Increment(ref downloader.TotalRetries);
                    goto TOP;
                }
            }

            private string ReplaceHost(string[] hosts, string url)
            {
                var rnd = new Random();
                var builder = new UriBuilder(url) {Host = hosts[rnd.Next(0, hosts.Length)]};
                return builder.ToString();
            }
            
            private async Task<CDNFileDefinition> GetDefinition(CancellationToken? token = null)
            {
                var client = new Wabbajack.Lib.Http.Client();
                if (DomainRemaps.TryGetValue(Url.Host, out var remap))
                {
                    var builder = new UriBuilder(Url) {Host = remap};
                    using var data = await client.GetAsync(builder + "/definition.json.gz", token: token);
                    await using var gz = new GZipStream(await data.Content.ReadAsStreamAsync(),
                        CompressionMode.Decompress);
                    return gz.FromJson<CDNFileDefinition>();
                }
                else
                {
                    client.Headers.Add(("Host", Url.Host));
                    using var data = await GetWithMirroredRetry(client, Url + "/definition.json.gz");
                    await using var gz = new GZipStream(await data.Content.ReadAsStreamAsync(),
                        CompressionMode.Decompress);
                    return gz.FromJson<CDNFileDefinition>();
                }
            }

            public override IDownloader GetDownloader()
            {
                return DownloadDispatcher.GetInstance<WabbajackCDNDownloader>();
            }

            public override string? GetManifestURL(Archive a)
            {
                return Url.ToString();
            }

            public override string[] GetMetaIni()
            {
                return new[] {"[General]", $"directURL={Url}"};
            }
        }


    }
}
