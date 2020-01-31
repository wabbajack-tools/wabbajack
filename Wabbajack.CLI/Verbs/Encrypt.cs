using Alphaleonis.Win32.Filesystem;
using CommandLine;
using Wabbajack.Common;

namespace Wabbajack.CLI.Verbs
{
    [Verb("encrypt", HelpText = @"Encrypt local data and store it in AppData\Local\Wabbajack", Hidden = true)]
    public class Encrypt
    {
        [Option('n', "name", Required = true, HelpText = @"Credential to encrypt and store in AppData\Local\Wabbajack")]
        public string Name { get; set; }
        
        [Option('i', "input", Required = true, HelpText = @"Source data file name")]
        public string Input { get; set; }

        public static int Run(Encrypt opts)
        {
            File.ReadAllBytes(opts.Input).ToEcryptedData(opts.Name);
            return 0;
        }
    }
}
