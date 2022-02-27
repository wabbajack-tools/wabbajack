using System;
using System.IO;
using System.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Wabbajack.Common;
using Wabbajack.Common.IO;
using Wabbajack.Common.Serialization.Json;
using Wabbajack.Lib.Validation;

namespace Wabbajack.Lib.Downloaders
{
    public class ManualDownloader : IDownloader
    {
        class FileEvent
        {
            public string FullPath { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public long Size { get; set; }
        }

        public ManualDownloader()
        {
        }

        public async Task<AbstractDownloadState?> GetDownloaderState(dynamic archiveINI, bool quickMode)
        {
            var url = archiveINI?.General?.manualURL;
            var prompt = archiveINI?.General?.prompt;
            return url != null ? new State(url, prompt ?? "") : null;
        }

        public async Task Prepare()
        {
        }

        [JsonName("ManualDownloader")]
        public class State : AbstractDownloadState
        {
            public string Url { get; }
            public string Prompt { get; }

            [JsonIgnore]
            public override object[] PrimaryKey => new object[] { Url };

            public State(string url, string prompt)
            {
                Url = url;
                Prompt = prompt;
            }

            public override bool IsWhitelisted(ServerWhitelist whitelist)
            {
                return Url == "<TESTING>" || whitelist.AllowedPrefixes.Any(p => Url.StartsWith(p));
            }

            public override async Task<bool> Download(Archive a, AbsolutePath destination)
            {
                if ((new Uri(Url)).Host == "mega.nz")
                {
                    await Utils.Log(await ManuallyDownloadMegaFile.Create(this, destination)).Task;
                    return true;
                }

                if ((new Uri(Url)).Host.EndsWith("loverslab.com"))
                {
                    await Utils.Log(await ManuallyDownloadLoversLabFile.Create(this, destination)).Task;
                    return true;
                }
                else
                {
                    var (uri, client) = await Utils.Log(await ManuallyDownloadFile.Create(this)).Task;
                    var state = new HTTPDownloader.State(uri.ToString()) {Client = client};
                    return await state.Download(a, destination);
                }
            }

            public override async Task<bool> Verify(Archive a, CancellationToken? token)
            {
                return true;
            }

            public override IDownloader GetDownloader()
            {
                return DownloadDispatcher.GetInstance<ManualDownloader>();
            }

            public override string GetManifestURL(Archive a)
            {
                return Url;
            }

            public override string[] GetMetaIni()
            {
                return new []
                {
                    "[General]",
                    $"manualURL={Url}",
                    $"prompt={Prompt}"
                };
            }
        }
    }
}
