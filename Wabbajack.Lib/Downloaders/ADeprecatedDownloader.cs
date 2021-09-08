using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Wabbajack.Common;
using Wabbajack.Common.Serialization.Json;
using Wabbajack.Lib.Validation;

namespace Wabbajack.Lib.Downloaders
{
    public class ADeprecatedDownloader<TDownloader, TState> : IDownloader
    where TDownloader: ADeprecatedDownloader<TDownloader, TState>
    where TState: ADeprecatedDownloader<TDownloader, TState>.State
    {
        public async Task<AbstractDownloadState?> GetDownloaderState(dynamic archiveINI, bool quickMode = false)
        {
            return null;
        }

        public Task Prepare() => Task.CompletedTask;

        public class State : AbstractDownloadState
        {
            [JsonName("PrimaryKeyString")] private string _primaryKeyString { get; set; } = "";
            public override object[] PrimaryKey => _primaryKeyString.Split("|").Cast<object>().ToArray();
            
            public override bool IsWhitelisted(ServerWhitelist whitelist)
            {
                return true;
            }

            public override Task<bool> Download(Archive a, AbsolutePath destination)
            {
                throw new Exception($"Downloader {this.GetType().FullName} is deprecated");
            }

            public override async Task<bool> Verify(Archive archive, CancellationToken? token = null)
            {
                return false;
            }

            public override IDownloader GetDownloader()
            {
                return DownloadDispatcher.GetInstance<TDownloader>();
            }

            public override string? GetManifestURL(Archive a)
            {
                return null;
            }

            public override string[] GetMetaIni()
            {
                return new[] {"[General]", "downloaderIsDeprecated=True"};
            }
        }
    }
}
