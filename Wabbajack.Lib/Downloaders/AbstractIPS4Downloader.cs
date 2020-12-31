using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using F23.StringSimilarity;
using HtmlAgilityPack;
using Newtonsoft.Json;
using Wabbajack.Common;
using Wabbajack.Lib.LibCefHelpers;
using Wabbajack.Lib.Validation;
using Wabbajack.Lib.WebAutomation;

namespace Wabbajack.Lib.Downloaders
{

    interface IWaitForWindowDownloader
    {
        public Task WaitForNextRequestWindow();
    }
    // IPS4 is the site used by LoversLab, VectorPlexus, etc. the general mechanics of each site are the 
    // same, so we can fairly easily abstract the state.
    // Pass in the state type via TState
    public abstract class AbstractIPS4Downloader<TDownloader, TState> : AbstractNeedsLoginDownloader, IDownloader, IWaitForWindowDownloader
        where TState : AbstractIPS4Downloader<TDownloader, TState>.State<TDownloader>, new()
        where TDownloader : IDownloader
    {

        private DateTime LastRequestTime = default;
        protected long RequestsPerMinute = 20;
        private TimeSpan RequestDelay => TimeSpan.FromMinutes(1) / RequestsPerMinute;

        protected AbstractIPS4Downloader(Uri loginUri, string encryptedKeyName, string cookieDomain, string loginCookie = "ips4_member_id")
            : base(loginUri, encryptedKeyName, cookieDomain, loginCookie)
        {
        }

        public async Task<AbstractDownloadState?> GetDownloaderState(dynamic archiveINI, bool quickMode)
        { 
            Uri url = DownloaderUtils.GetDirectURL(archiveINI);
            return await GetDownloaderStateFromUrl(url, quickMode);
        }

        public async Task WaitForNextRequestWindow()
        {
            TimeSpan delay;
            lock (this)
            {
                if (LastRequestTime < DateTime.UtcNow - RequestDelay)
                {
                    LastRequestTime = DateTime.UtcNow;
                    delay = TimeSpan.Zero;
                }
                else
                {
                    LastRequestTime += RequestDelay;
                    delay = LastRequestTime - DateTime.UtcNow;
                }
            }

            Utils.Log($"Waiting for {delay.TotalSeconds} to make request via {typeof(TDownloader).Name}");
            await Task.Delay(delay);
        }

        public async Task<AbstractDownloadState?> GetDownloaderStateFromUrl(Uri url, bool quickMode)
        {
            var absolute = true;
            if (url == null || url.Host != SiteURL.Host) return null;

            if (url.PathAndQuery.StartsWith("/applications/core/interface/file/attachment"))
            {
                return new TState
                {
                    IsAttachment = true,
                    FullURL = url.ToString()
                };
            }

            if (url.PathAndQuery.StartsWith("/index.php?"))
            {
                var id2 = HttpUtility.ParseQueryString(url.Query)["r"];
                var parsed = HttpUtility.ParseQueryString(url.Query);
                var name = parsed[null].Split("/", StringSplitOptions.RemoveEmptyEntries).Last();
                return new TState
                {
                    FullURL = url.AbsolutePath,
                    FileID = id2,
                    FileName = name
                };
            }

            if (url.PathAndQuery.StartsWith("/files/getdownload"))
            {
                return new TState
                {
                    FullURL = url.ToString(), 
                    IsAttachment = true
                };
            }
            
            
            if (url.PathAndQuery.StartsWith("/files/download/") && long.TryParse(url.PathAndQuery.Split("/").Last(), out var fileId))
            {
                return new TState
                {
                    FullURL = url.ToString(),
                    IsAttachment = true
                };
            }
           
            if (!url.PathAndQuery.StartsWith("/files/file/"))
            {
                if (string.IsNullOrWhiteSpace(url.Query)) return null;
                if (!url.Query.Substring(1).StartsWith("/files/file/")) return null;
                absolute = false;
            }


            var id = HttpUtility.ParseQueryString(url.Query)["r"];
            var file = absolute
                ? url.AbsolutePath.Split('/').Last(s => s != "")
                : url.Query.Substring(1).Split('/').Last(s => s != "");
            
            return new TState
            {
                FullURL = url.AbsolutePath,
                FileID = id,
                FileName = file
            };
        }
        
        public class State<TStateDownloader> : AbstractDownloadState, IMetaState 
            where TStateDownloader : IDownloader
        {
            public string FullURL { get; set; } = string.Empty;
            public bool IsAttachment { get; set; }
            public string FileID { get; set; } = string.Empty;

            public string FileName { get; set; } = string.Empty;
            
            // from IMetaState
            public Uri URL => IsAttachment ? new Uri("https://www.wabbajack.org/") : new Uri($"{Site}/files/file/{FileName}");
            public string? Name { get; set; }
            public string? Author { get; set; }
            public string? Version { get; set; }
            public Uri? ImageURL { get; set; }
            public virtual bool IsNSFW { get; set; }
            public string? Description { get; set; }

            private static bool IsHTTPS => Downloader.SiteURL.AbsolutePath.StartsWith("https://");
            private static string URLPrefix => IsHTTPS ? "https://" : "http://";

            [JsonIgnore]
            public static string Site => string.IsNullOrWhiteSpace(Downloader.SiteURL.Query)
                ? $"{URLPrefix}{Downloader.SiteURL.Host}"
                : Downloader.SiteURL.ToString();

            public static AbstractNeedsLoginDownloader Downloader => (AbstractNeedsLoginDownloader)(object)DownloadDispatcher.GetInstance<TDownloader>();

            [JsonIgnore]
            public override object[] PrimaryKey
            {
                get
                {
                    return string.IsNullOrWhiteSpace(FileID)
                        ? IsAttachment 
                            ? new object[] {Downloader.SiteURL, IsAttachment, FullURL}
                            : new object[] {Downloader.SiteURL, FileName}
                        : new object[] {Downloader.SiteURL, FileName, FileID};
                }
            }

            public override bool IsWhitelisted(ServerWhitelist whitelist)
            {
                return true;
            }

            public override async Task<bool> Download(Archive a, AbsolutePath destination)
            {
                await ((IWaitForWindowDownloader)Downloader).WaitForNextRequestWindow();
                return await ResolveDownloadStream(a, destination, false);
            }

            private async Task<bool> ResolveDownloadStream(Archive a, AbsolutePath path, bool quickMode, CancellationToken? token = null)
            {

                
                TOP:
                string url;
                if (IsAttachment)
                {
                    url = FullURL;
                }
                else
                {
                    var csrfURL = string.IsNullOrWhiteSpace(FileID)
                        ? $"{Site}/files/file/{FileName}/?do=download"
                        : $"{Site}/files/file/{FileName}/?do=download&r={FileID}";
                    var html = await GetStringAsync(new Uri(csrfURL), token);

                    var pattern = new Regex("(?<=csrfKey=).*(?=[&\"\'])|(?<=csrfKey: \").*(?=[&\"\'])");
                    var matches = pattern.Matches(html).Cast<Match>();
                    
                    var csrfKey = matches.Where(m => m.Length == 32).Select(m => m.ToString()).FirstOrDefault();

                    if (!Downloader.IsCloudFlareProtected && csrfKey == null)
                    {
                        Utils.Log($"Returning null from IPS4 Downloader because no csrfKey was found");
                        return false;
                    }

                    var sep = Site.EndsWith("?") ? "&" : "?";
                    url = FileID == null
                        ? $"{Site}/files/file/{FileName}/{sep}do=download&confirm=1&t=1&csrfKey={csrfKey}"
                        : $"{Site}/files/file/{FileName}/{sep}do=download&r={FileID}&confirm=1&t=1&csrfKey={csrfKey}";
                }
                
                if (Downloader.IsCloudFlareProtected)
                {
                    using var driver = await Downloader.GetAuthedDriver();
                    var size = await driver.NavigateToAndDownload(new Uri(url), path, quickMode: quickMode, token);

                    if (a.Size == 0 || size == 0 || a.Size == size) return true;

                    Utils.Log($"Bad Header Content sizes {a.Size} vs {size}");
                    return false;

                }

                var streamResult = await GetDownloadAsync(new Uri(url));
                if (streamResult.StatusCode != HttpStatusCode.OK)
                {
                    Utils.ErrorThrow(new InvalidOperationException(), $"{Downloader.SiteName} servers reported an error for file: {FileID}");
                }

                var contentType = streamResult.Content.Headers.ContentType;

                if (contentType.MediaType != "application/json")
                {
                    var headerVar = a.Size == 0 ? "1" : a.Size.ToString();
                    long headerContentSize = 0;
                    if (streamResult.Content.Headers.Contains("Content-Length"))
                    {
                        headerVar = streamResult.Content.Headers.GetValues("Content-Length").FirstOrDefault();
                        if (headerVar != null)
                            long.TryParse(headerVar, out headerContentSize);
                    }
                    
                    if (a.Size != 0 && headerContentSize != 0 && a.Size != headerContentSize)
                    {
                        Utils.Log($"Bad Header Content sizes {a.Size} vs {headerContentSize}");
                        return false;
                    }

                    await using (var os = await path.Create())
                    await using (var ins = await streamResult.Content.ReadAsStreamAsync())
                    {
                        if (a.Size == 0)
                        {
                            Utils.Status($"Downloading {a.Name}");
                            await ins.CopyToAsync(os);
                        }
                        else
                        {
                            await ins.CopyToWithStatusAsync(headerContentSize, os, $"Downloading {a.Name}");
                        }
                    }

                    streamResult.Dispose();

                    return true;
                }

                // Sometimes LL hands back a json object telling us to wait until a certain time
                var times = (await streamResult.Content.ReadAsStringAsync()).FromJsonString<WaitResponse>();
                var secs = times.Download - times.CurrentTime;
                for (int x = 0; x < secs; x++)
                {
                    if (quickMode) return true;
                    Utils.Status($"Waiting for {secs} at the request of {Downloader.SiteName}", Percent.FactoryPutInRange(x, secs));
                    Utils.Log($"Waiting for {secs} at the request of {Downloader.SiteName}, {secs - x} remaining");
                    await Task.Delay(1000);
                }
                streamResult.Dispose();
                Utils.Status("Retrying download");
                goto TOP;
            }

            private async Task DeleteOldDownloadCookies(Driver driver)
            {
                await driver.DeleteCookiesWhere(c => c.Name.StartsWith("ips4_downloads_delay_") && c.Value == "-1");
            }

            private class WaitResponse
            {
                [JsonProperty("download")]
                public int Download { get; set; }
                [JsonProperty("currentTime")]
                public int CurrentTime { get; set; }
            }

            public override async Task<bool> Verify(Archive a, CancellationToken? token)
            {
                await ((IWaitForWindowDownloader)Downloader).WaitForNextRequestWindow();
                await using var tp = new TempFile();
                var isValid = await ResolveDownloadStream(a, tp.Path, true, token: token);
                return isValid;
            }
            
            public override IDownloader GetDownloader()
            {
                return DownloadDispatcher.GetInstance<TDownloader>();
            }

            public override async Task<(Archive? Archive, TempFile NewFile)> FindUpgrade(Archive a, Func<Archive, Task<AbsolutePath>> downloadResolver)
            {
                await ((IWaitForWindowDownloader)Downloader).WaitForNextRequestWindow();
                var files = await GetFilesInGroup();
                var nl = new Levenshtein();

                foreach (var newFile in files.OrderBy(f => nl.Distance(a.Name.ToLowerInvariant(), f.Name.ToLowerInvariant())))
                {
                    /*
                    var existing = await downloadResolver(newFile);
                    if (existing != default) return (newFile, new TempFile());*/

                    var tmp = new TempFile();
                    await DownloadDispatcher.PrepareAll(new[] {newFile.State});
                    if (await newFile.State.Download(newFile, tmp.Path))
                    {
                        newFile.Size = tmp.Path.Size;
                        newFile.Hash = await tmp.Path.FileHashAsync();
                        return (newFile, tmp);
                    }

                    await tmp.DisposeAsync();
                }
                return default;

            }

            public override async Task<bool> ValidateUpgrade(Hash srcHash, AbstractDownloadState newArchiveState)
            {
                return !string.IsNullOrWhiteSpace(FileID);
            }

            public async Task<List<Archive>> GetFilesInGroup()
            {
                var token = new CancellationTokenSource();
                token.CancelAfter(TimeSpan.FromMinutes(10));
                
                var others = await GetHtmlAsync(new Uri($"{Site}/files/file/{FileName}?do=download"), token.Token);

                var pairs = others.DocumentNode.SelectNodes("//a[@data-action='download']")
                    .Select(item => (item.GetAttributeValue("href", ""),
                        item.ParentNode.ParentNode.SelectNodes("//div//h4//span").First().InnerText));
                
                List<Archive> archives = new List<Archive>();
                foreach (var (url, name) in pairs)
                {
                    var urlDecoded = HttpUtility.HtmlDecode(url);
                    var ini = new[] {"[General]", $"directURL={urlDecoded}"};
                    var state = (AbstractDownloadState)(await DownloadDispatcher.ResolveArchive(
                        string.Join("\n", ini).LoadIniString(), false));
                    if (state == null) continue;
                    
                    archives.Add(new Archive(state) {Name = name});
                    
                }

                return archives;
            }

            public override string GetManifestURL(Archive a)
            {
                return IsAttachment ? FullURL : $"{Site}/files/file/{FileName}/?do=download&r={FileID}";
            }

            public async Task<string> GetStringAsync(Uri uri, CancellationToken? token = null)
            {
                if (!Downloader.IsCloudFlareProtected)
                    return await Downloader.AuthedClient.GetStringAsync(uri);

                
                using var driver = await Downloader.GetAuthedDriver();
                await ((IWaitForWindowDownloader)Downloader).WaitForNextRequestWindow();
                await DeleteOldDownloadCookies(driver);

                //var drivercookies = await Helpers.GetCookies("loverslab.com");
                
                //var cookies = await ClientAPI.GetAuthInfo<Helpers.Cookie[]>("loverslabcookies");
                //await Helpers.IngestCookies(uri.ToString(), cookies);
                await driver.NavigateTo(uri, token);
                if ((token ?? CancellationToken.None).IsCancellationRequested)
                    return "";

                var source = await driver.GetSourceAsync();
                
                
                /*
                Downloader.AuthedClient.Cookies.Add(drivercookies.Where(dc => dc.Name == "cf_clearance")
                    .Select(dc => new Cookie
                    {
                        Name = dc.Name,
                        Domain = dc.Domain,
                        Value = dc.Value,
                        Path = dc.Path
                    })
                    .FirstOrDefault());

                var source = await Downloader.AuthedClient.GetStringAsync(uri);
                */
                return source;
            }
            
            public async Task<HtmlDocument> GetHtmlAsync(Uri s, CancellationToken? token = null)
            {
                var body = await GetStringAsync(s, token);
                var doc = new HtmlDocument();
                doc.LoadHtml(body);
                return doc;
            }

            public async Task<HttpResponseMessage> GetDownloadAsync(Uri uri)
            {
                if (!Downloader.IsCloudFlareProtected)
                    return await Downloader.AuthedClient.GetAsync(uri);

                using var driver = await Downloader.GetAuthedDriver();
                TaskCompletionSource<Uri?> promise = new TaskCompletionSource<Uri?>();
                driver.DownloadHandler = uri1 =>
                {
                    promise.SetResult(uri);
                };
                await driver.NavigateTo(uri);

                var url = await promise.Task;
                if (url == null) throw new Exception("No Url to download");
                var location = await driver.GetLocation();
                return await Helpers.GetClient(await Helpers.GetCookies(), location!.ToString()).GetAsync(uri);
            }

            public override string[] GetMetaIni()
            {
                if (IsAttachment)
                    return new[] {"[General]", $"directURL={FullURL}"};

                if (FileID == null)
                    return new[] {"[General]", $"directURL={Site}/files/file/{FileName}"};

                if (Site.EndsWith("?"))
                {
                    return new[]
                    {
                        "[General]", $"directURL={Site}/files/file/{FileName}&do=download&r={FileID}&confirm=1&t=1"
                    };
                        
                }

                return new[]
                {
                    "[General]", $"directURL={Site}/files/file/{FileName}/?do=download&r={FileID}&confirm=1&t=1"
                };

            }

            public virtual async Task<bool> LoadMetaData()
            {
                return false;
            }
        }
    }
}
