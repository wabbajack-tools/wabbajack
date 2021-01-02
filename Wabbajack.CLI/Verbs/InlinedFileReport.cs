using System;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using CommandLine;
using Wabbajack.Common;
using Wabbajack.Lib;

namespace Wabbajack.CLI.Verbs
{
    [Verb("inlined-file-report", HelpText = "Reports on what could be causing .wabbajack bloat")]
    public class InlinedFileReport : AVerb
    {
        [Option('i', "input", Required = true, HelpText = "Input modlist to report on")]
        public string Input { get; set; } = "";
        protected override async Task<ExitCode> Run()
        {
            var file = (AbsolutePath)Input;

            var modlist = AInstaller.LoadFromFile(file);
            using var arch = new ZipArchive(await file.OpenRead(), ZipArchiveMode.Read);

            var reported = modlist.Directives
                .Select(d =>
                {
                    switch (d)
                    {
                        case CleanedESM esm:
                        {
                            var entry = arch.GetEntry(esm.SourceDataID.ToString());
                            return (entry!.Length, d);
                        }
                        case InlineFile inlined:
                            return (inlined.Size, d);
                        case PatchedFromArchive pfa:
                        {
                            var entry = arch.GetEntry(pfa.PatchID.ToString());
                            return (entry!.Length, d);
                        }
                        default:
                            return (0, d);
                    }
                })
                .Where(f => f.Item1 != 0)
                .OrderBy(f => f.Item1);

            foreach (var entry in reported)
            {
                switch (entry.d)
                {
                    case CleanedESM esm:
                        Console.WriteLine($"{entry.Item1.ToFileSizeString()} for a cleaned ESM patch on {entry.d.To}");
                        break;
                    case InlineFile ilined:
                        Console.WriteLine($"{entry.Item1.ToFileSizeString()} for a inlined file {entry.d.To}");
                        break;
                    case PatchedFromArchive archive:
                        Console.WriteLine($"{entry.Item1.ToFileSizeString()} for a patch on {entry.d.To}");
                        break;
                    default:
                        break;
                }
            }
            Console.WriteLine($"{reported.Count()} entries {reported.Sum(e => e.Item1).ToFileSizeString()} in total");
            return 0;
        }
    }
}
