using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using CommandLine;
using Wabbajack.Common;
using Wabbajack.Lib;

namespace Wabbajack.CLI.Verbs
{
    [Verb("purge-archive", HelpText = "Purges an archive and all directives from a .wabbajack file")]
    public class PurgeArchive : AVerb
    {
        [Option('i', "input", Required = true, HelpText = "Input .wabbajack file")]
        public string Input { get; set; } = "";
        private AbsolutePath _Input => (AbsolutePath)Input;

        [Option('o', "output", Required = true, HelpText = "Output .wabbajack file")]
        public string Output { get; set; } = "";
        private AbsolutePath _Output => (AbsolutePath)Output;

        [Option('h', "hash", Required = true, HelpText = "Hash to purge")]
        public string ArchiveHash { get; set; } = "";
        private Hash _Hash => Hash.Interpret(ArchiveHash);

        protected override async Task<ExitCode> Run()
        {
            Utils.Log("Copying .wabbajack file");
            await _Input.CopyToAsync(_Output);

            Utils.Log("Loading modlist");

            await using var fs = await _Output.OpenWrite();
            using var ar = new ZipArchive(fs, ZipArchiveMode.Update);
            ModList modlist;
            await using (var entry = ar.Entries.First(e => e.Name == "modlist").Open())
            {
                modlist = entry.FromJson<ModList>();
            }

            Utils.Log("Purging archives");
            modlist.Archives = modlist.Archives.Where(a => a.Hash != _Hash).ToList();
            modlist.Directives = modlist.Directives.Select(d =>
            {
                if (d is FromArchive a)
                {
                    if (a.ArchiveHashPath.BaseHash == _Hash) return (false, d);
                }
                return (true, d);
            }).Where(d => d.Item1)
                .Select(d => d.d)
                .ToList();

            Utils.Log("Writing modlist");
            
            await using (var entry = ar.Entries.First(e => e.Name == "modlist").Open())
            {
                entry.SetLength(0);
                entry.Position = 0;
                modlist.ToJson(entry);
            }

            return ExitCode.Ok;
        }
    }
}
