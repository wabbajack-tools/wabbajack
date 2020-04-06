using System;
using System.Threading.Tasks;
using CommandLine;

namespace Wabbajack.CLI.Verbs
{
    [Verb("changelog", HelpText = "Generate a changelog using two different versions of the same Modlist.")]
    public class Changelog : AVerb
    {
        [Option("original", Required = true, HelpText = "The original/previous modlist")]
        public string? Original { get; set; }

        [Option("update", Required = true, HelpText = "The current/updated modlist")]
        public string? Update { get; set; }

        [Option('o', "output", Required = false, HelpText = "The output file")]
        public string? Output { get; set; }

        protected override Task<int> Run()
        {
            throw new NotImplementedException();
        }
    }
}
