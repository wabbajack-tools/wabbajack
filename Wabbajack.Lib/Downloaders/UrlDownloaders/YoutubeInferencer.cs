using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DynamicData;
using Org.BouncyCastle.Utilities.Collections;
using Wabbajack.Common;
using YoutubeExplode;

namespace Wabbajack.Lib.Downloaders.UrlDownloaders
{
    public class YoutubeInferencer : IUrlInferencer
    {
        public async Task<AbstractDownloadState?> Infer(Uri uri)
        {
            var state = YouTubeDownloader.UriToState(uri) as YouTubeDownloader.State;
            if (state == null) return null;
            
            var client = new YoutubeClient(Wabbajack.Lib.Http.ClientFactory.Client);
            var video = await client.Videos.GetAsync(state.Key);

            var desc = video.Description;
            
            var replaceChars = new HashSet<char>() {'_', '(', ')', '-'};

            var lines = desc.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Trim())
                .Select(line =>
                {
                    
                    var segments = replaceChars.Aggregate(line, (acc, c) => acc.Replace(c, ' '))
                                                      .Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (segments.Length == 0) return (TimeSpan.Zero, string.Empty);

                    foreach (var segment in segments)
                    {
                        if (TryParseEx(segment, out var si))
                        {
                            return (si, string.Join(" ", segments.Where(s => !s.Contains(":"))));
                        }
                    }
                    return (TimeSpan.Zero, string.Empty);
                })
                .Where(t => t.Item2 != string.Empty)
                .ToList();

            var tracks = lines.Select((line, idx) => new YouTubeDownloader.State.Track
            {
                Name = Sanitize(line.Item2),
                Start = line.Item1,
                End = idx < lines.Count - 1 ? lines[idx + 1].Item1 : video.Duration,
                Format = YouTubeDownloader.State.Track.FormatEnum.XWM
            }).ToList();

            foreach (var track in tracks)
            {
                Utils.Log($"Inferred Track {track.Name} {track.Format} {track.Start}-{track.End}");
            }

            state.Tracks = tracks;
            
            return state;
        }

        private string Sanitize(string input)
        {
            return input.Replace(":", "_").Replace("'", "").Replace("\"", "");
        }

        private static bool TryParseEx(string s, out TimeSpan span)
        {
            var ints = s.Split(':').Select(segment => int.TryParse(segment, out int v) ? v : -1).ToArray();
            if (ints.Any(i => i == -1))
            {
                span = TimeSpan.Zero;
                return false;
            }

            switch (ints.Length)
            {
                case 2:
                    span = new TimeSpan(0, ints[0], ints[1]);
                    break;
                case 3:
                    span = new TimeSpan(ints[0], ints[1], ints[2]);
                    break;
                default:
                    span = TimeSpan.Zero;
                    return false;
            }

            return true;
        }
    }
}
