using System;
using System.Threading.Tasks;
using CommandLine;
using Wabbajack.Common;
using Wabbajack.Lib.FileUploader;

namespace Wabbajack.CLI.Verbs
{
    [Verb("update-nexus-cache", HelpText = "Tell the build server to update the Nexus cache (requires Author API key)")]
    public class UpdateNexusCache : AVerb
    {
        protected override async Task<int> Run()
        {
            Console.WriteLine($"Job ID: {await AuthorAPI.UpdateNexusCache()}");
            return 0;
        }
    }
}
