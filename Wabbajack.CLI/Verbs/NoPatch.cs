using System;
using System.Threading.Tasks;
using CommandLine;
using Wabbajack.Common;
using Wabbajack.Lib.FileUploader;

namespace Wabbajack.CLI.Verbs
{
    [Verb("no-patch", HelpText = "Add a hash to the no-patch list and purge existing patches")]
    public class NoPatch : AVerb
    {
        [Option('h', "hash", Required = true, HelpText = "Hash to purge")]
        public string NoPatchHash { get; set; } = "";
        
        [Option('r', "rationale", Required = true, HelpText = "Why are you purging this?")]
        public string Rationale { get; set; } = "";
        protected override async Task<ExitCode> Run()
        {
            var hash = Hash.FromBase64(NoPatchHash);
            Console.WriteLine(await AuthorAPI.NoPatch(hash, Rationale));
            return ExitCode.Ok;
        }
    }
}
