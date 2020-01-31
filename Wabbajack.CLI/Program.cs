using System;
using CommandLine;
using Wabbajack.CLI.Verbs;

namespace Wabbajack.CLI
{
    class Program
    {
        static int Main(string[] args)
        {
            return CommandLine.Parser.Default.ParseArguments(args, OptionsDefinition.AllOptions)
                .MapResult(
                    (Encrypt opts) => Encrypt.Run(opts),
                    (Decrypt opts) => Decrypt.Run(opts),
                    errs => 1);
        }
    }
}
