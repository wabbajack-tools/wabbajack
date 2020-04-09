using System;
using System.Linq;
using System.Threading.Tasks;
using Wabbajack.Common;
using YoutubeExplode;
#nullable enable

namespace Wabbajack.Lib.Downloaders.UrlDownloaders
{
    public class YoutubeInferencer : IUrlInferencer
    {
        public async Task<AbstractDownloadState?> Infer(Uri uri)
        {
            var state = YouTubeDownloader.UriToState(uri) as YouTubeDownloader.State;
            if (state == null) return null;
            
            var client = new YoutubeClient(Common.Http.ClientFactory.Client);
            var video = await client.GetVideoAsync(state.Key);

            var desc = video.Description;

            var lines = desc.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Trim())
                .Select(line =>
                {
                    var segments = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (segments.Length == 0) return (TimeSpan.Zero, string.Empty);
                    
                    if (TryParseEx(segments.First(), out var s1))
                        return (s1, string.Join(" ", segments.Skip(1)));
                    if (TryParseEx(Enumerable.Last(segments), out var s2))
                        return (s2, string.Join(" ", Utils.ButLast(segments)));
                    return (TimeSpan.Zero, string.Empty);
                })
                .Where(t => t.Item2 != null)
                .ToList();

            var tracks = lines.Select((line, idx) => new YouTubeDownloader.State.Track
            {
                Name = Sanitize(line.Item2),
                Start = line.Item1,
                End = idx < lines.Count - 1 ? lines[idx + 1].Item1 : video.Duration,
                Format = YouTubeDownloader.State.Track.FormatEnum.XWM
            }).ToList();

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
