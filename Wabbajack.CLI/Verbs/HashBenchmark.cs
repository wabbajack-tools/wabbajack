using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using CommandLine;
using Wabbajack.Common;

namespace Wabbajack.CLI.Verbs
{
    [Verb("hash-benchmark", HelpText = "Perform a benchmark of the hash routines by benchmarking all files in a folder")]
    public class HashBenchmark : AVerb
    {
        [Option('i', "input", Required = true, HelpText = "Input Folder")]
        public string _input { get; set; } = "";

        public AbsolutePath Input => (AbsolutePath)_input;

        [Option('t', "threads", Required = false, HelpText = "Thread Count (defaults to number of logical cores)")]
        public int ThreadCount { get; set; } = Environment.ProcessorCount;


        protected override async Task<ExitCode> Run()
        {
            using var queue = new WorkQueue(ThreadCount);

            var files = Input.EnumerateFiles().Select(f => (f, f.Size)).Take(1000).ToArray();
            var totalSize = files.Sum(f => f.Size);
            Console.WriteLine($"Found {files.Length} files and {totalSize.ToFileSizeString()} to hash");

            var stopwatch = new Stopwatch();
            stopwatch.Start();

            var su = new StatusUpdateTracker(1);
            
            await files.PMap(queue, su, async f =>
            {
                await f.f.FileHashAsync();
            });
            
            stopwatch.Stop();
            Console.WriteLine($"Hashed {totalSize.ToFileSizeString()} in {stopwatch.Elapsed.TotalSeconds} or {((long)(totalSize/stopwatch.Elapsed.TotalSeconds)).ToFileSizeString()}/sec");

            return ExitCode.Ok;
        }
    }
}
