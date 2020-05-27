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
                    (Encrypt opts) => opts.Execute(),
                    (Decrypt opts) => opts.Execute(),
                    (Validate opts) => opts.Execute(),
                    (DownloadUrl opts) => opts.Execute(),
                    (UpdateModlists opts) => opts.Execute(),
                    (UpdateNexusCache opts) => opts.Execute(),
                    (ChangeDownload opts) => opts.Execute(),
                    (ServerLog opts) => opts.Execute(),
                    (MyFiles opts) => opts.Execute(),
                    (DeleteFile opts) => opts.Execute(),
                    (Changelog opts) => opts.Execute(),
                    (FindSimilar opts) => opts.Execute(),
                    (BSADump opts) => opts.Execute(),
                    (MigrateGameFolderFiles opts) => opts.Execute(),
                    errs => 1);
        }
    }
}
