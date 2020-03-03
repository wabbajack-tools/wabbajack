using System;
using System.Threading.Tasks;
using CommandLine;
using Wabbajack.Lib.FileUploader;

namespace Wabbajack.CLI.Verbs
{
    [Verb("my-files", HelpText = "List files I have uploaded to the CDN (requires Author API key)")] 
    public class MyFiles : AVerb
    {
        protected override async Task<int> Run()
        {
            var files = await AuthorAPI.GetMyFiles();
            foreach (var file in files)
                Console.WriteLine(file);
            return 0;
        }
    }
}
