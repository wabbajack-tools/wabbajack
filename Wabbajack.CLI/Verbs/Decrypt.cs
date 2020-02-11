using System.Threading.Tasks;
using Alphaleonis.Win32.Filesystem;
using CommandLine;
using Wabbajack.Common;

namespace Wabbajack.CLI.Verbs
{
    [Verb("decrypt", HelpText = @"Decrypt data from AppData\Local\Wabbajack and store it locally", Hidden = true)]
    public class Decrypt : AVerb
    {
        [Option('n', "name", Required = true, HelpText = @"Credential to encrypt and store in AppData\Local\Wabbajack")]
        public string Name { get; set; }

        
        [Option('o', "output", Required = true, HelpText = @"Output file for the decrypted data")]
        public string Output { get; set; }

        protected override async Task<int> Run()
        {
            File.WriteAllBytes(Output, Utils.FromEncryptedData(Name));
            return 0;
        }
    }
}
