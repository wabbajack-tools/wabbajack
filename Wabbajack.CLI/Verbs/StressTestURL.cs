using System;
using System.Linq;
using System.Threading.Tasks;
using CommandLine;
using Wabbajack.Common;
using Wabbajack.Lib;
using Wabbajack.Lib.Downloaders;

namespace Wabbajack.CLI.Verbs
{
    [Verb("stress-test-url", HelpText = "Verify a file rapidly, trying to make it fail")]
    public class StressTestURL : AVerb
    {
        [Option('i', "input", Required = true, HelpText = "Input url to stress")]
        public string Input { get; set; } = "";
        private Uri _Input => new Uri(Input);
        
        protected override async Task<ExitCode> Run()
        {
            using var queue = new WorkQueue();
            var state = await DownloadDispatcher.Infer(_Input);
            if (state == null)
            {
                Console.WriteLine("Could not parse URL");
            }

            Console.WriteLine("Performing initial download");
            await using var temp = new TempFile();
            var archive = new Archive(state!);
            if (!await state!.Download(archive, temp.Path))
            {
                Console.WriteLine("Failed initial download");
            }

            var hash = await temp.Path.FileHashAsync();
            archive.Hash = hash!.Value;
            archive.Size = temp.Path.Size;
            Console.WriteLine($"Hash: {archive.Hash} Size: {archive.Size.ToFileSizeString()}");

            await Enumerable.Range(0, 100000)
                .PMap(queue, async idx =>
                {
                    if (!await state.Verify(archive))
                    {
                        throw new Exception($"{idx} Verification failed");
                    }
                    Console.WriteLine($"{idx} Verification passed");
                });
            return ExitCode.Ok;
        }
    }
}
