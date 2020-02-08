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
                    (Encrypt opts) => Encrypt.Run(opts),
                    (Decrypt opts) => Decrypt.Run(opts),
                    (Validate opts) => Validate.Run(opts),
                    (DownloadUrl opts) => DownloadUrl.Run(opts),
                    errs => 1);
        }
    }
}
