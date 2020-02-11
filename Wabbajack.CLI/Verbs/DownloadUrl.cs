using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Alphaleonis.Win32.Filesystem;
using CommandLine;
using Wabbajack.Common;
using Wabbajack.Lib;
using Wabbajack.Lib.Downloaders;

namespace Wabbajack.CLI.Verbs
{
    [Verb("download-url", HelpText = "Infer a download state from a URL and download it")]
    public class DownloadUrl : AVerb
    {
        [Option('u', "url", Required = true, HelpText = "Url to download")]
        public Uri Url { get; set; }

        [Option('o', "output", Required = true, HelpText = "Output file name")]
        public string Output { get; set; }

        protected override async Task<int> Run()
        {
            var state = DownloadDispatcher.Infer(Url);
            if (state == null)
            {
                Console.WriteLine($"Could not find download source for URL {Url}");
                return 1;
            }
            
            DownloadDispatcher.PrepareAll(new []{state});

            using var queue = new WorkQueue();
            queue.Status
                .Where(s => s.ProgressPercent != Percent.Zero)
                .Debounce(TimeSpan.FromSeconds(1))
                .Subscribe(s => Console.WriteLine($"Downloading {s.ProgressPercent}"));

                new[] {state}
                .PMap(queue, async s =>
                {
                    await s.Download(new Archive {Name = Path.GetFileName(Output)}, Output);
                }).Wait();

            File.WriteAllLines(Output + ".meta", state.GetMetaIni());
            return 0;
        }

    }
}
