using CommandLine;
using Wabbajack.CLI.Verbs;
using Wabbajack.Common;
using Console = System.Console;
using System.Reactive.Linq;
using System;

namespace Wabbajack.CLI
{
    public class Program
    {
        private static int Main(string[] args)
        {
            Utils.LogMessages.Subscribe(Console.WriteLine);
            return Parser.Default.ParseArguments(args, OptionsDefinition.AllOptions)
                .MapResult(
                    (AVerb opts) => opts.Execute(),
                    errs => 1);
        }
    }
}
