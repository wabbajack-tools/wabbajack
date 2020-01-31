using Alphaleonis.Win32.Filesystem;
using CommandLine;
using Wabbajack.Common;

namespace Wabbajack.CLI.Verbs
{
    [Verb("decrypt", HelpText = @"Decrypt data from AppData\Local\Wabbajack and store it locally", Hidden = true)]
    public class Decrypt
    {
        [Option('n', "name", Required = true, HelpText = @"Credential to encrypt and store in AppData\Local\Wabbajack")]
        public string Name { get; set; }

        
        [Option('o', "output", Required = true, HelpText = @"Output file for the decrypted data")]
        public string Output { get; set; }

        public static int Run(Decrypt opts)
        {
            File.WriteAllBytes(opts.Output, Utils.FromEncryptedData(opts.Name));
            return 0;
        }
    }
}
