using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Compression;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Newtonsoft.Json;
using Wabbajack.Common;
using Wabbajack.Common.Serialization.Json;
using Wabbajack.Lib.Validation;
using YoutubeExplode;
using YoutubeExplode.Exceptions;
using YoutubeExplode.Videos.Streams;
using File = Alphaleonis.Win32.Filesystem.File;
using Path = Alphaleonis.Win32.Filesystem.Path;

namespace Wabbajack.Lib.Downloaders
{
    public class YouTubeDownloader : IDownloader
    {
        public async Task<AbstractDownloadState?> GetDownloaderState(dynamic archiveINI, bool quickMode)
        {
            var directURL = (Uri)DownloaderUtils.GetDirectURL(archiveINI);
            var state = UriToState(directURL) as State;
            if (state == null) return state;

            var idx = 0;
            while (true)
            {
                var section = archiveINI[$"track/{idx}"];
                if (section.name == null) break;
                
                var track = new State.Track();
                track.Name = section.name;
                track.Start = TimeSpan.Parse(section.start);
                track.End = TimeSpan.Parse(section.end);
                track.Format = Enum.Parse<State.Track.FormatEnum>(section.format);
                state.Tracks.Add(track);
                idx += 1;
            }

            return state;
        }

        internal static AbstractDownloadState? UriToState(Uri directURL)
        {
            if (directURL == null || !directURL.Host.EndsWith("youtube.com"))
            {
                return null;
            }

            var key = HttpUtility.ParseQueryString(directURL.Query)["v"];
            return key != null ? new State(key) : null;
        }

        public async Task Prepare()
        {
        }

        [JsonName("YouTubeDownloader")]
        public class State : AbstractDownloadState
        {
            public string Key { get; }
            
            public List<Track> Tracks { get; set; } = new List<Track>();
            
            [JsonIgnore]
            public override object[] PrimaryKey => new object[] {Key};

            public State(string key)
            {
                Key = key;
            }

            [JsonName("YouTubeTrack")]
            public class Track
            {
                public enum FormatEnum
                {
                    XWM,
                    WAV
                }
                public FormatEnum Format { get; set; }

                public string Name { get; set; } = string.Empty;
                
                public TimeSpan Start { get; set; }
                
                public TimeSpan End { get; set; }
            }
            
            public override bool IsWhitelisted(ServerWhitelist whitelist)
            {
                return true;
            }

            public override async Task<bool> Download(Archive a, AbsolutePath destination)
            {
                try
                {
                    using var queue = new WorkQueue();
                    await using var folder = await TempFolder.Create();
                    folder.Dir.Combine("tracks").CreateDirectory();
                    var client = new YoutubeClient(Common.Http.ClientFactory.Client);
                    var meta = await client.Videos.GetAsync(Key);
                    var video = await client.Videos.Streams.GetManifestAsync(Key);
                    var stream = video.Streams.OfType<AudioOnlyStreamInfo>().Where(f => f.AudioCodec.StartsWith("mp4a")).OrderByDescending(a => a.Bitrate)
                        .ToArray().First();

                    var initialDownload = folder.Dir.Combine("initial_download");

                    var trackFolder = folder.Dir.Combine("tracks");

                    await using (var fs = await initialDownload.Create())
                    {
                        await client.Videos.Streams.CopyToAsync(stream, fs, new Progress($"Downloading {a.Name}"),
                            CancellationToken.None);
                    }

                    await Tracks.PMap(queue, async track =>
                    {
                        Utils.Status($"Extracting track {track.Name}");
                        await ExtractTrack(initialDownload, trackFolder, track);
                    });

                    await using var dest = await destination.Create();
                    using var ar = new ZipArchive(dest, ZipArchiveMode.Create);
                    foreach (var track in trackFolder.EnumerateFiles().OrderBy(e => e))
                    {
                        Utils.Status($"Adding {track.FileName} to archive");
                        var entry = ar.CreateEntry(Path.Combine("Data", "Music", (string)track.RelativeTo(trackFolder)), CompressionLevel.NoCompression);
                        entry.LastWriteTime = meta.UploadDate;
                        await using var es = entry.Open();
                        await using var ins = await track.OpenRead();
                        await ins.CopyToAsync(es);
                    }
                        
                    return true;
                }
                catch (VideoUnavailableException)
                {
                    return false;
                }

            }

            private AbsolutePath FFMpegPath => "Downloaders/Converters/ffmpeg.exe".RelativeTo(AbsolutePath.EntryPoint);
            private AbsolutePath xWMAEncodePath = "Downloaders/Converters/xWMAEncode.exe".RelativeTo(AbsolutePath.EntryPoint);
            private Extension WAVExtension = new Extension(".wav");
            private Extension XWMExtension = new Extension(".xwm");
            private async Task ExtractTrack(AbsolutePath source, AbsolutePath destFolder, Track track)
            {
                Utils.Log($"Extracting {track.Name}");
                var wavFile = track.Name.RelativeTo(destFolder).WithExtension(WAVExtension);
                var process = new ProcessHelper
                {
                    Path = FFMpegPath,
                    Arguments = new object[] {"-hide_banner", "-loglevel", "panic", "-threads", 1, "-i", source, "-ss", track.Start, "-t", track.End - track.Start, wavFile},
                    ThrowOnNonZeroExitCode = true
                };

                var ffmpegLogs = process.Output.Where(arg => arg.Type == ProcessHelper.StreamType.Output)
                    .ForEachAsync(val =>
                    {
                        Utils.Status($"Extracting {track.Name} - {val.Line}");
                    });
                
                await process.Start();
                ffmpegLogs.Dispose();

                if (track.Format == Track.FormatEnum.WAV) return;
                
                process = new ProcessHelper()
                {
                    Path = xWMAEncodePath,
                    Arguments = new object[] {"-b", 192000, wavFile, wavFile.ReplaceExtension(XWMExtension)},
                    ThrowOnNonZeroExitCode = true
                };
                
                var xwmLogs = process.Output.Where(arg => arg.Type == ProcessHelper.StreamType.Output)
                    .ForEachAsync(val =>
                    {
                        Utils.Log($"Encoding {track.Name} - {val.Line}");
                    });

                await process.Start();
                xwmLogs.Dispose();

                await wavFile.DeleteAsync();
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
                    var video = await client.Videos.GetAsync(Key);
                    return true;
                }
                catch (VideoUnavailableException)
                {
                    return false;
                }
                
            }

            public override IDownloader GetDownloader()
            {
                return DownloadDispatcher.GetInstance<YouTubeDownloader>();
            }

            public override string GetManifestURL(Archive a)
            {
                return $"https://www.youtube.com/watch?v={Key}";
            }

            public override string[] GetMetaIni()
            {
                IEnumerable<string> start = new List<string> {"[General]", $"directURL=https://www.youtube.com/watch?v={Key}"};
                start = start.Concat(Tracks.SelectMany((track, idx) =>
                {
                    return new[]
                    {
                        $"\n[track/{idx}]", $"name={track.Name}", $"start={track.Start}", $"end={track.End}",
                        $"format={track.Format}"
                    };

                }));
                return start.ToArray();
            }
        }
    }
}
