using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
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
using Path = Alphaleonis.Win32.Filesystem.Path;

namespace Wabbajack.Lib.Downloaders
{
    public class YouTubeDownloader : IDownloader
    {
        public async Task<AbstractDownloadState> GetDownloaderState(dynamic archiveINI)
        {
            var directURL = (Uri)DownloaderUtils.GetDirectURL(archiveINI);
            var state = (State)UriToState(directURL);
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

        internal static AbstractDownloadState UriToState(Uri directURL)
        {
            if (directURL == null || !directURL.Host.EndsWith("youtube.com"))
            {
                return null;
            }

            var key = HttpUtility.ParseQueryString(directURL.Query)["v"];
            return key != null ? new State {Key = key} : null;
        }

        public async Task Prepare()
        {
        }

        public class State : AbstractDownloadState
        {
            public string Key { get; set; }
            public List<Track> Tracks { get; set; } = new List<Track>();
            public override object[] PrimaryKey => new object[] {Key};

            public class Track
            {
                public enum FormatEnum
                {
                    XWM,
                    WAV
                }
                public FormatEnum Format { get; set; }
                public string Name { get; set; }
                public TimeSpan Start { get; set; }
                public TimeSpan End { get; set; }
            }
            
            public override bool IsWhitelisted(ServerWhitelist whitelist)
            {
                return true;
            }

            public override async Task<bool> Download(Archive a, string destination)
            {
                try
                {
                    using var queue = new WorkQueue();
                    using var folder = new TempFolder();
                    Directory.CreateDirectory(Path.Combine(folder.Dir.FullName, "tracks"));
                    var client = new YoutubeClient(Common.Http.ClientFactory.Client);
                    var meta = await client.GetVideoAsync(Key);
                    var video = await client.GetVideoMediaStreamInfosAsync(Key);
                    var all = video.GetAll();
                    var stream = video.GetAll().OfType<AudioStreamInfo>().Where(f => f.AudioEncoding == AudioEncoding.Opus).OrderByDescending(a => a.Bitrate)
                        .ToArray().First();

                    var initialDownload = Path.Combine(folder.Dir.FullName, "initial_download");

                    var trackFolder = Path.Combine(folder.Dir.FullName, "tracks");

                    await using (var fs = File.Create(initialDownload))
                    {
                        await client.DownloadMediaStreamAsync(stream, fs, new Progress($"Downloading {a.Name}"),
                            CancellationToken.None);
                    }

                    File.Copy(initialDownload, @$"c:\tmp\{Path.GetFileName(destination)}.dest_stream");
                    
                    await Tracks.PMap(queue, async track =>
                    {
                        Utils.Status($"Extracting track {track.Name}");
                        await ExtractTrack(initialDownload, trackFolder, track);
                    });

                    await using var dest = File.Create(destination);
                    using var ar = new ZipArchive(dest, ZipArchiveMode.Create);
                    foreach (var track in Directory.EnumerateFiles(trackFolder).OrderBy(e => e))
                    {
                        Utils.Status($"Adding {Path.GetFileName(track)} to archive");
                        var entry = ar.CreateEntry(Path.Combine("Data", "tracks", track.RelativeTo(trackFolder)), CompressionLevel.NoCompression);
                        entry.LastWriteTime = meta.UploadDate;
                        await using var es = entry.Open();
                        await using var ins = File.OpenRead(track);
                        await ins.CopyToAsync(es);
                    }
                        
                    return true;
                }
                catch (VideoUnavailableException ex)
                {
                    return false;
                }

            }

            private const string FFMpegPath = "Downloaders/Converters/ffmpeg.exe";
            private const string xWMAEncodePath = "Downloaders/Converters/xWMAEncode.exe";
            private async Task ExtractTrack(string source, string dest_folder, Track track)
            {
                var info = new ProcessStartInfo
                {
                    FileName = FFMpegPath,
                    Arguments =
                        $"-threads 1 -i \"{source}\" -ss {track.Start} -t {track.End - track.Start} \"{dest_folder}\\{track.Name}.wav\"",
                    RedirectStandardError = true,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                
                var p = new Process {StartInfo = info};
                p.Start();
                ChildProcessTracker.AddProcess(p);

                var output = await p.StandardError.ReadToEndAsync();

                try
                {
                    p.PriorityClass = ProcessPriorityClass.BelowNormal;
                }
                catch (Exception e)
                {
                    Utils.Error(e, "Error while setting process priority level for ffmpeg.exe");
                }
                p.WaitForExit();

                if (track.Format == Track.FormatEnum.WAV) return;
                
                info = new ProcessStartInfo
                {
                    FileName = xWMAEncodePath,
                    Arguments =
                        $"-b 192000 \"{dest_folder}\\{track.Name}.wav\" \"{dest_folder}\\{track.Name}.xwm\"",
                    RedirectStandardError = true,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                
                p = new Process {StartInfo = info};

                p.Start();
                ChildProcessTracker.AddProcess(p);

                var output2 = await p.StandardError.ReadToEndAsync();

                try
                {
                    p.PriorityClass = ProcessPriorityClass.BelowNormal;
                }
                catch (Exception e)
                {
                    Utils.Error(e, "Error while setting process priority level for ffmpeg.exe");
                }
                p.WaitForExit();
                
                if (File.Exists($"{dest_folder}\\{track.Name}.wav"))
                    File.Delete($"{dest_folder}\\{track.Name}.wav");
                
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
