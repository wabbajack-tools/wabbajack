using System;
using System.Threading.Tasks;
using CommandLine;
using Wabbajack.Common;
using Wabbajack.Lib.Downloaders;

namespace Wabbajack.CLI.Verbs
{
    [Verb("parse-meta", HelpText = "Parse a .meta file, figure out the download state and print it")]
    public class ParseMeta : AVerb
    {
        
        [Option('i', "input", Required = true, HelpText = "Input meta file to parse")]
        public string Input { get; set; } = "";
        protected override async Task<ExitCode> Run()
        {
            var meta = (AbstractDownloadState)await DownloadDispatcher.ResolveArchive(((AbsolutePath)Input).LoadIniFile());
            if (meta == null)
            {
                Console.WriteLine("Cannot resolve meta!");
                return ExitCode.Error;
            }

                Console.WriteLine($"PrimaryKeyString : {meta.PrimaryKeyString}");
            return ExitCode.Ok;
        }
    }
}
