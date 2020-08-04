using System;
using System.Threading.Tasks;
using CommandLine;
using Wabbajack.Common;

namespace Wabbajack.CLI.Verbs
{
    [Verb("hash-variants", HelpText = "Print all the known variants (formats) of a hash")]
    public class HashVariants : AVerb
    {
        [Option('i', "input", Required = true, HelpText = "Input Hash")]
        public string Input { get; set; } = "";
        
        protected override async Task<ExitCode> Run()
        {
            var hash = Hash.Interpret(Input);
            Console.WriteLine($"Base64: {hash.ToBase64()}");
            Console.WriteLine($"Hex: {hash.ToHex()}");
            Console.WriteLine($"Long: {(long)hash}");
            Console.WriteLine($"ULong (uncommon): {(ulong)hash}");
            return ExitCode.Ok;
        }
    }
}
