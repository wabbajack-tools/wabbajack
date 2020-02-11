using CommandLine;
using Wabbajack.CLI.Verbs;

namespace Wabbajack.CLI
{
    public class Program
    {
        private static int Main(string[] args)
        {
            return Parser.Default.ParseArguments(args, OptionsDefinition.AllOptions)
                .MapResult(
                    (Encrypt opts) => opts.Execute(),
                    (Decrypt opts) => opts.Execute(),
                    (Validate opts) => opts.Execute(),
                    (DownloadUrl opts) => opts.Execute(),
                    (UpdateModlists opts) => opts.Execute(),
                    (UpdateNexusCache opts) => opts.Execute(),
                    errs => 1);
        }
    }
}
