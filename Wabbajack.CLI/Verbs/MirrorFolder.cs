using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommandLine;
using Wabbajack.Common;

namespace Wabbajack.CLI.Verbs
{
    [Verb("mirror-folder", HelpText = "Copies the files from one folder to the other, skipping file of the same size and copies in parallel")]
    public class MirrorFolder : AVerb
    {
        [IsDirectory(CustomMessage = "Downloads folder at %1 does not exist!")]
        [Option('f', "from", HelpText = "From folder", Required = true)]
        public string _FromFolder { get; set; } = "";

        public AbsolutePath FromFolder => (AbsolutePath)_FromFolder;
        
        
        [IsDirectory(CustomMessage = "Downloads folder at %1 does not exist!")]
        [Option('t', "to", HelpText = "To folder", Required = true)]
        public string _ToFolder { get; set; } = "";

        public AbsolutePath ToFolder => (AbsolutePath)_ToFolder;
        protected override async Task<ExitCode> Run()
        {
            var queue = new WorkQueue();

            var src = FromFolder.EnumerateFiles().Where(f => f.IsFile).ToList();
            Console.WriteLine($"Found {src.Count} files");
            int idx = 0;

            await src.PMap(queue, async f =>
            {
                var thisidx = Interlocked.Increment(ref idx);
                var dest = f.RelativeTo(FromFolder).RelativeTo(ToFolder);

                if (dest.IsFile && f.Size == dest.Size) return;

                Console.WriteLine($"({thisidx}/{src.Count}) Copying {f.RelativeTo(FromFolder)} - {f.Size.ToFileSizeString()}");
                await f.CopyToAsync(dest);
            });
            return ExitCode.Ok;
        }
    }
}
