using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommandLine;
using Wabbajack.Common;
using Wabbajack.Lib;
using Wabbajack.Lib.Downloaders;
using Wabbajack.Lib.LibCefHelpers;

namespace Wabbajack.CLI.Verbs
{
    [Verb("verify-all-downloads", HelpText = "Verify all downloads in a folder")]
    public class VerifyAllDownloads : AVerb
    {
        [Option('i', "input", Required = true, HelpText = "Input Folder")]
        public string _input { get; set; } = "";

        public AbsolutePath Input => (AbsolutePath)_input;

        [Option('t', "type", Required = false,
            HelpText = "Only verify files of this type of download state for example NexusDownloader+State")]
        public string StateType { get; set; } = "";
        
        protected override async Task<ExitCode> Run()
        {
            var files = Input.EnumerateFiles()
                .Where(f => f.WithExtension(Consts.MetaFileExtension).Exists)
                .ToArray();
            
            Console.WriteLine($"Found {files.Length} files to verify");

            using var queue = new WorkQueue();

            var states = (await files.PMap(queue, async f =>
            {
                var ini = f.WithExtension(Consts.MetaFileExtension).LoadIniFile();
                var state = (AbstractDownloadState?)await DownloadDispatcher.ResolveArchive(ini, quickMode: true);
                if (state == default)
                {
                    Console.WriteLine($"[Skipping] {f.FileName} because no meta could be interpreted");
                }
                
                if (!string.IsNullOrWhiteSpace(StateType) && !state!.PrimaryKeyString.StartsWith(StateType + "|"))
                {
                    Console.WriteLine(
                        $"[Skipping] {f.FileName} because type {state.PrimaryKeyString[0]} does not match filter");
                    return (f, null);
                }

                return (f, state);

            })).Where(s => s.state != null)
                .Select(s => (s.f, s.state!))
                .ToArray();

            await DownloadDispatcher.PrepareAll(states.Select(s => s.Item2));
            Helpers.Init();

            Console.WriteLine($"Found {states.Length} states to verify");
            int timedOut = 0;

            await states.PMap(queue, async p=>
            {
                try
                {
                    var (f, state) = p;


                    try
                    {
                        var cts = new CancellationTokenSource();
                        cts.CancelAfter(TimeSpan.FromMinutes(10));
                        var result =
                            await state!.Verify(new Archive(state) {Name = f.FileName.ToString(), Size = f.Size},
                                cts.Token);
                        Console.WriteLine($"[{(result ? "Failed" : "Passed")}] {f.FileName}");

                    }
                    catch (TaskCanceledException)
                    {
                        Console.WriteLine($"[Timed Out] {f.FileName} {state!.PrimaryKeyString}");
                        Interlocked.Increment(ref timedOut);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Exception] {p.f.FileName} {ex.Message}");
                }


            });
            
            Console.WriteLine($"[Total TimedOut] {timedOut}");
            Console.WriteLine("[Done]");

            return ExitCode.Ok;
        }
    }
}
