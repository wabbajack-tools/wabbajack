using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Wabbajack.Common;
using Wabbajack.Lib.Validation;
using YoutubeExplode;
using YoutubeExplode.Exceptions;
using YoutubeExplode.Models.MediaStreams;
using File = Alphaleonis.Win32.Filesystem.File;

namespace Wabbajack.Lib.Downloaders
{
    public class YouTubeDownloader : IDownloader
    {
        public async Task<AbstractDownloadState> GetDownloaderState(dynamic archiveINI)
        {
            var directURL = (Uri)DownloaderUtils.GetDirectURL(archiveINI);
            if (directURL.Host.EndsWith("youtube.com"))
            {
                var key = HttpUtility.ParseQueryString(directURL.Query)["v"];
                if (key != null)
                    return new State {Key = key};
            }

            return null;
        }

        public async Task Prepare()
        {
        }

        public class State : AbstractDownloadState
        {
            public string Key { get; set; }
            public override object[] PrimaryKey => new object[] {Key};
            
            public override bool IsWhitelisted(ServerWhitelist whitelist)
            {
                return true;
            }

            public override async Task<bool> Download(Archive a, string destination)
            {
                try
                {
                    var client = new YoutubeClient(Common.Http.ClientFactory.Client);
                    var video = await client.GetVideoMediaStreamInfosAsync(Key);
                    var stream = video.GetAll().OfType<AudioStreamInfo>().OrderByDescending(a => a.Bitrate).ToArray().First();
                    await using var fs = File.Create(destination);
                    await client.DownloadMediaStreamAsync(stream, fs, new Progress($"Downloading {a.Name}"), CancellationToken.None);
                    return true;
                }
                catch (VideoUnavailableException ex)
                {
                    return false;
                }

            }
            
            private class Progress : IProgress<double>
            {
                private string _prefix;

                public Progress(string prefix)
                {
                    _prefix = prefix;
                }
                public void Report(double value)
                {
                    Utils.Status(_prefix, Percent.FactoryPutInRange(value));
                }
            }

            public override async Task<bool> Verify(Archive archive)
            {
                try
                {
                    var client = new YoutubeClient(Common.Http.ClientFactory.Client);
                    var video = await client.GetVideoAsync(Key);
                    return true;
                }
                catch (VideoUnavailableException ex)
                {
                    return false;
                }
                
            }

            public override IDownloader GetDownloader()
            {
                throw new NotImplementedException();
            }

            public override string GetManifestURL(Archive a)
            {
                throw new NotImplementedException();
            }

            public override string[] GetMetaIni()
            {
                throw new NotImplementedException();
            }
        }
    }
}
