using System;
using System.Threading.Tasks;
using CommandLine;
using Wabbajack.Lib.FileUploader;

namespace Wabbajack.CLI.Verbs
{
    [Verb("purge-nexus-cache", HelpText = "Purge the Wabbajack Server's info about a given nexus Mod ID. Future requests for this mod will be grabbed from the Nexus")]
    public class PurgeNexusCache : AVerb
    {
        [Option('i', "mod-id", Required = true, HelpText = @"Mod ID to purge")]
        public long ModId { get; set; } = 0;
        protected override async Task<ExitCode> Run()
        {
            Console.WriteLine(await AuthorAPI.PurgeNexusModInfo(ModId));
            return ExitCode.Ok;
        }
    }
}
