using System;
using System.Threading.Tasks;
using CommandLine;
using Compression.BSA;
using Wabbajack.Common;

namespace Wabbajack.CLI.Verbs
{
    [Verb("bsa-dump", HelpText = "Print detailed info about the contents of a BSA")]
    public class BSADump : AVerb
    {
        [Option('i', "input", Required = true, HelpText = "Input BSA to dump")]
        public string Input { get; set; } = "";
        
        protected override async Task<ExitCode> Run()
        {
            await using var bsa = BSADispatch.OpenRead(Input.RelativeTo(AbsolutePath.GetCurrentDirectory()));
            bsa.Dump(line => Console.WriteLine(line));
            return ExitCode.Ok;
        }
    }
}
